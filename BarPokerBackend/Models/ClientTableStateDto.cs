namespace BarPokerBackend.Models;

public class ClientTableStateDto
{
    public string TableId { get; set; }
    public string GamePhase { get; set; }
    public int Pot { get; set; }
    public int CurrentHighestBet { get; set; }
    public int ActiveSeatTurn { get; set; }
    public List<string> CommunityCards { get; set; } = new();
    public Dictionary<int, ClientPlayerDto> Players { get; set; } = new();
}
