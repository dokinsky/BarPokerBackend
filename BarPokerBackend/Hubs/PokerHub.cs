using BarPokerBackend.Models;
using BarPokerBackend.Services;
using Microsoft.AspNetCore.SignalR;

namespace BarPokerBackend.Hubs;

public class PokerHub : Hub
{
    public async Task JoinTable(string tableId, string playerName)
    {
        int? assignedSeat = PokerGameEngine.AssignNextAvailableSeat(tableId, Context.ConnectionId, playerName);
        if (assignedSeat == null)
        {
            await Clients.Caller.SendAsync("Error", "Table is full!");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, tableId);
        await BroadcastFilteredStatesToTable(tableId);
    }

    public async Task SubmitAction(string tableId, PokerActionType action, int amount)
    {
        PokerGameEngine.ProcessAction(tableId, Context.ConnectionId, action, amount);
        await BroadcastFilteredStatesToTable(tableId);
    }

    public async Task StartGame(string tableId)
    {
        PokerGameEngine.StartNewHand(tableId);
        await BroadcastFilteredStatesToTable(tableId);
    }

    // Helper to send targeted, filtered state updates to every connected client individually
    private async Task BroadcastFilteredStatesToTable(string tableId)
    {
        var table = PokerGameEngine.GetTable(tableId);

        // Send a custom filtered payload to every connection mapped to this table
        foreach (var connId in table.ConnectionToSeatMap.Keys)
        {
            var filteredState = PokerGameEngine.GetFilteredStateForConnection(tableId, connId);
            await Clients.Client(connId).SendAsync("ReceiveTableUpdate", filteredState);
        }
    }
}