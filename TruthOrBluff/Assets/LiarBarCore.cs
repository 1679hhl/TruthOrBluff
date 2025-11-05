using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreMountains.Feedbacks;

namespace LiarsBar
{
    public class Card
    {
        public string Id;
        public Rank Rank;
        public Card(string id, Rank rank) { Id = id; Rank = rank; }
        public override string ToString() => $"{Rank}-{Id}";
    }

    public enum Phase { Claim, Response }

    public class LastClaim
    {
        public int PlayerIndex;
        public string CardId;
        public Rank DeclaredRank;
    }

    public class GameState
    {
        public List<Player> Players = new List<Player>();
        public int Turn = 0;
        public Phase Phase = Phase.Claim;
        public Rank TableRank;
        public List<Card> Pile = new List<Card>();
        public LastClaim LastClaim = null;
        public int BulletSlot = 4;
        public int ChamberCount = 0;
        public int Step = 0;
        public StringBuilder Log = new StringBuilder();

        public int AliveCount() => Players.Count(p => p.Alive);
        public bool EveryoneEmptyHand() => Players.Where(p => p.Alive).All(p => p.Hand.Count == 0);
    }

    public interface IAgent
    {
        string Name { get; }
        string ChooseClaimCard(GameState s, int playerIndex, Random rng);
        bool DecideChallenge(GameState s, int responderIndex, Random rng);
    }

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
                        _instance ??= new T();
                    }
                }
                return _instance;
            }
        }

        public static void SetInstance(T instance)
        {
            lock (_lock) { _instance = instance; }
        }

        public static void ClearInstance()
        {
            lock (_lock) { _instance = null; }
        }
    }

    public class GameEngine : Singleton<GameEngine>
    {
        public GameConfig Config { get; private set; }
        public GameState State { get; private set; } = new GameState();
        IAgent[] agents;
        Random rng;

        public GameEngine() { }

        public void Initialize(GameConfig config, IAgent[] agents)
        {
            if (agents?.Length != config.PlayerCount)
                throw new ArgumentException($"agents 数量必须等于 PlayerCount");
            
            Config = config;
            this.agents = agents;
            rng = new Random(config.Seed);
            State = new GameState
            {
                TableRank = config.TableRank,
                BulletSlot = Math.Clamp(config.BulletSlot, 1, 6)
            };

            for (int i = 0; i < config.PlayerCount; i++)
                State.Players.Add(new Player { Index = i, Name = agents[i].Name });

            var deck = BuildDeck(config);
            Shuffle(deck, rng);
            DealRoundRobin(deck, State.Players);

            LogLine($"== 开局：玩家 {string.Join("，", State.Players)}；桌面牌面 {State.TableRank}；子弹槽在第 {State.BulletSlot} 格 ==");
            foreach (var p in State.Players)
                LogLine($"{p} 手牌：{string.Join(",", p.Hand.Select(c => c.Rank))}");
        }

        public string RunUntilGameOver(int maxSteps = 500)
        {
            while (!IsGameOver() && State.Step < maxSteps)
                StepOnce();

            if (IsGameOver())
            {
                var alive = State.Players.Where(p => p.Alive).ToList();
                LogLine(alive.Count == 1 ? $"== 胜者：{alive[0]} ==" : "== 牌用尽/步数上限，和局 ==");
            }
            else
            {
                LogLine("== 达到步数上限，强制结束 ==");
            }
            return State.Log.ToString();
        }

        public void StepOnce()
        {
            if (IsGameOver()) return;
            State.Step++;

            if (State.Phase == Phase.Claim)
                ProcessClaimPhase();
            else
                ProcessResponsePhase();
        }

        void ProcessClaimPhase()
        {
            var me = CurrentPlayer();
            if (!me.Alive) { AdvanceTurnToNextAlive(); return; }

            if (me.Hand.Count == 0)
            {
                LogLine($"{me} 没牌可出，跳过。");
                AdvanceTurnToNextAlive();
                return;
            }

            var card = ChooseAndPlayCard(me);
            State.LastClaim = new LastClaim
            {
                PlayerIndex = me.Index,
                CardId = card.Id,
                DeclaredRank = State.TableRank
            };
            LogLine($"[声明] {me} 面朝下打出一张牌，声明为 {State.TableRank}");
            State.Phase = Phase.Response;
        }

        Card ChooseAndPlayCard(Player player)
        {
            var chooseId = agents[player.Index].ChooseClaimCard(State, player.Index, rng);
            var card = PopCardFromHand(player, chooseId) ?? PopRandomCard(player, rng);
            State.Pile.Add(card);
            player.Feedbacks?.PlayFeedbacks();
            return card;
        }

        void ProcessResponsePhase()
        {
            int responder = NextAlive(State.Turn);
            
            if (State.LastClaim == null)
            {
                LogLine("（无可响应的声明，自动进入下一位声明）");
                AdvanceTurnToNextAlive();
                State.Phase = Phase.Claim;
                return;
            }

            if (agents[responder].DecideChallenge(State, responder, rng))
                ResolveChallenge(responder);
            else
            {
                LogLine($"[接受] {State.Players[responder]} 接受声明，不翻牌。");
                State.LastClaim = null;
                State.Turn = responder;
                State.Phase = Phase.Claim;
            }
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

        Player CurrentPlayer() => State.Players[State.Turn];
        void AdvanceTurnToNextAlive() => State.Turn = NextAlive(State.Turn);
        
        int NextAlive(int start)
        {
            if (State.AliveCount() <= 1) return start;
            int i = start;
            do { i = (i + 1) % State.Players.Count; }
            while (!State.Players[i].Alive);
            return i;
        }

        bool IsGameOver() => State.AliveCount() <= 1 || State.EveryoneEmptyHand();
        void LogLine(string msg) => State.Log.AppendLine(msg);

        static List<Card> BuildDeck(GameConfig cfg)
        {
            var deck = new List<Card>();
            int copiesPerRank = cfg.PlayerCount * cfg.CopiesPerRankPerPlayer;
            int id = 1;
            foreach (var r in new[] { Rank.Q, Rank.K, Rank.A })
                for (int i = 0; i < copiesPerRank; i++)
                    deck.Add(new Card($"{r}{id++}", r));
            return deck;
        }

        static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static void DealRoundRobin(List<Card> deck, List<Player> players)
        {
            for (int i = 0; i < deck.Count; i++)
                players[i % players.Count].Hand.Add(deck[i]);
        }

        static Card PopCardFromHand(Player p, string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            int idx = p.Hand.FindIndex(c => c.Id == cardId);
            if (idx < 0) return null;
            var card = p.Hand[idx];
            p.Hand.RemoveAt(idx);
            return card;
        }

        static Card PopRandomCard(Player p, Random rng)
        {
            int idx = rng.Next(p.Hand.Count);
            var card = p.Hand[idx];
            p.Hand.RemoveAt(idx);
            return card;
        }
    }

    public class RandomBot : IAgent
    {
        public string Name { get; }
        public RandomBot(string name) => Name = name;
        
        public string ChooseClaimCard(GameState s, int playerIndex, Random rng)
        {
            var hand = s.Players[playerIndex].Hand;
            return hand.Count == 0 ? null : hand[rng.Next(hand.Count)].Id;
        }
        
        public bool DecideChallenge(GameState s, int responderIndex, Random rng) 
            => rng.NextDouble() < 0.3;
    }
}
