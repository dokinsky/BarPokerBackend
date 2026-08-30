using BarPokerBackend.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace BarPokerBackend.Services;

public class PokerGameEngine
{
    private static readonly ConcurrentDictionary<string, TableState> _tables = new();
    private const int MaxSeats = 9;

    public static TableState GetTable(string tableId)
    {
        return _tables.GetOrAdd(tableId, id => new TableState { TableId = id });
    }

    // Automatically assign the next available seat to this connection
    public static int? AssignNextAvailableSeat(string tableId, string connectionId, string playerName)
    {
        var table = GetTable(tableId);

        lock (table)
        {
            // Check if this connection is already seated
            if (table.ConnectionToSeatMap.TryGetValue(connectionId, out int existingSeat))
            {
                return existingSeat;
            }

            // Find the first open seat from 1 to MaxSeats
            for (int seat = 1; seat <= MaxSeats; seat++)
            {
                if (!table.Players.ContainsKey(seat))
                {
                    table.Players[seat] = new Player
                    {
                        SeatNumber = seat,
                        Name = playerName,
                        ChipCount = 1000
                    };
                    table.ConnectionToSeatMap[connectionId] = seat;
                    return seat;
                }
            }
        }

        return null; 
    }

    public static void RemovePlayerByConnection(string tableId, string connectionId)
    {
        var table = GetTable(tableId);
        lock (table)
        {
            if (table.ConnectionToSeatMap.TryRemove(connectionId, out int seat))
            {
                table.Players.Remove(seat);
            }
        }
    }

    public static TableState ProcessAction(string tableId, string connectionId, PokerActionType action, int amount)
    {
        var table = GetTable(tableId);

        if (!table.ConnectionToSeatMap.TryGetValue(connectionId, out int seatNumber)) return table;
        if (table.ActiveSeatTurn != seatNumber) return table; // Not your turn!

        if (!table.Players.TryGetValue(seatNumber, out var player)) return table;

        int amountOwed = table.CurrentHighestBet - player.CurrentBet;
        int minRaise = table.CurrentHighestBet == 0 ? table.BigBlindAmount : table.CurrentHighestBet * 2;
        int maxRaise = player.ChipCount + player.CurrentBet;
        int potRaise = table.Pot + amountOwed;

        switch (action)
        {
            case PokerActionType.Fold:
                player.HasFolded = true;
                break;

            case PokerActionType.Check:
                if (amountOwed > 0) return table; // Invalid: cannot check facing a bet
                break;

            case PokerActionType.Call:
                int callCost = Math.Min(player.ChipCount, amountOwed);
                player.ChipCount -= callCost;
                player.CurrentBet += callCost;
                table.Pot += callCost;
                break;

            case PokerActionType.MinRaise:
                ExecuteRaise(player, table, minRaise);
                break;

            case PokerActionType.PotRaise:
                ExecuteRaise(player, table, Math.Min(potRaise, maxRaise));
                break;

            case PokerActionType.AllIn:
                ExecuteRaise(player, table, maxRaise);
                break;

            case PokerActionType.CustomRaise:
                if (amount < minRaise || amount > maxRaise) return table; // Invalid range
                ExecuteRaise(player, table, amount);
                break;
        }

        AdvanceTurn(table);
        return table;
    }

