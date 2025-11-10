using UnityEngine;
using TMPro;

namespace LiarsBar
{
    /// <summary>
    /// 3D卡牌显示：用于在角色手中显示的卡牌
    /// </summary>
    public class Card3DView : MonoBehaviour
    {
        [Header("卡牌信息")]
        public Card CardData;
        
        [Header("显示组件")]
        public TextMeshPro RankText; // 牌面文字（3D文本）
        public MeshRenderer CardMesh; // 卡牌网格
        
        [Header("材质配置")]
        public Material QMaterial; // Q的材质
        public Material KMaterial; // K的材质
        public Material AMaterial; // A的材质
        public Material BackMaterial; // 背面材质
        
        [Header("显示设置")]
        public bool ShowFront = true; // 是否显示正面

        /// <summary>设置卡牌数据并更新显示</summary>
        public void SetCard(Card card, bool showFront = true)
        {
            CardData = card;
            ShowFront = showFront;
            UpdateDisplay();
        }

        /// <summary>更新显示</summary>
        void UpdateDisplay()
        {
            if (CardData == null) return;
            
            // 更新文字
            if (RankText != null)
            {
                RankText.text = ShowFront ? CardData.Rank.ToString() : "?";
                RankText.gameObject.SetActive(ShowFront);
            }
            
            // 更新材质
            if (CardMesh != null)
            {
                if (ShowFront)
                {
                    Material mat = GetMaterialForRank(CardData.Rank);
                    if (mat != null)
                        CardMesh.material = mat;
                }
                else
                {
                    if (BackMaterial != null)
                        CardMesh.material = BackMaterial;
                }
            }
        }

        /// <summary>根据牌面获取材质</summary>
        Material GetMaterialForRank(Rank rank)
        {
            return rank switch
            {
                Rank.Q => QMaterial,
                Rank.K => KMaterial,
                Rank.A => AMaterial,
                _ => null
            };
        }

        /// <summary>翻转卡牌</summary>
        public void Flip()
        {
            ShowFront = !ShowFront;
            UpdateDisplay();
            
            // 可以添加翻转动画
            // StartCoroutine(FlipAnimation());
        }

        /// <summary>播放打出动画</summary>
        public void PlayCardAnimation(Vector3 targetPosition, float duration = 0.5f)
        {
            // TODO: 实现卡牌飞向目标位置的动画
            // 可以使用 DOTween 或者协程
            StartCoroutine(MoveToPosition(targetPosition, duration));
        }

        System.Collections.IEnumerator MoveToPosition(Vector3 target, float duration)
        {
            Vector3 startPos = transform.position;
            float elapsed = 0;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // 使用缓动函数
                t = EaseOutQuad(t);
                
                transform.position = Vector3.Lerp(startPos, target, t);
                
                // 添加旋转效果
                transform.Rotate(Vector3.up, 360 * Time.deltaTime / duration);
                
                yield return null;
            }
            
            transform.position = target;
        }

        float EaseOutQuad(float t)
        {
            return 1 - (1 - t) * (1 - t);
        }

        void OnValidate()
        {
            // 编辑器中修改时自动更新
            if (CardData != null)
                UpdateDisplay();
        }

        void OnDrawGizmosSelected()
        {
            // 在Scene视图中显示卡牌信息
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
    }
}
