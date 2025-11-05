using UnityEngine;
using LiarsBar;

public class LiarBarGameController : MonoBehaviour
{
    [SerializeField] private GameConfig gameConfig;
    
    private void Start()
    {
        var agents = new IAgent[]
        {
            new RandomBot("玩家1"),
            new RandomBot("玩家2"),
            new RandomBot("玩家3")
        };
        
        GameEngine.Instance.Initialize(gameConfig, agents);
    }
    
    public void StepGame() => GameEngine.Instance.StepOnce();
}