    // Updated helper method passing the table object directly instead of ref properties
    private static void ExecuteRaise(Player player, TableState table, int targetBetAmount)
    {
        int raiseCost = targetBetAmount - player.CurrentBet;
        if (player.ChipCount < raiseCost) return; // Not enough chips

        player.ChipCount -= raiseCost;
        player.CurrentBet = targetBetAmount;
        table.CurrentHighestBet = targetBetAmount;
        table.Pot += raiseCost;
    }
    public static TableState StartNewHand(string tableId)
    {
        var table = GetTable(tableId);
        lock (table)
        {
            var activeSeats = table.Players.Keys.OrderBy(s => s).ToList();
            if (activeSeats.Count < 2) return table;

            table.GamePhase = "PreFlop";
            table.Pot = 0;
            table.CommunityCards.Clear();
            table.CurrentHighestBet = table.BigBlindAmount;
            table.Deck.InitializeAndShuffle();

            // 1. Rotate Dealer Button
            if (table.DealerButtonSeat == 0 || !activeSeats.Contains(table.DealerButtonSeat))
            {
                table.DealerButtonSeat = activeSeats.First();
            }
            else
            {
                int currentDealerIndex = activeSeats.IndexOf(table.DealerButtonSeat);
                table.DealerButtonSeat = activeSeats[(currentDealerIndex + 1) % activeSeats.Count];
            }

            // 2. Reset Player Hand States & Deal Cards
            foreach (var player in table.Players.Values)
            {
                player.HasFolded = false;
                player.CurrentBet = 0;
                player.HoleCards = table.Deck.DealCards(2);
            }

            // 3. Post Blinds (Simplified 2-player vs Multi-player positioning)
            // Heads-up: Dealer is Small Blind. 3+ players: SB is left of dealer, BB is left of SB.
            int sbSeat, bbSeat, firstActorSeat;
            if (activeSeats.Count == 2)
            {
                sbSeat = table.DealerButtonSeat;
                bbSeat = activeSeats.First(s => s != sbSeat);
                firstActorSeat = sbSeat; // Pre-flop, dealer/SB acts first in heads-up
            }
            else
            {
                int dealerIndex = activeSeats.IndexOf(table.DealerButtonSeat);
                sbSeat = activeSeats[(dealerIndex + 1) % activeSeats.Count];
                bbSeat = activeSeats[(dealerIndex + 2) % activeSeats.Count];
                // Pre-flop first actor is left of Big Blind (UTG)
                firstActorSeat = activeSeats[(dealerIndex + 3) % activeSeats.Count];
            }

            // Apply Small Blind
            var sbPlayer = table.Players[sbSeat];
            int sbPost = Math.Min(sbPlayer.ChipCount, table.SmallBlindAmount);
            sbPlayer.ChipCount -= sbPost;
            sbPlayer.CurrentBet = sbPost;
            table.Pot += sbPost;

            // Apply Big Blind
            var bbPlayer = table.Players[bbSeat];
            int bbPost = Math.Min(bbPlayer.ChipCount, table.BigBlindAmount);
            bbPlayer.ChipCount -= bbPost;
            bbPlayer.CurrentBet = bbPost;
            table.Pot += bbPost;

            table.ActiveSeatTurn = firstActorSeat;
        }
        return table;
    }

    
    private static void AdvanceTurn(TableState table)
    {
        var activeSeats = table.Players.Keys.Where(s => !table.Players[s].HasFolded).OrderBy(s => s).ToList();

        // Check if only 1 player remains (everyone else folded)
        if (activeSeats.Count == 1)
        {
            EndHandPrematurely(table, activeSeats.First());
            return;
        }

        int currentIndex = activeSeats.IndexOf(table.ActiveSeatTurn);

        // Check if betting round is complete (all active players have equal bets and have acted)
        bool roundComplete = activeSeats.All(s => table.Players[s].CurrentBet == table.CurrentHighestBet);

        if (roundComplete || currentIndex == -1 || currentIndex >= activeSeats.Count - 1)
        {
            // Advance street
            if (!AdvanceBettingRound(table))
            {
                return; // Hand reached showdown / ended
            }
            // Set first actor for the new street (usually first active player left of dealer)
            table.ActiveSeatTurn = activeSeats.First();
        }
        else
        {
            table.ActiveSeatTurn = activeSeats[currentIndex + 1];
        }
    }

