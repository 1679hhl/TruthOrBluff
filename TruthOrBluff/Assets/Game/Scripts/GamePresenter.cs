using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace LiarsBar
{
    /// <summary>
    /// 游戏展示器：连接游戏逻辑层(GameEngine)和表现层(UI)
    /// 监听游戏事件并更新 UI 显示
    /// </summary>
    public class GamePresenter : MonoBehaviour
    {
        [Header("UI 引用")]
        public TextMeshProUGUI GameStatusText;
        public TextMeshProUGUI TableRankText;
        public TextMeshProUGUI ChamberCountText;
        public Transform PlayerViewContainer;
        public GameObject PlayerViewPrefab;
        
        [Header("消息面板")]
        public TextMeshProUGUI MessageText;
        public float MessageDisplayTime = 3f;
        
        [Header("调试")]
        public bool ShowDebugLogs = true;

        private GameEngine engine;
        private List<PlayerView> playerViews = new List<PlayerView>();
        private float messageTimer;

        void Awake()
        {
            engine = GameEngine.Instance;
        }

        void OnEnable()
        {
            // 订阅所有游戏事件
            engine.Events.OnGameInitialized += OnGameInitialized;
            engine.Events.OnCardPlayed += OnCardPlayed;
            engine.Events.OnClaimAccepted += OnClaimAccepted;
            engine.Events.OnChallenge += OnChallenge;
            engine.Events.OnPunishmentTrack += OnPunishmentTrack;
            engine.Events.OnPlayerEliminated += OnPlayerEliminated;
            engine.Events.OnGameOver += OnGameOver;
            engine.Events.OnTurnChanged += OnTurnChanged;
        }

        void OnDisable()
        {
            // 取消订阅
            engine.Events.OnGameInitialized -= OnGameInitialized;
            engine.Events.OnCardPlayed -= OnCardPlayed;
            engine.Events.OnClaimAccepted -= OnClaimAccepted;
            engine.Events.OnChallenge -= OnChallenge;
            engine.Events.OnPunishmentTrack -= OnPunishmentTrack;
            engine.Events.OnPlayerEliminated -= OnPlayerEliminated;
            engine.Events.OnGameOver -= OnGameOver;
            engine.Events.OnTurnChanged -= OnTurnChanged;
        }

        void Update()
        {
            // 消息自动隐藏
            if (messageTimer > 0)
            {
                messageTimer -= Time.deltaTime;
                if (messageTimer <= 0 && MessageText != null)
                    MessageText.text = "";
            }
        }

        // ========== 事件处理 ==========

        void OnGameInitialized(GameInitializedEvent e)
        {
            Log($"游戏初始化：{e.Players.Length}名玩家，桌面牌面 {e.TableRank}");
            
            // 创建玩家视图
            foreach (var player in e.Players)
            {
                var viewObj = Instantiate(PlayerViewPrefab, PlayerViewContainer);
                var view = viewObj.GetComponent<PlayerView>();
                view.PlayerIndex = player.Index;
                view.UpdateDisplay(player, false);
                playerViews.Add(view);
            }
            
            // 更新UI
            if (TableRankText != null)
                TableRankText.text = $"桌面牌面: {e.TableRank}";
                
            UpdateGameStatus("游戏开始");
            UpdateChamberDisplay();
        }

        void OnCardPlayed(CardPlayedEvent e)
        {
            Log($"{e.PlayerName} 打出一张牌，声称是 {e.DeclaredRank}");
            ShowMessage($"{e.PlayerName} 声称打出 {e.DeclaredRank}");
            
            // 播放动画
            var view = GetPlayerView(e.PlayerIndex);
            view?.PlayCardAnimation();
            
            // 更新手牌数
            var playerData = engine.State.Players[e.PlayerIndex];
            view?.UpdateDisplay(playerData, true);
        }

        void OnClaimAccepted(ClaimAcceptedEvent e)
        {
            Log($"{e.ResponderName} 接受了声明");
            ShowMessage($"{e.ResponderName} 接受");
        }

        void OnChallenge(ChallengeEvent e)
        {
            string result = e.WasTruthful ? "说真话" : "说谎";
            Log($"{e.ChallengerName} 质疑 {e.ClaimantName}！翻开的牌是 {e.RevealedRank}（{result}）");
            ShowMessage($"质疑！实际是 {e.RevealedRank} - {e.ClaimantName} {result}！", 4f);
        }

        void OnPunishmentTrack(PunishmentTrackEvent e)
        {
            Log($"惩罚轨: {e.ChamberCount}/6 {(e.Hit ? "【命中！】" : "")}");
            UpdateChamberDisplay();
            
            if (e.Hit)
                ShowMessage("命中子弹！", 3f);
        }

        void OnPlayerEliminated(PlayerEliminatedEvent e)
        {
            Log($"{e.PlayerName} 被淘汰！剩余 {e.AliveCount} 人");
            ShowMessage($"{e.PlayerName} 被淘汰！", 3f);
            
            // 播放淘汰动画
            var view = GetPlayerView(e.PlayerIndex);
            view?.PlayEliminatedAnimation();
            
            // 更新显示
            var playerData = engine.State.Players[e.PlayerIndex];
            view?.UpdateDisplay(playerData, false);
        }

        void OnGameOver(GameOverEvent e)
        {
            if (e.IsDraw)
            {
                Log("游戏结束：平局");
                UpdateGameStatus("平局");
                ShowMessage("游戏结束 - 平局", 5f);
            }
            else
            {
                Log($"游戏结束：{e.WinnerName} 获胜！");
                UpdateGameStatus($"{e.WinnerName} 获胜！");
                ShowMessage($"{e.WinnerName} 获胜！", 5f);
                
                // 播放胜利动画
                var view = GetPlayerView(e.WinnerIndex.Value);
                view?.PlayWinAnimation();
            }
        }

        void OnTurnChanged(TurnChangedEvent e)
        {
            Log($"轮到 {e.CurrentPlayerName} ({e.Phase})");
            
            // 更新所有玩家视图的高亮状态
            foreach (var view in playerViews)
            {
                bool isActive = view.PlayerIndex == e.CurrentPlayerIndex;
                var playerData = engine.State.Players[view.PlayerIndex];
                view.UpdateDisplay(playerData, isActive);
            }
        }

        // ========== UI 更新辅助方法 ==========

        void UpdateGameStatus(string status)
        {
            if (GameStatusText != null)
                GameStatusText.text = status;
        }

        void UpdateChamberDisplay()
        {
            if (ChamberCountText != null)
            {
                int chamber = engine.State.ChamberCount;
                int slot = engine.State.BulletSlot;
                ChamberCountText.text = $"惩罚轨: {chamber}/6 (子弹在第{slot}格)";
            }
        }

        void ShowMessage(string msg, float duration = -1)
        {
            if (MessageText != null)
            {
                MessageText.text = msg;
                messageTimer = duration > 0 ? duration : MessageDisplayTime;
            }
        }

        PlayerView GetPlayerView(int index)
        {
            return playerViews.FirstOrDefault(v => v.PlayerIndex == index);
        }

        void Log(string msg)
        {
            if (ShowDebugLogs)
                Debug.Log($"[GamePresenter] {msg}");
        }

        // ========== 公开方法 ==========

        /// <summary>手动刷新所有玩家视图</summary>
        public void RefreshAllPlayers()
        {
            for (int i = 0; i < playerViews.Count; i++)
            {
                var playerData = engine.State.Players[i];
                bool isActive = i == engine.State.Turn;
                playerViews[i].UpdateDisplay(playerData, isActive);
            }
        }
    }
}
