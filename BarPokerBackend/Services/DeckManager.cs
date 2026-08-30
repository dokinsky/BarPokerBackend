namespace BarPokerBackend.Services;

public class DeckManager
{
    private List<string> _cards = new();

    public void InitializeAndShuffle()
    {
        string[] suits = { "♠", "♥", "♦", "♣" };
        string[] ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };

        _cards.Clear();
        foreach (var suit in suits)
        {
            foreach (var rank in ranks)
            {
                _cards.Add($"{rank}{suit}");
            }
        }

        // Fisher-Yates shuffle
        Random rng = new();
        int n = _cards.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            string value = _cards[k];
            _cards[k] = _cards[n];
            _cards[n] = value;
        }
    }

    public List<string> DealCards(int count)
    {
        var dealt = _cards.Take(count).ToList();
        _cards.RemoveRange(0, count);
        return dealt;
    }
}
