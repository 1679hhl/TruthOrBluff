using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using MoreMountains.Feedbacks;

namespace LiarsBar
{
    /// <summary>
    /// 玩家控制器：3D场景中的玩家实体
    /// 管理角色模型、动画、世界空间UI和手牌显示
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("玩家信息")]
        public int PlayerIndex;
        public PlayerData PlayerData; // 关联的数据
        
        [Header("角色模型")]
        public GameObject CharacterModel; // 当前角色实例
        public Transform CharacterRoot; // 角色模型根节点
        private CharacterAnimationController animController;
        
        [Header("世界空间UI")]
        public Canvas WorldSpaceCanvas;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI HandCountText;
        public Image StatusIndicator;
        
        [Header("UI颜色配置")]
        public Color AliveColor = Color.green;
        public Color DeadColor = Color.red;
        public Color ActiveTurnColor = Color.yellow;
        
        [Header("手牌显示")]
        public Transform LeftHandBone; // 左手骨骼（手持卡牌）
        public Transform RightHandBone; // 右手骨骼
        public Transform CardHoldPoint; // 手牌持握点（如果没有骨骼绑定）
        public GameObject CardPrefab; // 3D卡牌预制体
        private List<GameObject> handCardObjects = new List<GameObject>();
        
        [Header("反馈效果")]
        public MMFeedbacks PlayCardFeedback;
        public MMFeedbacks EliminatedFeedback;
        public MMFeedbacks WinFeedback;
        
        [Header("调试")]
        public bool ShowHandCards = false; // 是否显示手牌（仅本地玩家或调试）

        /// <summary>初始化玩家控制器</summary>
        public void Initialize(PlayerData data, GameObject characterPrefab)
        {
            PlayerData = data;
            PlayerIndex = data.Index;
            
            // 生成角色模型
            if (characterPrefab != null && CharacterRoot != null)
            {
                CharacterModel = Instantiate(characterPrefab, CharacterRoot.position, CharacterRoot.rotation, CharacterRoot);
                animController = CharacterModel.GetComponent<CharacterAnimationController>();
                if (animController == null)
                    animController = CharacterModel.GetComponentInChildren<CharacterAnimationController>();
                
                // 尝试自动查找手部骨骼
                if (LeftHandBone == null)
                    LeftHandBone = FindBoneByName(CharacterModel.transform, "LeftHand");
                if (RightHandBone == null)
                    RightHandBone = FindBoneByName(CharacterModel.transform, "RightHand");
            }
            
            // 初始化UI
            UpdateUI(false);
            
            // 初始化手牌显示
            if (ShowHandCards)
                UpdateHandCardsDisplay();
                
            Debug.Log($"PlayerController {PlayerIndex} ({data.Name}) 初始化完成");
        }

        /// <summary>更新UI显示</summary>
        public void UpdateUI(bool isCurrentTurn)
        {
            if (NameText != null)
                NameText.text = PlayerData.Name;
                
            if (HandCountText != null)
                HandCountText.text = $"手牌: {PlayerData.Hand.Count}";
                
            if (StatusIndicator != null)
            {
                if (!PlayerData.Alive)
                    StatusIndicator.color = DeadColor;
                else if (isCurrentTurn)
                    StatusIndicator.color = ActiveTurnColor;
                else
                    StatusIndicator.color = AliveColor;
            }
            
            // 让UI始终朝向摄像机
            if (WorldSpaceCanvas != null && Camera.main != null)
            {
                WorldSpaceCanvas.transform.LookAt(Camera.main.transform);
                WorldSpaceCanvas.transform.Rotate(0, 180, 0); // 翻转以正确朝向
            }
        }

        /// <summary>更新手牌显示（3D手持）</summary>
        public void UpdateHandCardsDisplay()
        {
            // 清除旧手牌
            foreach (var card in handCardObjects)
                Destroy(card);
            handCardObjects.Clear();
            
            if (!ShowHandCards || CardPrefab == null || PlayerData.Hand.Count == 0)
                return;
            
            // 确定手牌持握点
            Transform holdPoint = LeftHandBone ?? CardHoldPoint;
            if (holdPoint == null)
            {
                Debug.LogWarning($"Player {PlayerIndex}: 未设置手牌持握点");
                return;
            }
            
            // 生成手牌（扇形排列）
            float fanAngle = 15f; // 每张牌之间的角度
            float startAngle = -(PlayerData.Hand.Count - 1) * fanAngle / 2f;
            
            for (int i = 0; i < PlayerData.Hand.Count; i++)
            {
                var card = PlayerData.Hand[i];
                // 不作为子节点，直接放在场景根节点下
                var cardObj = Instantiate(CardPrefab, Vector3.zero, Quaternion.identity);
                
                // 设置卡牌位置和旋转（扇形排列）
                float angle = startAngle + i * fanAngle;
                
                // 复制骨骼的世界位置和旋转
                cardObj.transform.position = holdPoint.position;
                cardObj.transform.rotation = holdPoint.rotation;
                
                // 应用偏移（基于骨骼的局部坐标系）
                Vector3 offset = holdPoint.TransformDirection(new Vector3(i * 0.05f, 0, 0));
                cardObj.transform.position += offset;
                
                // 应用扇形旋转（基于骨骼的局部Z轴）
                cardObj.transform.Rotate(holdPoint.forward, -angle, Space.World);
                
                // 设置卡牌显示
                var cardText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
                if (cardText != null)
                    cardText.text = card.Rank.ToString();
                
                handCardObjects.Add(cardObj);
            }
        }

        /// <summary>播放出牌动画</summary>
        public void PlayCardAnimation()
        {
            animController?.PlayCard();
            PlayCardFeedback?.PlayFeedbacks();
            
            // 更新手牌显示
            if (ShowHandCards)
                UpdateHandCardsDisplay();
        }

        /// <summary>播放淘汰动画</summary>
        public void PlayEliminatedAnimation()
        {
            animController?.PlayLose();
            EliminatedFeedback?.PlayFeedbacks();
            
            // 可以添加角色倒下效果
            if (CharacterModel != null)
            {
                // 例如：添加布娃娃效果或淡出
            }
        }

        /// <summary>播放胜利动画</summary>
        public void PlayWinAnimation()
        {
            animController?.PlayWin();
            WinFeedback?.PlayFeedbacks();
        }

        /// <summary>播放思考动画</summary>
        public void PlayThinkAnimation()
        {
            animController?.PlayThink();
        }

        /// <summary>高亮当前回合</summary>
        public void SetActiveHighlight(bool active)
        {
            UpdateUI(active);
            
            // 可以添加额外的高亮效果，比如光圈
            // TODO: 添加地面光圈或发光效果
        }

        /// <summary>获取角色动画控制器</summary>
        public CharacterAnimationController GetAnimationController()
        {
            return animController;
        }

        /// <summary>查找骨骼节点</summary>
        Transform FindBoneByName(Transform root, string boneName)
        {
            if (root.name.Contains(boneName))
                return root;
                
            foreach (Transform child in root)
            {
                var result = FindBoneByName(child, boneName);
                if (result != null)
                    return result;
            }
            
            return null;
        }

        void LateUpdate()
        {
            // 每帧让UI朝向摄像机
            if (WorldSpaceCanvas != null && Camera.main != null)
            {
                WorldSpaceCanvas.transform.LookAt(Camera.main.transform);
                WorldSpaceCanvas.transform.Rotate(0, 180, 0);
            }
        }

        void Update()
        {
            if (ShowHandCards && LeftHandBone != null)
            {
                // 实时更新卡牌位置以跟随骨骼
                UpdateHandCardsPosition();
            }
        }

        void UpdateHandCardsPosition()
        {
            Transform holdPoint = LeftHandBone ?? CardHoldPoint;
            if (holdPoint == null) return;
            
            float fanAngle = 15f;
            float startAngle = -(handCardObjects.Count - 1) * fanAngle / 2f;
            
            for (int i = 0; i < handCardObjects.Count; i++)
            {
                var cardObj = handCardObjects[i];
                if (cardObj == null) continue;
                
                float angle = startAngle + i * fanAngle;
                
                cardObj.transform.position = holdPoint.position;
                cardObj.transform.rotation = holdPoint.rotation;
                
                Vector3 offset = holdPoint.TransformDirection(new Vector3(i * 0.01f, 0, 0));
                cardObj.transform.position += offset;
                
                cardObj.transform.Rotate(holdPoint.forward, -angle, Space.World);
            }
        }

        void OnDrawGizmosSelected()
        {
            // 在Scene视图中显示手牌持握点
            if (CardHoldPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(CardHoldPoint.position, 0.1f);
            }
        }
    }
}
