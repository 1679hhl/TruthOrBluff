using System;

namespace LiarsBar
{
    /// <summary>牌的点数</summary>
    public enum Rank { Q, K, A }

    /// <summary>游戏配置（可序列化，支持 Inspector 编辑）</summary>
    [Serializable]
    public class GameConfig
    {
        public int PlayerCount = 4;
        public Rank TableRank = Rank.Q;
        public int BulletSlot = 4;
        public int CopiesPerRankPerPlayer = 2;
        public int Seed = 12345;
    }
}
