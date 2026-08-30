namespace BarPokerBackend.Models;

public class Player
{
    public int SeatNumber { get; set; }
    public string Name { get; set; }
    public int ChipCount { get; set; }
    public int CurrentBet { get; set; }
    public List<string> HoleCards { get; set; } = new();
    public bool HasFolded { get; set; }
}
