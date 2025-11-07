using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using MoreMountains.Feedbacks;

namespace LiarsBar
{
    /// <summary>
    /// 玩家视图：负责显示单个玩家的状态和手牌
    /// </summary>
    public class PlayerView : MonoBehaviour
    {
        [Header("玩家信息")]
        public int PlayerIndex;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI HandCountText;
        public Image StatusIndicator; // 当前回合高亮
        
        [Header("状态颜色")]
        public Color AliveColor = Color.green;
        public Color DeadColor = Color.red;
        public Color ActiveTurnColor = Color.yellow;
        public Color InactiveColor = Color.gray;
        
        [Header("反馈效果")]
        public MMFeedbacks PlayCardFeedback; // 出牌动画
        public MMFeedbacks EliminatedFeedback; // 淘汰动画
        public MMFeedbacks WinFeedback; // 获胜动画
        
        [Header("手牌显示（可选）")]
        public Transform CardContainer;
        public GameObject CardPrefab;
        private List<GameObject> cardObjects = new List<GameObject>();

        /// <summary>更新玩家信息显示</summary>
        public void UpdateDisplay(PlayerData data, bool isCurrentTurn)
        {
            if (NameText != null)
                NameText.text = data.Name;
                
            if (HandCountText != null)
                HandCountText.text = $"手牌: {data.Hand.Count}";
                
            if (StatusIndicator != null)
            {
                if (!data.Alive)
                    StatusIndicator.color = DeadColor;
                else if (isCurrentTurn)
                    StatusIndicator.color = ActiveTurnColor;
                else
                    StatusIndicator.color = AliveColor;
            }
        }

        /// <summary>播放出牌动画</summary>
        public void PlayCardAnimation()
        {
            PlayCardFeedback?.PlayFeedbacks();
        }

        /// <summary>播放淘汰动画</summary>
        public void PlayEliminatedAnimation()
        {
            EliminatedFeedback?.PlayFeedbacks();
        }

        /// <summary>播放胜利动画</summary>
        public void PlayWinAnimation()
        {
            WinFeedback?.PlayFeedbacks();
        }

        /// <summary>显示手牌（仅用于调试或本地玩家）</summary>
        public void ShowHand(List<Card> hand)
        {
            if (CardContainer == null || CardPrefab == null) return;
            
            // 清除旧卡牌
            foreach (var obj in cardObjects)
                Destroy(obj);
            cardObjects.Clear();
            
            // 创建新卡牌
            foreach (var card in hand)
            {
                var cardObj = Instantiate(CardPrefab, CardContainer);
                var cardText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
                if (cardText != null)
                    cardText.text = card.Rank.ToString();
                cardObjects.Add(cardObj);
            }
        }

        /// <summary>高亮当前回合</summary>
        public void SetActiveHighlight(bool active)
        {
            if (StatusIndicator != null)
                StatusIndicator.color = active ? ActiveTurnColor : AliveColor;
        }
    }
}
