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
        public int seed = 12345;

        [Header("AI 配置")]
        [Tooltip("0=随机, 1=谨慎, 2=莽夫")]
        public int[] botTypes = new int[] { 1, 2, 0, 1 }; // 示例：谨慎、莽夫、随机、谨慎

        [Header("自动运行")]
        public bool autoPlay = false;
        public float stepInterval = 1f; // 自动步进间隔（秒）

        private GameEngine engine;
        private GamePresenter presenter;
        private float autoPlayTimer;

        void Start()
        {
            InitializeGame();
        }

        void Update()
        {
            if (autoPlay && !engine.IsGameOver())
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
            // 创建配置
            var config = new GameConfig
            {
                PlayerCount = playerCount,
                TableRank = tableRank,
                BulletSlot = bulletSlot,
                CopiesPerRankPerPlayer = copiesPerRankPerPlayer,
                Seed = seed
            };

            // 创建 AI 代理
            IAgent[] agents = new IAgent[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                int botType = i < botTypes.Length ? botTypes[i] : 0;
                agents[i] = CreateBot(botType, $"玩家{i + 1}");
            }

            // 初始化引擎
            engine = GameEngine.Instance;
            engine.Initialize(config, agents);

            // 查找展示器
            presenter = FindFirstObjectByType<GamePresenter>();
            if (presenter == null)
                Debug.LogWarning("未找到 GamePresenter！UI 将不会更新。");

            Debug.Log("游戏初始化完成。按 Space 步进，或启用 autoPlay。");
        }

        /// <summary>执行一步游戏</summary>
        public void StepGame()
        {
            if (engine != null && !engine.IsGameOver())
            {
                engine.StepOnce();
            }
            else if (engine.IsGameOver())
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

        IAgent CreateBot(int type, string name)
        {
            return type switch
            {
                1 => new CautiousBot(name),
                2 => new RecklessBot(name),
                _ => new RandomBot(name)
            };
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
