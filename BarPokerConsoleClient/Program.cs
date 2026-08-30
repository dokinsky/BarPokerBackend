namespace BarPokerConsoleClient;

class Program
{
    private static int? _myAssignedSeat = null;
    private static string _hubUrl = "https://localhost:7181/pokerhub";
    private static string _tableId = "Table-1";

    static async Task Main(string[] args)
    {
        Console.Write("Enter your Player Name for this terminal: ");
        string playerName = Console.ReadLine() ?? "Player";

        var clientService = new PokerClientService(_hubUrl, _tableId, playerName);

        // Keep track of the latest phase so the menu knows what options to show
        string currentPhase = "Waiting";

        clientService.OnStateUpdated += (stateJson) =>
        {
            // Grab the current phase from the state for our input menu logic
            currentPhase = stateJson.GetProperty("gamePhase").GetString() ?? "Waiting";
            TerminalUI.Render(stateJson, playerName, out _, out _myAssignedSeat);
        };

        clientService.OnErrorReceived += (errorMsg) =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] {errorMsg}");
            Console.ResetColor();
        };

        try
        {
            Console.WriteLine("Connecting to poker backend...");
            await clientService.StartAsync();
            Console.WriteLine("Connected successfully!");

            bool running = true;
            while (running)
            {
                string input = Console.ReadLine()?.Trim() ?? "";

                if (string.IsNullOrEmpty(input)) continue;

                if (input.Equals("0", StringComparison.OrdinalIgnoreCase) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    running = false;
                    break;
                }

                // Handle WAITING phase menu options
                if (currentPhase.Equals("Waiting", StringComparison.OrdinalIgnoreCase))
                {
                    if (input == "1") // Start Game
                    {
                        await clientService.SendStartGameAsync();
                    }
                    else
                    {
                        Console.WriteLine("Invalid option. Choose 1 to start or 0 to exit.");
                    }
                    continue;
                }

                // Handle ACTIVE GAME phase menu options (Fold, Check, Call, Raise)
                if (_myAssignedSeat == null)
                {
                    Console.WriteLine("Waiting for seat assignment...");
                    continue;
                }

                int actionType = -1; // 0: Fold, 1: Check, 2: Call, 3: Raise
                int amount = 0;

                switch (input)
                {
                    case "1": // Fold
                        actionType = 0;
                        break;
                    case "2": // Check
                        actionType = 1;
                        break;
                    case "3": // Call
                        actionType = 2;
                        break;
                    case "4": // Raise
                        actionType = 3;
                        Console.Write("Enter total raise amount (e.g. 200): ");
                        if (int.TryParse(Console.ReadLine(), out int raiseAmount))
                        {
                            amount = raiseAmount;
                        }
                        else
                        {
                            Console.WriteLine("Invalid amount. Action cancelled.");
                            continue;
                        }
                        break;
                    default:
                        Console.WriteLine("Unknown selection. Choose a valid menu number.");
                        continue;
                }

                await clientService.SendActionAsync(actionType, amount);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
        }
    }
}