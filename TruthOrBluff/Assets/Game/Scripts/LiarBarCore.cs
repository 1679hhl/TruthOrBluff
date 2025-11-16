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
    public enum Phase { Claim, WaitForResponse, Response }

    /// <summary>上一次声明记录</summary>
    public class LastClaim
    {
        public int PlayerIndex;
        public string CardId;
        public Rank DeclaredRank;
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
        string ChooseClaimCard(GameState s, int playerIndex, Random rng);
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
                BulletSlot = Math.Clamp(config.BulletSlot, 1, 6)
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
        public void RunUntilGameOver(int maxSteps = 500)
        {
            while (!IsGameOver() && State.Step < maxSteps)
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
            else if (State.Phase == Phase.WaitForResponse)
                ProcessWaitPhase();
            else
                ProcessResponsePhase();
        }

        void ProcessClaimPhase()
        {
            var me = CurrentPlayer();
            if (!me.Alive) { AdvanceTurnToNextAlive(); return; }

            if (me.Hand.Count == 0)
            {
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
            
            Events.TriggerCardPlayed(new CardPlayedEvent
            {
                PlayerIndex = me.Index,
                PlayerName = me.Name,
                DeclaredRank = State.TableRank,
                RemainingCards = me.Hand.Count
            });
            
            // 切换到等待阶段，下一步才进入Response
            State.Phase = Phase.WaitForResponse;
        }

        Card ChooseAndPlayCard(PlayerData player)
        {
            var chooseId = agents[player.Index].ChooseClaimCard(State, player.Index, rng);
            var card = PopCardFromHand(player, chooseId) ?? PopRandomCard(player, rng);
            State.Pile.Add(card);
            return card;
        }

        void ProcessWaitPhase()
        {
            // 等待阶段：切换到下一个玩家，准备进入Response阶段
            int nextPlayer = NextAlive(State.Turn);
            var nextPlayerData = State.Players[nextPlayer];
            
            Events.TriggerTurnChanged(new TurnChangedEvent
            {
                CurrentPlayerIndex = nextPlayer,
                CurrentPlayerName = nextPlayerData.Name,
                Phase = Phase.WaitForResponse
            });
            
            // 切换到Response阶段，下一步将进行质疑判断
            State.Phase = Phase.Response;
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
            
            if (isFinalShowdown || agents[responder].DecideChallenge(State, responder, rng))
                ResolveChallenge(responder, isFinalShowdown);
            else
            {
                var responderData = State.Players[responder];
                Events.TriggerClaimAccepted(new ClaimAcceptedEvent
                {
                    ResponderIndex = responder,
                    ResponderName = responderData.Name
                });
                
                State.LastClaim = null;
                State.Turn = responder;
                State.Phase = Phase.Claim;
                
                Events.TriggerTurnChanged(new TurnChangedEvent
                {
                    CurrentPlayerIndex = responder,
                    CurrentPlayerName = responderData.Name,
                    Phase = Phase.Claim
                });
            }
        }

        void ResolveChallenge(int responderIndex, bool isFinalShowdown = false)
        {
            var claimantIndex = State.LastClaim.PlayerIndex;
            var claimant = State.Players[claimantIndex];
            var responder = State.Players[responderIndex];
            var lastCard = State.Pile.Last();
            bool truthful = (lastCard.Rank == State.TableRank);

            Events.TriggerChallenge(new ChallengeEvent
            {
                ChallengerIndex = responderIndex,
                ChallengerName = responder.Name,
                ClaimantIndex = claimantIndex,
                ClaimantName = claimant.Name,
                RevealedRank = lastCard.Rank,
                WasTruthful = truthful
            });

            int loserIndex = truthful ? responderIndex : claimantIndex;
            var loser = State.Players[loserIndex];

            // 如果是两人局最后决斗，失败者直接死亡
            if (isFinalShowdown)
            {
                loser.Alive = false;
                State.ChamberCount = 0;
                
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

        public bool IsGameOver() => State.AliveCount() <= 1 || State.EveryoneEmptyHand();

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
        
        public string ChooseClaimCard(GameState s, int playerIndex, Random rng)
        {
            var hand = s.Players[playerIndex].Hand;
            return hand.Count == 0 ? null : hand[rng.Next(hand.Count)].Id;
        }
        
        public bool DecideChallenge(GameState s, int responderIndex, Random rng) 
            => rng.NextDouble() < 0.3;
    }
}
