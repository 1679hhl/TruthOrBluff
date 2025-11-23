using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LiarsBar
{
    /// <summary>
    /// 玩家输入管理器：处理人类玩家的UI交互
    /// 管理手牌选择和质疑决策UI
    /// </summary>
    public class PlayerInputManager : MonoBehaviour
    {
        public static PlayerInputManager Instance { get; private set; }

        [Header("UI面板")]
        public GameObject CardSelectionPanel;    // 选择卡牌面板
        public GameObject ChallengeDecisionPanel; // 质疑决策面板
        
        [Header("卡牌选择UI")]
        public Transform CardButtonContainer;     // 卡牌按钮容器
        public GameObject CardButtonPrefab;       // 卡牌按钮预制体
        public Button ConfirmCardButton;          // 确认出牌按钮
        public TextMeshProUGUI SelectionInfoText; // 选择提示文本
        
        [Header("质疑决策UI")]
        public Button ChallengeButton;            // 质疑按钮
        public Button PassButton;                 // 不质疑（继续出牌）按钮
        public TextMeshProUGUI DecisionInfoText;  // 决策提示文本

        // 当前状态
        private List<Card> currentHand;
        private List<string> selectedCardIds = new List<string>();
        private int minCards = 1;
        private int maxCards = 3;
        
        // 回调
        private Action<List<string>> onCardSelectionComplete;
        private Action<bool> onChallengeDecisionComplete;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            // 初始化时隐藏所有面板
            if (CardSelectionPanel != null)
                CardSelectionPanel.SetActive(false);
            if (ChallengeDecisionPanel != null)
                ChallengeDecisionPanel.SetActive(false);
                
            // 绑定按钮事件
            if (ConfirmCardButton != null)
                ConfirmCardButton.onClick.AddListener(OnConfirmCardSelection);
            if (ChallengeButton != null)
                ChallengeButton.onClick.AddListener(() => OnChallengeDecision(true));
            if (PassButton != null)
                PassButton.onClick.AddListener(() => OnChallengeDecision(false));
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ========== 请求玩家输入 ==========

        /// <summary>请求玩家选择卡牌</summary>
        public void RequestCardSelection(List<Card> hand, int min, int max)
        {
            currentHand = hand;
            minCards = min;
            maxCards = max;
            selectedCardIds.Clear();
            
            // 显示卡牌选择面板
            if (CardSelectionPanel != null)
            {
                CardSelectionPanel.SetActive(true);
                CreateCardButtons();
                UpdateSelectionUI();
            }
            
            Debug.Log($"[InputManager] 请求选择 {min}-{max} 张牌");
        }

        /// <summary>请求玩家决定是否质疑</summary>
        public void RequestChallengeDecision()
        {
            // 显示质疑决策面板
            if (ChallengeDecisionPanel != null)
            {
                ChallengeDecisionPanel.SetActive(true);
                
                if (DecisionInfoText != null)
                {
                    var engine = GameEngine.Instance;
                    var lastClaim = engine.State.LastClaim;
                    var claimant = engine.State.Players[lastClaim.PlayerIndex];
                    DecisionInfoText.text = $"{claimant.Name} 出了 {lastClaim.CardCount} 张牌\n质疑 或 继续出牌？";
                }
            }
            
            Debug.Log($"[InputManager] 请求质疑决策");
        }

        // ========== 卡牌选择 ==========

        void CreateCardButtons()
        {
            // 清除旧按钮
            foreach (Transform child in CardButtonContainer)
            {
                Destroy(child.gameObject);
            }
            
            // 为每张手牌创建按钮
            foreach (var card in currentHand)
            {
                var btnObj = Instantiate(CardButtonPrefab, CardButtonContainer);
                var btn = btnObj.GetComponent<Button>();
                var text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                
                if (text != null)
                    text.text = card.Rank.ToString();
                
                // 绑定点击事件
                string cardId = card.Id;
                btn.onClick.AddListener(() => OnCardButtonClicked(cardId, btnObj));
                
                // 保存引用以便后续高亮
                btnObj.name = $"CardBtn_{cardId}";
            }
        }

        void OnCardButtonClicked(string cardId, GameObject btnObj)
        {
            if (selectedCardIds.Contains(cardId))
            {
                // 取消选择
                selectedCardIds.Remove(cardId);
                SetButtonHighlight(btnObj, false);
            }
            else
            {
                // 选择卡牌
                if (selectedCardIds.Count < maxCards)
                {
                    selectedCardIds.Add(cardId);
                    SetButtonHighlight(btnObj, true);
                }
                else
                {
                    Debug.Log($"最多只能选择 {maxCards} 张牌");
                }
            }
            
            UpdateSelectionUI();
        }

        void SetButtonHighlight(GameObject btnObj, bool highlight)
        {
            var image = btnObj.GetComponent<Image>();
            if (image != null)
            {
                image.color = highlight ? Color.yellow : Color.white;
            }
        }

        void UpdateSelectionUI()
        {
            if (SelectionInfoText != null)
            {
                SelectionInfoText.text = $"已选择: {selectedCardIds.Count}/{maxCards} 张\n(需要 {minCards}-{maxCards} 张)";
            }
            
            // 确认按钮仅在选择数量合法时可用
            if (ConfirmCardButton != null)
            {
                ConfirmCardButton.interactable = 
                    selectedCardIds.Count >= minCards && 
                    selectedCardIds.Count <= maxCards;
            }
        }

        void OnConfirmCardSelection()
        {
            if (selectedCardIds.Count < minCards || selectedCardIds.Count > maxCards)
            {
                Debug.LogWarning("选择的卡牌数量不合法");
                return;
            }
            
            // 隐藏面板
            if (CardSelectionPanel != null)
                CardSelectionPanel.SetActive(false);
            
            // 触发回调
            onCardSelectionComplete?.Invoke(new List<string>(selectedCardIds));
            
            Debug.Log($"[InputManager] 确认选择了 {selectedCardIds.Count} 张牌");
        }

        // ========== 质疑决策 ==========

        void OnChallengeDecision(bool challenge)
        {
            // 隐藏面板
            if (ChallengeDecisionPanel != null)
                ChallengeDecisionPanel.SetActive(false);
            
            // 触发回调
            onChallengeDecisionComplete?.Invoke(challenge);
            
            Debug.Log($"[InputManager] 玩家决定{(challenge ? "质疑" : "不质疑")}");
        }

        // ========== 注册回调 ==========

        public void SetCardSelectionCallback(Action<List<string>> callback)
        {
            onCardSelectionComplete = callback;
        }

        public void SetChallengeDecisionCallback(Action<bool> callback)
        {
            onChallengeDecisionComplete = callback;
        }
    }
}
