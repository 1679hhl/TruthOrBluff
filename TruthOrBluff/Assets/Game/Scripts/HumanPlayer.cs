using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace LiarsBar
{
    /// <summary>
    /// 人类玩家：通过UI输入进行决策
    /// </summary>
    public class HumanPlayer : IAgent
    {
        public string Name { get; }
        
        // 玩家的选择结果（由PlayerInputManager设置）
        public List<string> SelectedCardIds { get; set; }
        public bool ChallengeDecision { get; set; }
        
        public HumanPlayer(string name)
        {
            Name = name;
        }

        public List<string> ChooseClaimCards(GameState s, int playerIndex, Random rng)
        {
            // 人类玩家返回null表示需要等待输入
            // GameRunner会检测到null并暂停自动步进，等待玩家输入
            if (SelectedCardIds != null && SelectedCardIds.Count > 0)
            {
                var result = new List<string>(SelectedCardIds);
                SelectedCardIds = null; // 清除已使用的选择
                return result;
            }
            
            return null; // 表示需要等待玩家输入
        }

        public bool DecideChallenge(GameState s, int responderIndex, Random rng)
        {
            // 返回存储的决策（由UI设置）
            return ChallengeDecision;
        }
    }
}

