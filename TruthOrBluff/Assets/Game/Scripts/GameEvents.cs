using System;

namespace LiarsBar
{
    // ========== 游戏事件定义 ==========
    
    /// <summary>游戏初始化事件</summary>
    public class GameInitializedEvent
    {
        public PlayerData[] Players;
        public Rank TableRank;
        public int BulletSlot;
    }

    /// <summary>玩家打出卡牌事件</summary>
    public class CardPlayedEvent
    {
        public int PlayerIndex;
        public string PlayerName;
        public Rank DeclaredRank;
        public int RemainingCards;
    }

    /// <summary>玩家接受声明事件</summary>
    public class ClaimAcceptedEvent
    {
        public int ResponderIndex;
        public string ResponderName;
    }

    /// <summary>玩家质疑事件</summary>
    public class ChallengeEvent
    {
        public int ChallengerIndex;
        public string ChallengerName;
        public int ClaimantIndex;
        public string ClaimantName;
        public Rank RevealedRank;
        public bool WasTruthful;
    }

    /// <summary>惩罚轨推进事件</summary>
    public class PunishmentTrackEvent
    {
        public int ChamberCount;
        public bool Hit;
    }

    /// <summary>玩家淘汰事件</summary>
    public class PlayerEliminatedEvent
    {
        public int PlayerIndex;
        public string PlayerName;
        public int AliveCount;
    }

    /// <summary>游戏结束事件</summary>
    public class GameOverEvent
    {
        public int? WinnerIndex;
        public string WinnerName;
        public bool IsDraw;
    }

    /// <summary>回合切换事件</summary>
    public class TurnChangedEvent
    {
        public int CurrentPlayerIndex;
        public string CurrentPlayerName;
        public Phase Phase;
    }

    // ========== 事件管理器 ==========
    
    public class GameEventManager
    {
        public event Action<GameInitializedEvent> OnGameInitialized;
        public event Action<CardPlayedEvent> OnCardPlayed;
        public event Action<ClaimAcceptedEvent> OnClaimAccepted;
        public event Action<ChallengeEvent> OnChallenge;
        public event Action<PunishmentTrackEvent> OnPunishmentTrack;
        public event Action<PlayerEliminatedEvent> OnPlayerEliminated;
        public event Action<GameOverEvent> OnGameOver;
        public event Action<TurnChangedEvent> OnTurnChanged;

        public void TriggerGameInitialized(GameInitializedEvent e) => OnGameInitialized?.Invoke(e);
        public void TriggerCardPlayed(CardPlayedEvent e) => OnCardPlayed?.Invoke(e);
        public void TriggerClaimAccepted(ClaimAcceptedEvent e) => OnClaimAccepted?.Invoke(e);
        public void TriggerChallenge(ChallengeEvent e) => OnChallenge?.Invoke(e);
        public void TriggerPunishmentTrack(PunishmentTrackEvent e) => OnPunishmentTrack?.Invoke(e);
        public void TriggerPlayerEliminated(PlayerEliminatedEvent e) => OnPlayerEliminated?.Invoke(e);
        public void TriggerGameOver(GameOverEvent e) => OnGameOver?.Invoke(e);
        public void TriggerTurnChanged(TurnChangedEvent e) => OnTurnChanged?.Invoke(e);

        public void Clear()
        {
            OnGameInitialized = null;
            OnCardPlayed = null;
            OnClaimAccepted = null;
            OnChallenge = null;
            OnPunishmentTrack = null;
            OnPlayerEliminated = null;
            OnGameOver = null;
            OnTurnChanged = null;
        }
    }
}
