using BarPokerBackend.Models;
using BarPokerBackend.Services;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace BarPokerBackend.Models;

public class TableState
{
    public string TableId { get; set; }
    public string GamePhase { get; set; } = "Waiting";
    public int Pot { get; set; }
    public int CurrentHighestBet { get; set; }
    public int ActiveSeatTurn { get; set; }
    public int DealerButtonSeat { get; set; } = 0;
    public int SmallBlindAmount { get; set; } = 10;
    public int BigBlindAmount { get; set; } = 20;
    public List<string> CommunityCards { get; set; } = new();
    public Dictionary<int, Player> Players { get; set; } = new();
    public ConcurrentDictionary<string, int> ConnectionToSeatMap { get; set; } = new();

    [JsonIgnore]
    public DeckManager Deck { get; set; } = new();
}