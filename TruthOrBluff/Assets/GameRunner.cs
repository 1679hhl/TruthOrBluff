using UnityEngine;
using LiarsBar;

public class GameRunner : MonoBehaviour
{
    [Header("配置（可在 Inspector 改）")]
    public int playerCount = 4;
    public LiarsBar.Rank tableRank = LiarsBar.Rank.Q;
    [Range(1,6)] public int bulletSlot = 4;
    public int copiesPerRankPerPlayer = 2;
    public int seed = 12345;

    private GameEngine engine;

    void Start()
    {
        var cfg = new GameConfig
        {
            PlayerCount = playerCount,
            TableRank = tableRank,
            BulletSlot = bulletSlot,
            CopiesPerRankPerPlayer = copiesPerRankPerPlayer,
            Seed = seed
        };

        // 准备机器人（你也可以混搭）
        IAgent[] agents = new IAgent[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            if (i % 3 == 0) agents[i] = new CautiousBot($"谨慎{i}");
            else if (i % 3 == 1) agents[i] = new RecklessBot($"莽夫{i}");
            else agents[i] = new RandomBot($"随机{i}");
        }

        // 使用单例模式并初始化
        engine = GameEngine.Instance;
        engine.Initialize(cfg, agents);
        Debug.Log("游戏初始化完成。调用 StepGame() 执行一步。");
    }

    public void StepGame()
    {
        if (engine != null)
        {
            engine.StepOnce();
            Debug.Log(engine.State.Log.ToString());
        }
        else
        {
            Debug.LogWarning("游戏尚未初始化。");
        }
    }
}
