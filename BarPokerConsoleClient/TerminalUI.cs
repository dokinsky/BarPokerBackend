using System.Text.Json;

namespace BarPokerConsoleClient;

public static class TerminalUI
{
    public static void Render(JsonElement state, string localPlayerName, out List<string> myHoleCards, out int? assignedSeat)
    {
        Console.Clear();
        myHoleCards = new List<string>();
        assignedSeat = null;

        string phase = state.GetProperty("gamePhase").GetString() ?? "Waiting";
        int pot = state.GetProperty("pot").GetInt32();
        int highestBet = state.GetProperty("currentHighestBet").GetInt32();
        int activeTurn = state.GetProperty("activeSeatTurn").GetInt32();

        var communityCardsList = new List<string>();
        foreach (var card in state.GetProperty("communityCards").EnumerateArray())
        {
            communityCardsList.Add(card.GetString());
        }

        Console.WriteLine("==================================================");
        Console.WriteLine($" TABLE STATUS | Phase: {phase.ToUpper()} | Pot: ${pot} | Highest Bet: ${highestBet}");
        Console.WriteLine($" Community Cards: [ {string.Join(" ", communityCardsList)} ]");
        Console.WriteLine("==================================================");
        Console.WriteLine(" PLAYERS AT TABLE:");

        var playersProp = state.GetProperty("players");

        // Track if it's our turn and capture local player permissions
        bool isMyTurn = false;
        JsonElement myPlayerDto = default;

        foreach (JsonProperty playerEntry in playersProp.EnumerateObject())
        {
            int seatNum = int.Parse(playerEntry.Name);
            var pData = playerEntry.Value;
            string name = pData.GetProperty("name").GetString();
            int chips = pData.GetProperty("chipCount").GetInt32();
            int currentBet = pData.GetProperty("currentBet").GetInt32();
            bool folded = pData.GetProperty("hasFolded").GetBoolean();

            if (name.Equals(localPlayerName, StringComparison.OrdinalIgnoreCase))
            {
                assignedSeat = seatNum;
                myPlayerDto = pData;
                if (seatNum == activeTurn && !phase.Equals("Waiting", StringComparison.OrdinalIgnoreCase))
                {
                    isMyTurn = true;
                }
            }

            List<string> cards = new();
            if (pData.TryGetProperty("holeCards", out var cardsArr))
            {
                foreach (var c in cardsArr.EnumerateArray())
                {
                    cards.Add(c.GetString());
                }
            }

            if (cards.Count > 0 && cards[0] != "🎴")
            {
                myHoleCards = cards;
            }

            string turnIndicator = (seatNum == activeTurn && phase != "Waiting") ? " <-- [ACTIVE TURN]" : "";
            string foldStatus = folded ? " [FOLDED]" : "";
            string cardDisplay = cards.Count > 0 ? $"[ {string.Join(" ", cards)} ]" : "";

            Console.WriteLine($" Seat {seatNum}: {name} | Chips: ${chips} | Bet: ${currentBet} {cardDisplay}{foldStatus}{turnIndicator}");
        }

        Console.WriteLine("--------------------------------------------------");

        if (myHoleCards.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($" YOUR PRIVATE HOLE CARDS: [ {string.Join(" ", myHoleCards)} ]");
            Console.ResetColor();
        }

        Console.WriteLine("\n[AVAILABLE ACTIONS]:");

        if (phase.Equals("Waiting", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(" [1] Start Game");
            Console.WriteLine(" [0] Exit");
        }
        else if (!isMyTurn)
        {
            Console.WriteLine(" (Waiting for your turn...)");
            Console.WriteLine(" [0] Exit");
        }
        else
        {
            // Render only server-authorized options based on server action flags
            bool canFold = myPlayerDto.TryGetProperty("canFold", out var f) && f.GetBoolean();
            bool canCheck = myPlayerDto.TryGetProperty("canCheck", out var c) && c.GetBoolean();
            bool canCall = myPlayerDto.TryGetProperty("canCall", out var ca) && ca.GetBoolean();
            bool canRaise = myPlayerDto.TryGetProperty("canRaise", out var r) && r.GetBoolean();

            int callAmount = myPlayerDto.TryGetProperty("callAmount", out var co) ? co.GetInt32() : 0;
            int minRaise = myPlayerDto.TryGetProperty("minRaiseAmount", out var mr) ? mr.GetInt32() : 0;
            int potRaise = myPlayerDto.TryGetProperty("potRaiseAmount", out var pr) ? pr.GetInt32() : 0;

            if (canFold) Console.WriteLine(" [1] Fold");
            if (canCheck) Console.WriteLine(" [2] Check");
            if (canCall) Console.WriteLine($" [3] Call (${callAmount})");
            if (canRaise)
            {
                Console.WriteLine($" [4] Min Raise (${minRaise})");
                Console.WriteLine($" [5] Pot-Size Raise (${potRaise})");
                Console.WriteLine(" [6] All-In");
            }
            Console.WriteLine(" [0] Exit");
        }

        Console.Write("\nSelect option [Number] > ");
    }
}