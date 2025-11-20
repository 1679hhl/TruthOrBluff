using System;
using System.Collections.Generic;
using System.Linq;

namespace LiarsBar
{
    // ========== 核心数据类 ==========
    
    /// <summary>卡牌数据</summary>
    public class Card
    {
        public string Id;
        public Rank Rank;
        public Card(string id, Rank rank) { Id = id; Rank = rank; }
        public override string ToString() => $"{Rank}-{Id}";
    }

    /// <summary>玩家数据（纯数据，无 Unity 依赖）</summary>
    public class PlayerData
    {
        public int Index;
        public string Name;
        public List<Card> Hand = new List<Card>();
        public bool Alive = true;
        public override string ToString() => $"P{Index}({Name})";
    }

    /// <summary>游戏阶段</summary>
    public enum Phase { Claim, Response }

    /// <summary>上一次声明记录</summary>
    public class LastClaim
    {
        public int PlayerIndex;
        public List<string> CardIds; // 支持多张牌
        public Rank DeclaredRank;
        public int CardCount => CardIds?.Count ?? 0;
    }

    /// <summary>游戏状态</summary>
    public class GameState
    {
        public List<PlayerData> Players = new List<PlayerData>();
        public int Turn = 0;
        public Phase Phase = Phase.Claim;
        public Rank TableRank;
        public List<Card> Pile = new List<Card>();
        public LastClaim LastClaim = null;
        public int BulletSlot = 4;
        public int ChamberCount = 0;
        public int Step = 0;

        public int AliveCount() => Players.Count(p => p.Alive);
        public bool EveryoneEmptyHand() => Players.Where(p => p.Alive).All(p => p.Hand.Count == 0);
    }

    // ========== AI 接口 ==========
    
    /// <summary>AI 代理接口</summary>
    public interface IAgent
    {
        string Name { get; }
        List<string> ChooseClaimCards(GameState s, int playerIndex, Random rng); // 返回1-3张牌的ID列表
        bool DecideChallenge(GameState s, int responderIndex, Random rng);
    }

    // ========== 单例基类 ==========
    
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

