using System;

namespace LiarsBar
{
    public enum Rank { Q, K, A } // 牌的点数：Q、K、A

    public interface IGameConfig
    {
        int PlayerCount { get; } // 玩家人数
        Rank TableRank { get; } // 桌面固定牌面
        int BulletSlot { get; } // 惩罚轨触发位置
        int CopiesPerRankPerPlayer { get; } // 每名玩家每个点数的份数
        int Seed { get; } // 随机种子
    }

    public class GameConfig : IGameConfig
    {
        public int PlayerCount { get; set; } = 4; // 默认玩家人数为4
        public Rank TableRank { get; set; } = Rank.Q; // 默认桌面牌面为Q
        public int BulletSlot { get; set; } = 4; // 默认惩罚轨触发位置为4
        public int CopiesPerRankPerPlayer { get; set; } = 2; // 默认每个点数的份数为2
        public int Seed { get; set; } = 12345; // 默认随机种子
    }
}
