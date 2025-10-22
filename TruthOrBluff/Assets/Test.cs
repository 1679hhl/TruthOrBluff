using UnityEngine;

public class Test : MonoBehaviour
{
    /// <summary>
    /// 动画状态枚举
    /// </summary>
    public enum AnimationState
    {
        Default = 2,
        Action = 4
    }

    private const float TRANSITION_DURATION = 0.1f;

    public Animator Animator;
    public AnimationState currentAnimation;

    void Start()
    {
        
    }

    /// <summary>
    /// 播放动作动画
    /// </summary>
    public void PlayActionAnimation()
    {
        PlayAnimation(AnimationState.Action);
    }

    /// <summary>
    /// 停止动画,返回默认状态
    /// </summary>
    public void StopAnimation()
    {
        PlayAnimation(AnimationState.Default);
    }
    
    /// <summary>
    /// 播放默认动画
    /// </summary>
    public void PlayDefaultAnimation()
    {
        PlayAnimation(AnimationState.Default);
    }

    /// <summary>
    /// 播放指定动画
    /// </summary>
    private void PlayAnimation(AnimationState animationState)
    {
        if (Animator == null)
        {
            Debug.LogWarning("Animator is not assigned!");
            return;
        }

        currentAnimation = animationState;
        // 将枚举值转换为字符串用于 Animator
        string animationName = ((int)animationState).ToString();
        Animator.CrossFade(animationName, TRANSITION_DURATION);
    }
}
