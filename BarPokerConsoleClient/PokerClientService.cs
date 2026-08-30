using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace BarPokerConsoleClient;

public class PokerClientService
{
    private readonly HubConnection _connection;
    private readonly string _tableId;
    private readonly string _playerName;

    public event Action<JsonElement>? OnStateUpdated;
    public event Action<string>? OnErrorReceived;

    public PokerClientService(string hubUrl, string tableId, string playerName)
    {
        _tableId = tableId;
        _playerName = playerName;

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = (message) =>
                {
                    if (message is HttpClientHandler clientHandler)
                        clientHandler.ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    return message;
                };
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<JsonElement>("ReceiveTableUpdate", (state) => OnStateUpdated?.Invoke(state));
        _connection.On<string>("Error", (msg) => OnErrorReceived?.Invoke(msg));
    }

    public async Task StartAsync()
    {
        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinTable", _tableId, _playerName);
    }

    public async Task SendActionAsync(int actionType, int amount = 0)
    {
        await _connection.InvokeAsync("SubmitAction", _tableId, actionType, amount);
    }

    public async Task SendStartGameAsync()
    {
        await _connection.InvokeAsync("StartGame", _tableId);
    }
}