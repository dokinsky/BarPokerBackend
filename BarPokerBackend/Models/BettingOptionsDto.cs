namespace BarPokerBackend.Models;
public enum PokerActionType
{
    Fold = 0,
    Check = 1,
    Call = 2,
    MinRaise = 3,
    PotRaise = 4,   
    AllIn = 5,
    CustomRaise = 6 
}
public class BettingOptionsDto
{
    public int MinRaise { get; set; }
    public int PotSizeRaise { get; set; } // 2-bet / Pot size reference
    public int MaxRaise { get; set; }     // All-in (Player's total chips + current bet)
    public bool CanCheck { get; set; }
    public bool CanCall { get; set; }
    public int CallAmount { get; set; }


    public static BettingOptionsDto GetBettingOptionsForPlayer(TableState table, int seatNumber)
    {
        var player = table.Players[seatNumber];
        int amountOwed = table.CurrentHighestBet - player.CurrentBet;

        // Min raise is typically Current Highest Bet + (Current Highest Bet - Previous Bet) 
        // Or simplified for casual play: Highest Bet + Big Blind (or match current highest + min increment)
        int minRaise = table.CurrentHighestBet == 0 ? table.BigBlindAmount : table.CurrentHighestBet * 2;

        // Max raise is everything the player has available
        int maxRaise = player.ChipCount + player.CurrentBet;

        // Pot-sized bet reference
        int potSizeRaise = table.Pot + amountOwed;

        return new BettingOptionsDto
        {
            CanCheck = (amountOwed == 0),
            CanCall = (amountOwed > 0 && player.ChipCount > amountOwed),
            CallAmount = amountOwed,
            MinRaise = Math.Min(minRaise, maxRaise),
            PotSizeRaise = Math.Min(Math.Max(potSizeRaise, minRaise), maxRaise),
            MaxRaise = maxRaise
        };
    }
}
