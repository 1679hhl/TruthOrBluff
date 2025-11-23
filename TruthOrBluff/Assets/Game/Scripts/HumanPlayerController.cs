using UnityEngine;

namespace LiarsBar
{
    /// <summary>
    /// 人类玩家游戏控制器：处理人类玩家的回合和输入等待
    /// </summary>
    public class HumanPlayerController : MonoBehaviour
    {
        private GameEngine engine;
        private PlayerInputManager inputManager;
        private HumanPlayer currentHumanPlayer;
        private bool waitingForInput = false;

        void Awake()
        {
            engine = GameEngine.Instance;
        }

        void Start()
        {
            inputManager = PlayerInputManager.Instance;
            
            if (inputManager != null)
            {
                // 注册输入回调
                inputManager.SetCardSelectionCallback(OnCardSelectionComplete);
                inputManager.SetChallengeDecisionCallback(OnChallengeDecisionComplete);
            }
        }

        /// <summary>检查当前是否需要等待人类玩家输入</summary>
        public bool IsWaitingForHumanInput()
        {
            if (engine == null || engine.State == null)
                return false;
            
            // 检查当前回合玩家是否为人类
            int currentPlayerIndex = engine.State.Turn;
            if (currentPlayerIndex < 0 || currentPlayerIndex >= engine.State.Players.Count)
                return false;
            
            // 这里需要通过GameRunner或其他方式获取Agent类型
            // 简化实现：通过标记判断
            return waitingForInput;
        }

        /// <summary>请求人类玩家选择卡牌</summary>
        public void RequestHumanCardSelection(HumanPlayer player, GameState state, int playerIndex)
        {
            currentHumanPlayer = player;
            waitingForInput = true;
            
            if (inputManager != null)
            {
                inputManager.RequestCardSelection(
                    state.Players[playerIndex].Hand,
                    1,
                    3
                );
            }
        }

        /// <summary>请求人类玩家决定是否质疑</summary>
        public void RequestHumanChallengeDecision(HumanPlayer player)
        {
            currentHumanPlayer = player;
            waitingForInput = true;
            
            if (inputManager != null)
            {
                inputManager.RequestChallengeDecision();
            }
        }

        void OnCardSelectionComplete(System.Collections.Generic.List<string> cardIds)
        {
            if (currentHumanPlayer != null)
            {
                currentHumanPlayer.SelectedCardIds = cardIds;
                waitingForInput = false;
                
                // 继续游戏步进
                var runner = FindFirstObjectByType<GameRunner>();
                if (runner != null)
                {
                    runner.StepGame();
                }
            }
        }

        void OnChallengeDecisionComplete(bool challenge)
        {
            if (currentHumanPlayer != null)
            {
                currentHumanPlayer.ChallengeDecision = challenge;
                waitingForInput = false;
                
                // 继续游戏步进
                var runner = FindFirstObjectByType<GameRunner>();
                if (runner != null)
                {
                    runner.StepGame();
                }
            }
        }
    }
}
