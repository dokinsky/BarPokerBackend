namespace BarPokerBackend.Services;

public enum HandRank
{
    HighCard = 1,
    OnePair = 2,
    TwoPair = 3,
    ThreeOfAKind = 4,
    Straight = 5,
    Flush = 6,
    FullHouse = 7,
    FourOfAKind = 8,
    StraightFlush = 9,
    RoyalFlush = 10
}

public class EvaluatedHand : IComparable<EvaluatedHand>
{
    public HandRank Rank { get; set; }
    // Tie-breaker values ordered from most significant to least significant
    public List<int> TieBreakers { get; set; } = new();

    public int CompareTo(EvaluatedHand? other)
    {
        if (other == null) return 1;

        // First compare primary hand rank (e.g. Full House beats Flush)
        int rankComparison = Rank.CompareTo(other.Rank);
        if (rankComparison != 0) return rankComparison;

        // If ranks are equal, compare tie-breakers sequentially (e.g. higher pair, higher kicker)
        for (int i = 0; i < Math.Min(TieBreakers.Count, other.TieBreakers.Count); i++)
        {
            int tbComparison = TieBreakers[i].CompareTo(other.TieBreakers[i]);
            if (tbComparison != 0) return tbComparison;
        }

        return 0; // Exact tie
    }
}

public record Card(int NumericRank, char Suit)
{
    // Parses string format like "A♠", "10♥", "2♦"
    public static Card Parse(string cardStr)
    {
        if (string.IsNullOrEmpty(cardStr) || cardStr.Length < 2)
            throw new ArgumentException($"Invalid card string: {cardStr}");

        string rankPart = cardStr.Substring(0, cardStr.Length - 1);
        char suit = cardStr[^1];

        int rank = rankPart switch
        {
            "J" => 11,
            "Q" => 12,
            "K" => 13,
            "A" => 14,
            _ => int.Parse(rankPart)
        };

        return new Card(rank, suit);
    }
}

public static class HandEvaluator
{
    public static EvaluatedHand EvaluateBestHand(List<string> holeCards, List<string> communityCards)
    {
        var allCards = holeCards.Concat(communityCards).Select(Card.Parse).ToList();

        if (allCards.Count < 5)
            throw new InvalidOperationException("Need at least 5 cards to evaluate a poker hand.");

        // Generate all 21 combinations of 5 cards out of 7
        var combinations = GetCombinations(allCards, 5);
        EvaluatedHand bestHand = null;

        foreach (var combo in combinations)
        {
            var evaluated = Evaluate5CardHand(combo);
            if (bestHand == null || evaluated.CompareTo(bestHand) > 0)
            {
                bestHand = evaluated;
            }
        }

        return bestHand;
    }

    private static EvaluatedHand Evaluate5CardHand(List<Card> hand)
    {
        // Sort descending by rank
        var sorted = hand.OrderByDescending(c => c.NumericRank).ToList();

        bool isFlush = sorted.All(c => c.Suit == sorted[0].Suit);
        bool isStraight = CheckStraight(sorted, out int highCardInStraight);

        // Group cards by rank frequency (e.g., pairs, three of a kind)
        var groups = sorted.GroupBy(c => c.NumericRank)
                           .OrderByDescending(g => g.Count())
                           .ThenByDescending(g => g.Key)
                           .ToList();

        // 1. Royal Flush / Straight Flush
        if (isStraight && isFlush)
        {
            HandRank rank = (highCardInStraight == 14) ? HandRank.RoyalFlush : HandRank.StraightFlush;
            return new EvaluatedHand { Rank = rank, TieBreakers = new List<int> { highCardInStraight } };
        }

        // 2. Four of a Kind
        if (groups[0].Count() == 4)
        {
            int quadRank = groups[0].Key;
            int kicker = groups[1].Key;
            return new EvaluatedHand { Rank = HandRank.FourOfAKind, TieBreakers = new List<int> { quadRank, kicker } };
        }

        // 3. Full House
        if (groups[0].Count() == 3 && groups[1].Count() >= 2)
        {
            int tripRank = groups[0].Key;
            int pairRank = groups[1].Key;
            return new EvaluatedHand { Rank = HandRank.FullHouse, TieBreakers = new List<int> { tripRank, pairRank } };
        }

        // 4. Flush
        if (isFlush)
        {
            return new EvaluatedHand { Rank = HandRank.Flush, TieBreakers = sorted.Select(c => c.NumericRank).ToList() };
        }

        // 5. Straight
        if (isStraight)
        {
            return new EvaluatedHand { Rank = HandRank.Straight, TieBreakers = new List<int> { highCardInStraight } };
        }

        // 6. Three of a Kind
        if (groups[0].Count() == 3)
        {
            int tripRank = groups[0].Key;
            var kickers = groups.Skip(1).Select(g => g.Key).ToList();
            var tb = new List<int> { tripRank };
            tb.AddRange(kickers);
            return new EvaluatedHand { Rank = HandRank.ThreeOfAKind, TieBreakers = tb };
        }

        // 7. Two Pair
        if (groups[0].Count() == 2 && groups[1].Count() == 2)
        {
            int highPair = Math.Max(groups[0].Key, groups[1].Key);
            int lowPair = Math.Min(groups[0].Key, groups[1].Key);
            int kicker = groups[2].Key;
            return new EvaluatedHand { Rank = HandRank.TwoPair, TieBreakers = new List<int> { highPair, lowPair, kicker } };
        }

        // 8. One Pair
        if (groups[0].Count() == 2)
        {
            int pairRank = groups[0].Key;
            var kickers = groups.Skip(1).Select(g => g.Key).ToList();
            var tb = new List<int> { pairRank };
            tb.AddRange(kickers);
            return new EvaluatedHand { Rank = HandRank.OnePair, TieBreakers = tb };
        }

        // 9. High Card
        return new EvaluatedHand { Rank = HandRank.HighCard, TieBreakers = sorted.Select(c => c.NumericRank).ToList() };
    }

    private static bool CheckStraight(List<Card> sorted, out int highCard)
    {
        highCard = 0;
        var distinctRanks = sorted.Select(c => c.NumericRank).Distinct().OrderByDescending(r => r).ToList();

        if (distinctRanks.Count < 5) return false;

        // Check standard straight
        for (int i = 0; i <= distinctRanks.Count - 5; i++)
        {
            if (distinctRanks[i] - distinctRanks[i + 4] == 4 &&
                distinctRanks[i] - distinctRanks[i + 1] == 3 &&
                distinctRanks[i] - distinctRanks[i + 2] == 2 &&
                distinctRanks[i] - distinctRanks[i + 3] == 1)
            {
                highCard = distinctRanks[i];
                return true;
            }
        }

        // Check A-5 low straight (Wheel: A, 5, 4, 3, 2)
        if (distinctRanks.Contains(14) && distinctRanks.Contains(5) &&
            distinctRanks.Contains(4) && distinctRanks.Contains(3) && distinctRanks.Contains(2))
        {
            highCard = 5; // In an A-5 straight, 5 is structurally the highest card for tie-breaking
            return true;
        }

        return false;
    }

    private static IEnumerable<List<T>> GetCombinations<T>(List<T> list, int length)
    {
        if (length == 0) yield return new List<T>();
        else if (list.Count == 0) yield break;
        else
        {
            var head = list[0];
            var tail = list.Skip(1).ToList();

            foreach (var combo in GetCombinations(tail, length - 1))
            {
                combo.Insert(0, head);
                yield return combo;
            }

            foreach (var combo in GetCombinations(tail, length))
            {
                yield return combo;
            }
        }
    }
}