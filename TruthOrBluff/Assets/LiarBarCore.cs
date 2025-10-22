using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LiarsBar;
using MoreMountains.Feedbacks;

namespace LiarsBar
{
    // ====== 配置与基础类型 ======
    // 已拆分到 GameConfig.cs 文件

    public class Card
    {
        public string Id; // 卡片唯一标识符
        public Rank Rank; // 卡片点数
        public Card(string id, Rank rank) { Id = id; Rank = rank; }
        public override string ToString() => $"{Rank}-{Id}"; // 返回卡片的字符串表示
    }



    public enum Phase { Claim, Response } // 游戏阶段：声明或响应

    public class LastClaim
    {
        public int PlayerIndex; // 声明者索引
        public string CardId; // 声明的卡片ID
        public Rank DeclaredRank; // 声明的牌面
    }

    public class GameState
    {
        public List<Player> Players = new List<Player>(); // 玩家列表
        public int Turn = 0; // 当前轮到的玩家索引
        public Phase Phase = Phase.Claim; // 当前游戏阶段
        public Rank TableRank; // 桌面固定牌面
        public List<Card> Pile = new List<Card>(); // 桌面牌堆
        public LastClaim LastClaim = null; // 上一次声明
        public int BulletSlot = 4; // 惩罚轨触发位置
        public int ChamberCount = 0; // 当前惩罚轨推进格数
        public int Step = 0; // 总步数
        public StringBuilder Log = new StringBuilder(); // 游戏日志

        public int AliveCount() => Players.Count(p => p.Alive); // 存活玩家数量
        public bool EveryoneEmptyHand() => Players.Where(p => p.Alive).All(p => p.Hand.Count == 0); // 是否所有玩家手牌为空
    }

    // ====== 机器人接口 ======
    public interface IAgent
    {
        string Name { get; } // 机器人名称
        string ChooseClaimCard(GameState s, int playerIndex, Random rng); // 声明阶段选择卡片
        bool DecideChallenge(GameState s, int responderIndex, Random rng); // 响应阶段是否质疑
    }

