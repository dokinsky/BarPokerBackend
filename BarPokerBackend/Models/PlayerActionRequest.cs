namespace BarPokerBackend.Models;

public class PlayerActionRequest
{
    public PokerActionType Action { get; set; }
    public int Amount { get; set; } // Used for raises
}