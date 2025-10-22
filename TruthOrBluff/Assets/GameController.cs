using UnityEngine;
using MoreMountains.Feedbacks;
using LiarsBar;

public class LiarBarGameController : MonoBehaviour
{
    [Header("Feedbacks")]
    [SerializeField] private MMFeedbacks playCardFeedbacks;
    
    [Header("Game Settings")]
    [SerializeField] private GameConfig gameConfig;
    
    private void Start()
    {
        // 初始化游戏引擎
        var agents = new IAgent[]
        {
            new RandomBot("玩家1"),
            new RandomBot("玩家2"),
            new RandomBot("玩家3")
        };
        
        GameEngine.Instance.Initialize(gameConfig, agents);
        
        // 将场景中的 Feedbacks 传递给游戏引擎
        GameEngine.Instance.PlayCardFeedbacks = playCardFeedbacks;
        
        // 开始游戏
        // GameEngine.Instance.RunUntilGameOver();
    }
    
    // 如果需要手动推进游戏
    public void StepGame()
    {
        GameEngine.Instance.StepOnce();
    }
}
