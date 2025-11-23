using UnityEngine;

namespace LiarsBar
{
    /// <summary>
    /// 游戏运行器：简化版本，负责初始化游戏并提供控制接口
    /// </summary>
    public class GameRunner : MonoBehaviour
    {
        [Header("游戏配置")]
        public int playerCount = 4;
        public Rank tableRank = Rank.Q;
        [Range(1, 6)] public int bulletSlot = 4;
        public int copiesPerRankPerPlayer = 2;
        
        [Header("随机种子")]
        [Tooltip("勾选后每次游戏使用随机种子，否则使用固定种子")]
        public bool useRandomSeed = true;
        [Tooltip("固定种子值（仅在useRandomSeed为false时使用）")]
        public int fixedSeed = 12345;

        [Header("玩家配置")]
        [Tooltip("玩家0是否为人类玩家")]
        public bool player0IsHuman = true;

        [Header("AI 配置")]
        [Tooltip("0=随机, 1=谨慎, 2=莽夫, -1=人类玩家")]
        public int[] botTypes = new int[] { -1, 1, 2, 0 }; // 示例：人类、谨慎、莽夫、随机

        [Header("自动运行")]
        public bool autoPlay = false;
        public float stepInterval = 1f; // 自动步进间隔（秒）

        private GameEngine engine;
        private GamePresenter presenter;
        private PlayerInputManager inputManager;
        private float autoPlayTimer;
        private IAgent[] agents;
        private bool waitingForHumanInput = false;

        void Start()
        {
            InitializeGame();
            inputManager = FindFirstObjectByType<PlayerInputManager>();
            
            if (inputManager != null)
            {
                inputManager.SetCardSelectionCallback(OnHumanCardSelection);
                inputManager.SetChallengeDecisionCallback(OnHumanChallengeDecision);
            }
        }

        void Update()
        {
            // 检查是否需要等待人类输入
            if (!waitingForHumanInput && autoPlay && !engine.IsGameOver())
            {
                autoPlayTimer += Time.deltaTime;
                if (autoPlayTimer >= stepInterval)
                {
                    autoPlayTimer = 0;
                    StepGame();
                }
            }
        }

        /// <summary>初始化游戏</summary>
        public void InitializeGame()
        {
            // 生成种子：使用随机种子或固定种子
            int gameSeed = useRandomSeed ? System.Environment.TickCount : fixedSeed;
            
            // 创建配置
            var config = new GameConfig
            {
                PlayerCount = playerCount,
                TableRank = tableRank,
                BulletSlot = bulletSlot,
                CopiesPerRankPerPlayer = copiesPerRankPerPlayer,
                Seed = gameSeed
            };

            // 创建 AI 代理
            agents = new IAgent[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                int botType = i < botTypes.Length ? botTypes[i] : 0;
                agents[i] = CreateAgent(botType, $"玩家{i + 1}", i);
            }

            // 初始化引擎
            engine = GameEngine.Instance;
            engine.Initialize(config, agents);

            // 查找展示器
            presenter = FindFirstObjectByType<GamePresenter>();
            if (presenter == null)
                Debug.LogWarning("未找到 GamePresenter！UI 将不会更新。");

            Debug.Log($"游戏初始化完成（种子: {gameSeed}）。按 Space 步进，或启用 autoPlay。");
        }

