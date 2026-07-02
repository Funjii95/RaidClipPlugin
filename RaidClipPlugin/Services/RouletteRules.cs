namespace RaidClipPlugin.Services;

public enum RouletteBetKind
{
    Red,
    Black,
    Even,
    Odd,
    Low,
    High,
    Number
}

public sealed record RouletteBet(RouletteBetKind Kind, int? Number, string DisplayName);

public static class RouletteRules
{
    private static readonly HashSet<int> RedNumbers = new()
    {
        1, 3, 5, 7, 9, 12, 14, 16, 18,
        19, 21, 23, 25, 27, 30, 32, 34, 36
    };

    public static bool TryParseBet(string value, out RouletteBet bet)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant();
        bet = normalized switch
        {
            "rot" or "red" => new(RouletteBetKind.Red, null, "Rot"),
            "schwarz" or "black" => new(RouletteBetKind.Black, null, "Schwarz"),
            "gerade" or "even" => new(RouletteBetKind.Even, null, "Gerade"),
            "ungerade" or "odd" => new(RouletteBetKind.Odd, null, "Ungerade"),
            "niedrig" or "low" or "1-18" => new(RouletteBetKind.Low, null, "1–18"),
            "hoch" or "high" or "19-36" => new(RouletteBetKind.High, null, "19–36"),
            _ => null!
        };

        if (bet is not null) return true;
        if (int.TryParse(normalized, out var number) && number is >= 0 and <= 36)
        {
            bet = new RouletteBet(RouletteBetKind.Number, number, number.ToString());
            return true;
        }
        return false;
    }

    public static bool IsWinner(RouletteBet bet, int number) => bet.Kind switch
    {
        RouletteBetKind.Red => number > 0 && IsRed(number),
        RouletteBetKind.Black => number > 0 && !IsRed(number),
        RouletteBetKind.Even => number > 0 && number % 2 == 0,
        RouletteBetKind.Odd => number > 0 && number % 2 != 0,
        RouletteBetKind.Low => number is >= 1 and <= 18,
        RouletteBetKind.High => number is >= 19 and <= 36,
        RouletteBetKind.Number => bet.Number == number,
        _ => false
    };

    public static bool IsRed(int number) => RedNumbers.Contains(number);

    public static string ColorName(int number) => number == 0
        ? "Grün"
        : IsRed(number) ? "Rot" : "Schwarz";
}