    private static bool AdvanceBettingRound(TableState table)
    {
        foreach (var p in table.Players.Values)
        {
            p.CurrentBet = 0;
        }
        table.CurrentHighestBet = 0;

        switch (table.GamePhase)
        {
            case "PreFlop":
                table.GamePhase = "Flop";
                table.CommunityCards.AddRange(table.Deck.DealCards(3));
                return true;
            case "Flop":
                table.GamePhase = "Turn";
                table.CommunityCards.AddRange(table.Deck.DealCards(1));
                return true;
            case "Turn":
                table.GamePhase = "River";
                table.CommunityCards.AddRange(table.Deck.DealCards(1));
                return true;
            case "River":
                table.GamePhase = "Showdown";
                EvaluateShowdown(table);
                return false; // Hand over
            default:
                return false;
        }
    }

    private static void EndHandPrematurely(TableState table, int winningSeat)
    {
        table.GamePhase = "Waiting";
        table.Players[winningSeat].ChipCount += table.Pot;
        table.Pot = 0;
    }

    private static void EvaluateShowdown(TableState table)
    {
        var activePlayers = table.Players.Values.Where(p => !p.HasFolded).ToList();
        if (!activePlayers.Any()) return;

        Player bestPlayer = null;
        EvaluatedHand bestHandScore = null;

        foreach (var player in activePlayers)
        {
            // Evaluate the player's 7-card hand (Hole cards + Community cards)
            var playerHandScore = HandEvaluator.EvaluateBestHand(player.HoleCards, table.CommunityCards);

            if (bestHandScore == null || playerHandScore.CompareTo(bestHandScore) > 0)
            {
                bestHandScore = playerHandScore;
                bestPlayer = player;
            }
        }

        // Award pot to the winner
        if (bestPlayer != null)
        {
            bestPlayer.ChipCount += table.Pot;
            // Optionally store a message in table state indicating who won and with what hand rank
        }

        table.Pot = 0;
        table.GamePhase = "Waiting";
    }

    public static ClientTableStateDto GetFilteredStateForConnection(string tableId, string connectionId)
    {
        var table = GetTable(tableId);
        var clientState = new ClientTableStateDto
        {
            TableId = table.TableId,
            GamePhase = table.GamePhase,
            Pot = table.Pot,
            CurrentHighestBet = table.CurrentHighestBet,
            ActiveSeatTurn = table.ActiveSeatTurn,
            CommunityCards = new List<string>(table.CommunityCards)
        };

        table.ConnectionToSeatMap.TryGetValue(connectionId, out int viewerSeat);

        foreach (var kvp in table.Players)
        {
            int seatNum = kvp.Key;
            var player = kvp.Value;

            var clientPlayer = new ClientPlayerDto
            {
                SeatNumber = player.SeatNumber,
                Name = player.Name,
                ChipCount = player.ChipCount,
                CurrentBet = player.CurrentBet,
                HasFolded = player.HasFolded
            };

            // Handle private hole cards privacy
            if (seatNum == viewerSeat || table.GamePhase == "Showdown")
            {
                clientPlayer.HoleCards = new List<string>(player.HoleCards);
            }
            else
            {
                clientPlayer.HoleCards = new List<string> { "🎴", "🎴" };
            }

            // ---> PUT YOUR ACTION-FLAG LOGIC RIGHT HERE <---
            int amountOwed = table.CurrentHighestBet - player.CurrentBet;
            bool isMyTurn = (table.ActiveSeatTurn == seatNum && table.GamePhase != "Waiting");

            if (isMyTurn)
            {
                clientPlayer.CanFold = (amountOwed > 0);
                clientPlayer.CanCheck = (amountOwed == 0);
                clientPlayer.CanCall = (amountOwed > 0 && player.ChipCount >= amountOwed);
                clientPlayer.CanRaise = (player.ChipCount > amountOwed);

                clientPlayer.CallAmount = amountOwed;
                clientPlayer.MinRaiseAmount = table.CurrentHighestBet == 0 ? table.BigBlindAmount : table.CurrentHighestBet * 2;
                clientPlayer.PotRaiseAmount = table.Pot + amountOwed;
            }

            clientState.Players[seatNum] = clientPlayer;
        }

        return clientState;
    }


}
