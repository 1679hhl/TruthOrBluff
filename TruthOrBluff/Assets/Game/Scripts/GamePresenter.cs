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
        
        [Header("3D玩家生成")]
        public PlayerSpawnManager SpawnManager; // 玩家生成管理器
        
        [Header("消息面板")]
        public TextMeshProUGUI MessageText;
        public float MessageDisplayTime = 3f;
        
        [Header("调试")]
        public bool ShowDebugLogs = true;

        private GameEngine engine;
        private List<PlayerController> playerControllers = new List<PlayerController>();
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
            
            // 使用 SpawnManager 动态生成玩家
            if (SpawnManager == null)
            {
                Debug.LogError("未设置 PlayerSpawnManager！无法生成玩家。");
                return;
            }
            
            playerControllers = SpawnManager.SpawnPlayers(e.Players);
            Log($"已生成 {playerControllers.Count} 名玩家");
            
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
            var controller = GetPlayerController(e.PlayerIndex);
            controller?.PlayCardAnimation();
            
            // 更新UI显示
            controller?.UpdateUI(true);
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
            var controller = GetPlayerController(e.PlayerIndex);
            controller?.PlayEliminatedAnimation();
            
            // 更新显示
            controller?.UpdateUI(false);
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
                var controller = GetPlayerController(e.WinnerIndex.Value);
                controller?.PlayWinAnimation();
            }
        }

        void OnTurnChanged(TurnChangedEvent e)
        {
            Log($"轮到 {e.CurrentPlayerName} ({e.Phase})");
            
            // 更新所有玩家的高亮状态
            foreach (var controller in playerControllers)
            {
                bool isActive = controller.PlayerIndex == e.CurrentPlayerIndex;
                controller.UpdateUI(isActive);
                
                // 当前回合玩家播放思考动画
                if (isActive && e.Phase == Phase.Claim)
                {
                    controller.PlayThinkAnimation();
                }
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

        PlayerController GetPlayerController(int index)
        {
            return playerControllers.FirstOrDefault(c => c.PlayerIndex == index);
        }

        void Log(string msg)
        {
            if (ShowDebugLogs)
                Debug.Log($"[GamePresenter] {msg}");
        }

        // ========== 公开方法 ==========

        /// <summary>手动刷新所有玩家显示</summary>
        public void RefreshAllPlayers()
        {
            foreach (var controller in playerControllers)
            {
                bool isActive = controller.PlayerIndex == engine.State.Turn;
                controller.UpdateUI(isActive);
            }
        }
        
        /// <summary>清除所有玩家（重新开始游戏时调用）</summary>
        public void ClearAllPlayers()
        {
            if (SpawnManager != null)
                SpawnManager.ClearPlayers();
            playerControllers.Clear();
        }
    }
}