    // ====== 单例基类 ======
    public class Singleton<T> where T : class, new()
    {
        private static T _instance;
        private static readonly object _lock = new object();

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new T();
                        }
                    }
                }
                return _instance;
            }
        }

        // 手动设置实例（用于需要构造参数的情况）
        public static void SetInstance(T instance)
        {
            lock (_lock)
            {
                _instance = instance;
            }
        }

        // 清除实例
        public static void ClearInstance()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }
    }

    // ====== 引擎（纯逻辑） ======
    public class GameEngine : Singleton<GameEngine>
    {
        public GameConfig Config { get; private set; } // 游戏配置
        public GameState State { get; private set; } = new GameState(); // 游戏状态
        IAgent[] agents; // 机器人代理
        Random rng; // 随机数生成器

        public MMFeedbacks PlayCardFeedbacks; // 播放出牌反馈
        MMFeedbacks feedbacks; // 临时反馈变量

        // 无参构造函数（单例需要）
        public GameEngine() { }

        // 初始化方法（替代原构造函数）
        public void Initialize(GameConfig config, IAgent[] agents)
        {
            if (agents == null || agents.Length != config.PlayerCount)
                throw new ArgumentException($"agents 数量必须等于 PlayerCount（目前 {agents?.Length}）");
            
            Config = config;
            this.agents = agents;
            rng = new Random(config.Seed);
            State = new GameState(); // 重置状态

            // 初始化玩家
            for (int i = 0; i < config.PlayerCount; i++)
            {
                State.Players.Add(new Player { Index = i, Name = agents[i].Name });
            }
            State.TableRank = config.TableRank;
            State.BulletSlot = Math.Clamp(config.BulletSlot, 1, 6);

            // 构建并发牌
            var deck = BuildDeck(config);
            Shuffle(deck, rng);
            DealRoundRobin(deck, State.Players);

            // 初始状态日志
            State.Turn = 0;
            State.Phase = Phase.Claim;
            LogLine($"== 开局：玩家 {string.Join("，", State.Players.Select(p => p.ToString()))}；" +
                    $"桌面牌面 {State.TableRank}；子弹槽在第 {State.BulletSlot} 格 ==");

            for (int i = 0; i < State.Players.Count; i++)
            {
                var p = State.Players[i];
                LogLine($"{p} 手牌：{string.Join(",", p.Hand.Select(c => c.Rank))}");
            }
        }

        // —— 对外：一路跑到结束（用于无 UI 的模拟） ——
        // 运行游戏直到结束或达到最大步数。
        public string RunUntilGameOver(int maxSteps = 500)
        {
            while (!IsGameOver() && State.Step < maxSteps)
            {
                StepOnce();
            }
            if (IsGameOver())
            {
                var alive = State.Players.Where(p => p.Alive).ToList();
                if (alive.Count == 1)
                {
                    LogLine($"== 胜者：{alive[0]} ==");
                }
                else
                {
                    LogLine("== 牌用尽/步数上限，和局（用于测试）==");
                }
            }
            else
            {
                LogLine("== 达到步数上限，强制结束（防御性）==");
            }
            return State.Log.ToString();
        }

        // —— 核心一步（Claim 或 Response） ——
        // 执行游戏中的一步（声明或响应阶段）。
        public void StepOnce()
        {
            if (IsGameOver()) return; // 如果游戏结束，直接返回
            State.Step++; // 增加步数

            if (State.Phase == Phase.Claim) // 当前阶段为声明阶段
            {
                ProcessClaimPhase();
            }
            else // 当前阶段为响应阶段
            {
                ProcessResponsePhase();
            }
        }

        // 处理声明阶段
        void ProcessClaimPhase()
        {
            var me = CurrentPlayer(); // 获取当前玩家
            if (!me.Alive) { AdvanceTurnToNextAlive(); return; } // 如果玩家已出局，跳到下一个存活玩家

            if (me.Hand.Count == 0) // 如果玩家手牌为空
            {
                LogLine($"{me} 没牌可出，跳过。"); // 记录日志：玩家跳过
                AdvanceTurnToNextAlive(); // 跳到下一个存活玩家
                return;
            }

            // 玩家打出一张牌并声明
            var card = ChooseAndPlayCard(me);
            RecordClaim(me, card);
            State.Phase = Phase.Response; // 切换到响应阶段
        }

        // 选择并打出一张卡片
        Card ChooseAndPlayCard(Player player)
        {
            // 由代理选择一张手牌打出（面朝下），声明固定的 TableRank
            var chooseId = agents[player.Index].ChooseClaimCard(State, player.Index, rng); // 代理选择要打出的卡片
            var card = PopCardFromHand(player, chooseId); // 从手牌中移除选定的卡片
            if (card == null) // 如果选择无效，随机出一张卡片
            {
                card = PopRandomCard(player, rng);
            }
            State.Pile.Add(card); // 将卡片加入桌面牌堆
            MyPlayFeedbacks(player); // 播放出牌反馈
            return card;
        }

        // 记录声明
        void RecordClaim(Player player, Card card)
        {
            State.LastClaim = new LastClaim
            {
                PlayerIndex = player.Index, // 声明者索引
                CardId = card.Id, // 声明的卡片ID
                DeclaredRank = State.TableRank // 声明的牌面
            };
            LogLine($"[声明] {player} 面朝下打出一张牌，声明为 {State.TableRank}（下家选择质疑/接受）"); // 记录声明日志
        }

        // 处理响应阶段
        void ProcessResponsePhase()
        {
            int responder = NextAlive(State.Turn); // 获取下一个存活玩家
            var me = State.Players[responder]; // 获取响应玩家
            
            if (State.LastClaim == null) // 如果没有可响应的声明
            {
                LogLine("（无可响应的声明，自动进入下一位声明）"); // 记录日志：无声明
                AdvanceTurnToNextAlive(); // 跳到下一个存活玩家
                State.Phase = Phase.Claim; // 切换到声明阶段
                return;
            }

            bool challenge = agents[responder].DecideChallenge(State, responder, rng); // 代理决定是否质疑
            if (challenge) // 如果选择质疑
            {
                ResolveChallenge(responder); // 处理质疑逻辑
            }
            else // 如果接受声明
            {
                AcceptClaim(me, responder);
            }
        }

        // 接受声明
        void AcceptClaim(Player responder, int responderIndex)
        {
            LogLine($"[接受] {responder} 接受声明，不翻牌。轮到 {responder} 声明。"); // 记录接受日志
            State.LastClaim = null; // 接受后清除声明
            State.Turn = responderIndex; // 设置下一个声明者为当前响应者
            State.Phase = Phase.Claim; // 切换到声明阶段
        }

        // —— 质疑结算：翻牌、推进惩罚轨、是否出局 ——
        // 处理质疑，翻开牌并决定结果。
        void ResolveChallenge(int responderIndex)
        {
            var claimantIndex = State.LastClaim.PlayerIndex;
            var claimant = State.Players[claimantIndex];
            var responder = State.Players[responderIndex];
            var lastCard = State.Pile.Last();
            bool truthful = (lastCard.Rank == State.TableRank);
            string truthWord = truthful ? "真实" : "撒谎";

            LogLine($"[质疑] {responder} 质疑 {claimant}，翻开牌 => 实际是 {lastCard.Rank}（{truthWord}）");

            int loserIndex = truthful ? responderIndex : claimantIndex;
            var loser = State.Players[loserIndex];

            // 惩罚轨推进
            State.ChamberCount++;
            bool hit = (State.ChamberCount == State.BulletSlot);

            if (hit)
            {
                loser.Alive = false;
                State.ChamberCount = 0; // 命中则重置
                LogLine($"[命中子弹槽] {loser} 被淘汰！当前存活：{string.Join("，", State.Players.Where(p => p.Alive).Select(p => p.ToString()))}");
            }
            else
            {
                LogLine($"[惩罚轨] 前进到第 {State.ChamberCount}/6 格；未命中子弹槽。");
            }

            // 下一轮从“输家”下家开始（命中与否统一处理）
            State.LastClaim = null;
            State.Turn = NextAlive(loserIndex);
            State.Phase = Phase.Claim;
        }

        // —— 辅助：回合推进与工具 —— 
        // 获取当前玩家。
        Player CurrentPlayer() => State.Players[State.Turn];

        // 将回合推进到下一个存活玩家。
        void AdvanceTurnToNextAlive() => State.Turn = NextAlive(State.Turn);

        // 从指定索引开始找到下一个存活玩家。
        int NextAlive(int start)
        {
            if (State.AliveCount() <= 1) return start;
            int i = start;
            do { i = (i + 1) % State.Players.Count; }
            while (!State.Players[i].Alive);
            return i;
        }

        // 检查游戏是否结束。
        bool IsGameOver()
        {
            if (State.AliveCount() <= 1) return true;
            if (State.EveryoneEmptyHand()) return true; // 便于首版收尾
            return false;
        }

        // 将消息记录到游戏日志中。
        void LogLine(string msg) => State.Log.AppendLine(msg); // 记录日志

        // 根据游戏配置构建卡牌堆。
        static List<Card> BuildDeck(GameConfig cfg)
        {
            var deck = new List<Card>();
            int copiesPerRank = cfg.PlayerCount * cfg.CopiesPerRankPerPlayer; // 每个点数的卡片数量
            int id = 1;
            foreach (var r in new[] { Rank.Q, Rank.K, Rank.A })
            {
                for (int i = 0; i < copiesPerRank; i++)
                {
                    deck.Add(new Card($"{r}{id++}", r));
                }
            }
            return deck;
        }

        // 使用 Fisher-Yates 算法打乱列表。
        static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // 以轮流方式将卡牌分发给玩家。
        static void DealRoundRobin(List<Card> deck, List<Player> players)
        {
            int i = 0;
            foreach (var c in deck)
            {
                players[i % players.Count].Hand.Add(c);
                i++;
            }
        }

        // 从玩家手牌中移除指定 ID 的卡牌。
        static Card PopCardFromHand(Player p, string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            int idx = p.Hand.FindIndex(c => c.Id == cardId);
            if (idx < 0) return null;
            var card = p.Hand[idx];
            p.Hand.RemoveAt(idx);
            return card;
        }

        // 从玩家手牌中随机移除一张卡牌。
        static Card PopRandomCard(Player p, Random rng)
        {
            int idx = rng.Next(p.Hand.Count);
            var card = p.Hand[idx];
            p.Hand.RemoveAt(idx);
            return card;
        }

        public void MyPlayFeedbacks(Player player )
        {
            feedbacks = player.Feedbacks;
            feedbacks?.PlayFeedbacks();
        }
    }

    // ====== 三种示例 AI（越简单越好，先把循环跑起来） ======

    // 随机型：声明随机出一张；质疑有 30% 概率
    public class RandomBot : IAgent
    {
        public string Name { get; }
        public RandomBot(string name) { Name = name; }
        public string ChooseClaimCard(GameState s, int playerIndex, Random rng)
        {
            var hand = s.Players[playerIndex].Hand;
            if (hand.Count == 0) return null;
            return hand[rng.Next(hand.Count)].Id;
        }
        public bool DecideChallenge(GameState s, int responderIndex, Random rng)
            => rng.NextDouble() < 0.3;
    }


}
