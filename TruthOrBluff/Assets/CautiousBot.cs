using System;
using System.Linq;

namespace LiarsBar
{
    // 谨慎型：如果自己手里有“桌面牌面”，基本不质疑；否则小概率质疑
    public class CautiousBot : IAgent
    {
        public string Name { get; }
        public CautiousBot(string name) { Name = name; }
        public string ChooseClaimCard(GameState s, int playerIndex, Random rng)
        {
            var hand = s.Players[playerIndex].Hand;
            if (hand.Count == 0) return null;
            // 如果真有桌面牌面，优先出真牌；没有就随机 bluff
            var real = hand.FirstOrDefault(c => c.Rank == s.TableRank);
            return (real ?? hand[rng.Next(hand.Count)]).Id;
        }
        public bool DecideChallenge(GameState s, int responderIndex, Random rng)
        {
            var me = s.Players[responderIndex];
            bool haveTable = me.Hand.Any(c => c.Rank == s.TableRank);
            double p = haveTable ? 0.10 : 0.30;
            return rng.NextDouble() < p;
        }
    }

    // 莽夫型：经常质疑；声明随缘
    public class RecklessBot : IAgent
    {
        public string Name { get; }
        public RecklessBot(string name) { Name = name; }
        public string ChooseClaimCard(GameState s, int playerIndex, Random rng)
        {
            var hand = s.Players[playerIndex].Hand;
            if (hand.Count == 0) return null;
            return hand[rng.Next(hand.Count)].Id;
        }
        public bool DecideChallenge(GameState s, int responderIndex, Random rng)
        {
            var me = s.Players[responderIndex];
            bool haveTable = me.Hand.Any(c => c.Rank == s.TableRank);
            double p = haveTable ? 0.50 : 0.80;
            return rng.NextDouble() < p;
        }
    }
}