        public static void ClearInstance()
        {
            lock (_lock) { _instance = null; }
        }
    }

    // ========== 游戏引擎 ==========
    
    /// <summary>游戏引擎核心逻辑（纯逻辑层，通过事件与表现层通信）</summary>
    public class GameEngine : Singleton<GameEngine>
    {
        public GameConfig Config { get; private set; }
        public GameState State { get; private set; } = new GameState();
        public GameEventManager Events { get; private set; } = new GameEventManager();
        
        IAgent[] agents;
        Random rng;

        public GameEngine() { }

        /// <summary>初始化游戏</summary>
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
                BulletSlot = rng.Next(1, 7) // 随机生成1-6之间的子弹槽位
            };

            for (int i = 0; i < config.PlayerCount; i++)
                State.Players.Add(new PlayerData { Index = i, Name = agents[i].Name });

            var deck = BuildDeck(config);
            Shuffle(deck, rng);
            DealRoundRobin(deck, State.Players);

            Events.TriggerGameInitialized(new GameInitializedEvent
            {
                Players = State.Players.ToArray(),
                TableRank = State.TableRank,
                BulletSlot = State.BulletSlot
            });
        }

        /// <summary>运行游戏直到结束</summary>
        public void RunUntilGameOver()
        {
            while (!IsGameOver())
                StepOnce();

            if (IsGameOver())
            {
                var alive = State.Players.Where(p => p.Alive).ToList();
                Events.TriggerGameOver(new GameOverEvent
                {
                    WinnerIndex = alive.Count == 1 ? alive[0].Index : (int?)null,
                    WinnerName = alive.Count == 1 ? alive[0].Name : null,
                    IsDraw = alive.Count != 1
                });
            }
        }

        /// <summary>执行一步游戏逻辑</summary>
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

            // 检查当前玩家是否有牌
            if (me.Hand.Count == 0)
            {
                // 检查是否所有存活玩家都没有牌了
                bool allPlayersEmpty = State.Players.Where(p => p.Alive).All(p => p.Hand.Count == 0);
                if (allPlayersEmpty)
                {
                    var alivePlayers = State.Players.Where(p => p.Alive).ToList();
                    
                    // 如果只剩一个玩家，游戏结束
                    if (alivePlayers.Count <= 1)
                    {
                        Events.TriggerGameOver(new GameOverEvent
                        {
                            WinnerIndex = alivePlayers.Count == 1 ? alivePlayers[0].Index : (int?)null,
                            WinnerName = alivePlayers.Count == 1 ? alivePlayers[0].Name : null,
                            IsDraw = alivePlayers.Count != 1
                        });
                        return;
                    }
                    
                    // 如果有多名玩家存活，重新发牌继续游戏
                    RedealCards();
                    return;
                }
                
                // 否则跳过该玩家
                AdvanceTurnToNextAlive();
                return;
            }

            var cards = ChooseAndPlayCards(me);
            State.LastClaim = new LastClaim
            {
                PlayerIndex = me.Index,
                CardIds = cards.Select(c => c.Id).ToList(),
                DeclaredRank = State.TableRank
            };
            
            Events.TriggerCardPlayed(new CardPlayedEvent
            {
                PlayerIndex = me.Index,
                PlayerName = me.Name,
                DeclaredRank = State.TableRank,
                PlayedCardCount = cards.Count,
                RemainingCards = me.Hand.Count
            });
            
            // 切换到响应阶段，并通知下家
            int nextPlayer = NextAlive(State.Turn);
            State.Phase = Phase.Response;
            
            Events.TriggerTurnChanged(new TurnChangedEvent
            {
                CurrentPlayerIndex = nextPlayer,
                CurrentPlayerName = State.Players[nextPlayer].Name,
                Phase = Phase.Response
            });
        }

        List<Card> ChooseAndPlayCards(PlayerData player)
        {
            var chooseIds = agents[player.Index].ChooseClaimCards(State, player.Index, rng);
            var cards = new List<Card>();
            
            // 确保返回1-3张牌
            if (chooseIds == null || chooseIds.Count == 0 || chooseIds.Count > 3)
            {
                // 默认出1张随机牌
                cards.Add(PopRandomCard(player, rng));
            }
            else
            {
                foreach (var id in chooseIds)
                {
                    var card = PopCardFromHand(player, id);
                    if (card != null)
                        cards.Add(card);
                }
                
                // 如果没有成功获取任何牌，随机出一张
                if (cards.Count == 0)
                    cards.Add(PopRandomCard(player, rng));
            }
            
            // 将牌加入牌堆
            State.Pile.AddRange(cards);
            return cards;
        }

        /// <summary>重新发牌给存活玩家</summary>
        void RedealCards()
        {
            // 清空牌堆
            State.Pile.Clear();
            State.LastClaim = null;
            
            // 获取存活玩家
            var alivePlayers = State.Players.Where(p => p.Alive).ToList();
            
            // 重新构建牌组（只给存活玩家发牌）
            var deck = new List<Card>();
            int copiesPerRank = alivePlayers.Count * Config.CopiesPerRankPerPlayer;
            int id = State.Step * 1000; // 使用步数作为ID前缀避免重复
            foreach (var r in new[] { Rank.Q, Rank.K, Rank.A })
                for (int i = 0; i < copiesPerRank; i++)
                    deck.Add(new Card($"{r}{id++}", r));
            
            // 洗牌
            Shuffle(deck, rng);
            
            // 发牌给存活玩家
            for (int i = 0; i < deck.Count; i++)
                alivePlayers[i % alivePlayers.Count].Hand.Add(deck[i]);
            
            // 重置惩罚轨和子弹槽位
            State.ChamberCount = 0;
            State.BulletSlot = rng.Next(1, 7);
            
            // 重置回合到第一个存活玩家
            State.Turn = State.Players.FindIndex(p => p.Alive);
            State.Phase = Phase.Claim;
            
            // 触发事件通知UI更新
            Events.TriggerTurnChanged(new TurnChangedEvent
            {
                CurrentPlayerIndex = State.Turn,
                CurrentPlayerName = State.Players[State.Turn].Name,
                Phase = Phase.Claim
            });
        }

        void ProcessResponsePhase()
        {
            int responder = NextAlive(State.Turn);
            
            if (State.LastClaim == null)
            {
                AdvanceTurnToNextAlive();
                State.Phase = Phase.Claim;
                return;
            }

            // 特殊情况：只剩两名玩家且出牌方手牌为空，强制质疑
            var claimant = State.Players[State.LastClaim.PlayerIndex];
            bool isFinalShowdown = State.AliveCount() == 2 && claimant.Hand.Count == 0;
            
            // 下家必须选择：质疑 或 继续出牌（移除同意功能）
            if (isFinalShowdown || agents[responder].DecideChallenge(State, responder, rng))
            {
                // 选择质疑
                ResolveChallenge(responder, isFinalShowdown);
            }
            else
            {
                // 选择不质疑，直接进入出牌阶段（移除同意事件）
                State.LastClaim = null;
                State.Turn = responder;
                State.Phase = Phase.Claim;
                
                Events.TriggerTurnChanged(new TurnChangedEvent
                {
                    CurrentPlayerIndex = responder,
                    CurrentPlayerName = State.Players[responder].Name,
                    Phase = Phase.Claim
                });
            }
        }

        void ResolveChallenge(int responderIndex, bool isFinalShowdown = false)
        {
            var claimantIndex = State.LastClaim.PlayerIndex;
            var claimant = State.Players[claimantIndex];
            var responder = State.Players[responderIndex];
            
            // 获取最后出的几张牌
            var claimedCardCount = State.LastClaim.CardCount;
            var lastCards = State.Pile.Skip(State.Pile.Count - claimedCardCount).ToList();
            
            // 只有所有牌都为真才算真话
            bool truthful = lastCards.All(card => card.Rank == State.TableRank);

            Events.TriggerChallenge(new ChallengeEvent
            {
                ChallengerIndex = responderIndex,
                ChallengerName = responder.Name,
                ClaimantIndex = claimantIndex,
                ClaimantName = claimant.Name,
                RevealedRank = lastCards.Count > 0 ? lastCards[0].Rank : State.TableRank,
                RevealedCardCount = lastCards.Count,
                WasTruthful = truthful
            });

            int loserIndex = truthful ? responderIndex : claimantIndex;
            var loser = State.Players[loserIndex];

            // 如果是两人局最后决斗，失败者直接死亡
            if (isFinalShowdown)
            {
                loser.Alive = false;
                State.ChamberCount = 0;
                State.BulletSlot = rng.Next(1, 7); // 重置时随机生成新的子弹槽位
                
                Events.TriggerPlayerEliminated(new PlayerEliminatedEvent
                {
                    PlayerIndex = loserIndex,
                    PlayerName = loser.Name,
                    AliveCount = State.AliveCount()
                });
            }
            else
            {
                // 正常质疑流程：推进惩罚轨
                State.ChamberCount++;
                bool hit = (State.ChamberCount == State.BulletSlot);

                Events.TriggerPunishmentTrack(new PunishmentTrackEvent
                {
                    ChamberCount = State.ChamberCount,
                    Hit = hit
                });

                if (hit)
                {
                    loser.Alive = false;
                    State.ChamberCount = 0;
                    State.BulletSlot = rng.Next(1, 7); // 重置时随机生成新的子弹槽位
                    
                    Events.TriggerPlayerEliminated(new PlayerEliminatedEvent
                    {
                        PlayerIndex = loserIndex,
                        PlayerName = loser.Name,
                        AliveCount = State.AliveCount()
                    });
                }
            }

            State.LastClaim = null;
            // 如果失败者还活着，由失败者继续出牌；否则跳到下一个存活玩家
            State.Turn = loser.Alive ? loserIndex : NextAlive(loserIndex);
            State.Phase = Phase.Claim;
            
            var nextPlayer = State.Players[State.Turn];
            Events.TriggerTurnChanged(new TurnChangedEvent
            {
                CurrentPlayerIndex = State.Turn,
                CurrentPlayerName = nextPlayer.Name,
                Phase = Phase.Claim
            });
        }

        PlayerData CurrentPlayer() => State.Players[State.Turn];
        void AdvanceTurnToNextAlive() => State.Turn = NextAlive(State.Turn);
        
        int NextAlive(int start)
        {
            if (State.AliveCount() <= 1) return start;
            int i = start;
            do { i = (i + 1) % State.Players.Count; }
            while (!State.Players[i].Alive);
            return i;
        }

        public bool IsGameOver() => State.AliveCount() <= 1;

        // ========== 工具方法 ==========
        
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

        static void DealRoundRobin(List<Card> deck, List<PlayerData> players)
        {
            for (int i = 0; i < deck.Count; i++)
                players[i % players.Count].Hand.Add(deck[i]);
        }

        static Card PopCardFromHand(PlayerData p, string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            int idx = p.Hand.FindIndex(c => c.Id == cardId);
            if (idx < 0) return null;
            var card = p.Hand[idx];
            p.Hand.RemoveAt(idx);
            return card;
        }

        static Card PopRandomCard(PlayerData p, Random rng)
        {
            int idx = rng.Next(p.Hand.Count);
            var card = p.Hand[idx];
            p.Hand.RemoveAt(idx);
            return card;
        }
    }

    // ========== 内置 AI 实现 ==========
    
    /// <summary>随机 Bot</summary>
    public class RandomBot : IAgent
    {
        public string Name { get; }
        public RandomBot(string name) => Name = name;
        
        public List<string> ChooseClaimCards(GameState s, int playerIndex, Random rng)
        {
            var hand = s.Players[playerIndex].Hand;
            if (hand.Count == 0) return new List<string>();
            
            // 随机选择1-3张牌
            int cardCount = rng.Next(1, Math.Min(4, hand.Count + 1));
            var selectedCards = new List<string>();
            var availableCards = new List<Card>(hand);
            
            for (int i = 0; i < cardCount; i++)
            {
                var card = availableCards[rng.Next(availableCards.Count)];
                selectedCards.Add(card.Id);
                availableCards.Remove(card);
            }
            
            return selectedCards;
        }
        
        public bool DecideChallenge(GameState s, int responderIndex, Random rng) 
            => rng.NextDouble() < 0.3;
    }
}
