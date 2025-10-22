using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace LiarsBar
{
    public class Player : MonoBehaviour
    {
        public int Index; // 玩家索引
        public string Name; // 玩家名称
        public List<Card> Hand = new List<Card>(); // 玩家手牌
        public bool Alive = true; // 玩家是否存活
        public MMFeedbacks Feedbacks; // 玩家反馈
        public override string ToString() => $"P{Index}({Name})"; // 返回玩家的字符串表示
    }
}