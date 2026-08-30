namespace BarPokerBackend.Models;

public class ClientPlayerDto
{
    public int SeatNumber { get; set; }
    public string Name { get; set; }
    public int ChipCount { get; set; }
    public int CurrentBet { get; set; }
    public bool HasFolded { get; set; }
    public List<string> HoleCards { get; set; } = new();

    // SERVER-DRIVEN UI FLAGS: Tells the client/pod what buttons should light up or be active
    public bool CanCheck { get; set; }
    public bool CanCall { get; set; }
    public bool CanFold { get; set; }
    public bool CanRaise { get; set; }
    public int CallAmount { get; set; }
    public int MinRaiseAmount { get; set; }
    public int PotRaiseAmount { get; set; }
}
