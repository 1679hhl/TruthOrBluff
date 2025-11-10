using UnityEngine;

namespace LiarsBar
{
    /// <summary>
    /// 角色动画控制器：控制3D角色的动画播放
    /// 可以附加到角色预制体上，或由 PlayerController 动态控制
    /// </summary>
    public class CharacterAnimationController : MonoBehaviour
    {
        [Header("动画组件")]
        public Animator animator;
        
        [Header("动画触发器名称")]
        public string idleAnimation = "Idle";
        public string playCardAnimation = "PlayCard";
        public string winAnimation = "Win";
        public string loseAnimation = "Lose";
        public string thinkAnimation = "Think";
        
        void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        /// <summary>播放闲置动画</summary>
        public void PlayIdle()
        {
            PlayAnimation(idleAnimation);
        }

        /// <summary>播放出牌动画</summary>
        public void PlayCard()
        {
            PlayAnimation(playCardAnimation);
        }

        /// <summary>播放胜利动画</summary>
        public void PlayWin()
        {
            PlayAnimation(winAnimation);
        }

        /// <summary>播放失败动画</summary>
        public void PlayLose()
        {
            PlayAnimation(loseAnimation);
        }

        /// <summary>播放思考动画</summary>
        public void PlayThink()
        {
            PlayAnimation(thinkAnimation);
        }

        /// <summary>播放指定动画</summary>
        void PlayAnimation(string animationName)
        {
            if (animator == null)
            {
                Debug.LogWarning("未找到 Animator 组件");
                return;
            }

            if (string.IsNullOrEmpty(animationName))
                return;

            // 如果是触发器
            animator.SetTrigger(animationName);
            
            // 或者直接播放（根据你的动画设置选择）
            // animator.Play(animationName);
        }

        /// <summary>设置布尔参数（用于状态机）</summary>
        public void SetBool(string paramName, bool value)
        {
            if (animator != null)
                animator.SetBool(paramName, value);
        }

        /// <summary>设置浮点参数</summary>
        public void SetFloat(string paramName, float value)
        {
            if (animator != null)
                animator.SetFloat(paramName, value);
        }
    }
}
