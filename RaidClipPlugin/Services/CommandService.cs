namespace RaidClipPlugin.Services;

public sealed class CommandService
{
    public event Func<string?, Task>? TestRequested;
    public event Action? StatusRequested;
    public event Action? QuitRequested;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        PrintHelp();

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("> ");

            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var command = parts[0].ToLowerInvariant();

            var argument = parts.Length > 1
                ? string.Join(' ', parts.Skip(1))
                : null;

            switch (command)
            {
                case "test":
                    if (TestRequested != null)
                        await TestRequested.Invoke(argument);
                    break;

                case "status":
                    StatusRequested?.Invoke();
                    break;

                case "quit":
                case "exit":
                case "q":
                    QuitRequested?.Invoke();
                    return;

                case "help":
                case "?":
                    PrintHelp();
                    break;

                default:
                    Console.WriteLine("Unbekannter Befehl.");
                    Console.WriteLine("Tippe 'help' für alle Befehle.");
                    break;
            }
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Verfügbare Befehle");
        Console.WriteLine("------------------");
        Console.WriteLine("test                -> Testclip des eigenen Kanals");
        Console.WriteLine("test <kanalname>    -> Testclip eines beliebigen Kanals");
        Console.WriteLine("status              -> Zeigt den aktuellen Status");
        Console.WriteLine("help                -> Zeigt diese Hilfe");
        Console.WriteLine("quit                -> Beendet das Plugin");
        Console.WriteLine();
    }
}