        /// <summary>执行一步游戏</summary>
        public void StepGame()
        {
            if (engine != null && !engine.IsGameOver())
            {
                // 检查当前玩家是否为人类且需要输入
                int currentPlayer = engine.State.Turn;
                var agent = agents[currentPlayer];
                
                if (agent is HumanPlayer humanPlayer)
                {
                    // 检查是哪个阶段
                    if (engine.State.Phase == Phase.Claim)
                    {
                        // 需要选择卡牌
                        if (humanPlayer.SelectedCardIds == null || humanPlayer.SelectedCardIds.Count == 0)
                        {
                            // 请求输入
                            if (inputManager != null && !waitingForHumanInput)
                            {
                                waitingForHumanInput = true;
                                inputManager.RequestCardSelection(
                                    engine.State.Players[currentPlayer].Hand,
                                    1,
                                    3
                                );
                                Debug.Log($"[GameRunner] 等待 {humanPlayer.Name} 选择卡牌...");
                            }
                            return; // 暂停步进，等待输入
                        }
                    }
                    else if (engine.State.Phase == Phase.Response)
                    {
                        // 需要决定是否质疑
                        int responder = engine.State.Players.FindIndex(p => 
                            p.Alive && p.Index != engine.State.LastClaim.PlayerIndex);
                        
                        if (responder == currentPlayer && !waitingForHumanInput)
                        {
                            waitingForHumanInput = true;
                            if (inputManager != null)
                            {
                                inputManager.RequestChallengeDecision();
                                Debug.Log($"[GameRunner] 等待 {humanPlayer.Name} 决定是否质疑...");
                            }
                            return; // 暂停步进，等待输入
                        }
                    }
                }
                
                engine.StepOnce();
            }
            else if (engine != null && engine.IsGameOver())
            {
                Debug.Log("游戏已结束");
            }
        }

        /// <summary>运行直到游戏结束</summary>
        public void RunToEnd()
        {
            if (engine != null)
            {
                engine.RunUntilGameOver();
                Debug.Log("游戏已完成");
            }
        }

        /// <summary>重新开始游戏</summary>
        public void RestartGame()
        {
            // 清除旧玩家
            if (presenter != null)
                presenter.ClearAllPlayers();
            
            // 清除旧实例
            GameEngine.ClearInstance();
            
            // 重新初始化
            InitializeGame();
        }

        // ========== 辅助方法 ==========

        IAgent CreateAgent(int type, string name, int playerIndex)
        {
            // -1 表示人类玩家
            if (type == -1 || (playerIndex == 0 && player0IsHuman))
            {
                return new HumanPlayer(name);
            }
            
            return type switch
            {
                1 => new CautiousBot(name),
                2 => new RecklessBot(name),
                _ => new RandomBot(name)
            };
        }

        // ========== 人类玩家输入回调 ==========

        void OnHumanCardSelection(System.Collections.Generic.List<string> cardIds)
        {
            // 找到当前的人类玩家
            int currentPlayer = engine.State.Turn;
            if (agents[currentPlayer] is HumanPlayer humanPlayer)
            {
                humanPlayer.SelectedCardIds = cardIds;
                waitingForHumanInput = false;
                Debug.Log($"[GameRunner] {humanPlayer.Name} 选择了 {cardIds.Count} 张牌");
                
                // 继续游戏
                StepGame();
            }
        }

        void OnHumanChallengeDecision(bool challenge)
        {
            // 找到当前的响应玩家
            int nextPlayer = (engine.State.Turn + 1) % engine.State.Players.Count;
            while (!engine.State.Players[nextPlayer].Alive)
            {
                nextPlayer = (nextPlayer + 1) % engine.State.Players.Count;
            }
            
            if (agents[nextPlayer] is HumanPlayer humanPlayer)
            {
                humanPlayer.ChallengeDecision = challenge;
                waitingForHumanInput = false;
                Debug.Log($"[GameRunner] {humanPlayer.Name} 决定{(challenge ? "质疑" : "不质疑")}");
                
                // 继续游戏
                StepGame();
            }
        }

        // ========== Unity Editor 按钮 ==========

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 200, 200));
            
            if (GUILayout.Button("步进 (Space)"))
                StepGame();
                
            if (GUILayout.Button("运行到底"))
                RunToEnd();
                
            if (GUILayout.Button("重新开始"))
                RestartGame();
                
            autoPlay = GUILayout.Toggle(autoPlay, "自动播放");
            
            if (engine != null)
            {
                GUILayout.Label($"步数: {engine.State.Step}");
                GUILayout.Label($"存活: {engine.State.AliveCount()}/{playerCount}");
            }
            
            GUILayout.EndArea();
        }

        void OnValidate()
        {
            // 确保 botTypes 数组长度正确
            if (botTypes.Length != playerCount)
            {
                System.Array.Resize(ref botTypes, playerCount);
            }
        }
    }
}
