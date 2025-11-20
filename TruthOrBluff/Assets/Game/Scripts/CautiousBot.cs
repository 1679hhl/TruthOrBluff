using System;
using System.Linq;
using System.Collections.Generic;

namespace LiarsBar
{
    // 谨慎型：如果自己手里有"桌面牌面"，基本不质疑；否则小概率质疑
    public class CautiousBot : IAgent
    {
        public string Name { get; }
        public CautiousBot(string name) { Name = name; }
        public List<string> ChooseClaimCards(GameState s, int playerIndex, Random rng)
        {
            var hand = s.Players[playerIndex].Hand;
            if (hand.Count == 0) return new List<string>();
            
            // 如果真有桌面牌面，优先出真牌；谨慎起见只出1-2张
            var realCards = hand.Where(c => c.Rank == s.TableRank).ToList();
            var selectedCards = new List<string>();
            
            if (realCards.Count > 0)
            {
                // 有真牌，出1-2张真牌
                int count = Math.Min(rng.Next(1, 3), realCards.Count);
                for (int i = 0; i < count; i++)
                {
                    selectedCards.Add(realCards[i].Id);
                }
            }
            else
            {
                // 没有真牌，随机bluff 1张
                selectedCards.Add(hand[rng.Next(hand.Count)].Id);
            }
            
            return selectedCards;
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
        public List<string> ChooseClaimCards(GameState s, int playerIndex, Random rng)
        {
            var hand = s.Players[playerIndex].Hand;
            if (hand.Count == 0) return new List<string>();
            
            // 莽夫：随机出1-3张牌
            int cardCount = rng.Next(1, Math.Min(4, hand.Count + 1));
            var selectedCards = new List<string>();
            var availableCards = new List<Card>(hand);
            
            for (int i = 0; i < cardCount; i++)
            {
                var card = availableCards[rng.Next(availableCards.Count)];
                selectedCards.Add(card.Id);
                availableCards.Remove(card);
            }
            
            return selectedCards;
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
