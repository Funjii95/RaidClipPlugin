using RaidClipPlugin.Config;

namespace RaidClipPlugin.Services;

internal sealed record ShellRewards(int White, int Blue, int Red, int Stones, long Pearls);
internal sealed record RpgShellOpenResult(bool Success, string Message, bool Duplicate = false, ShellRewards? Rewards = null);

internal sealed class RpgShellService(ViewerPointStore points, RpgConfig config,
    IDuelRandom? random = null, long maximumBalance = long.MaxValue)
{
    private readonly IDuelRandom rng = random ?? new CryptoDuelRandom();

    internal async Task<RpgShellOpenResult> OpenAsync(string user, string name, string actionId,
        int count, CancellationToken token)
    {
        if (!config.Enabled) return new(false, "RPG ist deaktiviert.");
        if (count is < 1 or > RpgShellOpeningRules.MaximumBatchSize)
            return new(false, "Bitte 1 bis 100 Muscheln auswählen.");
        if (maximumBalance < 0) throw new InvalidOperationException("Invalid maximum balance.");
        ShellRewards? rewards = null;
        var result = await points.ApplyRpgAsync(user, name, actionId, account =>
        {
            var stock = account.Rpg.Materials;
            stock.Validate();
            if (stock.Shells < count) return new(false, "Nicht genügend Muscheln; nichts wurde geöffnet.");
            var remaining = RpgShellOpeningRules.RemainingShells(stock.Shells, count);
            int white = 0, blue = 0, red = 0, stones = 0;
            long pearls = 0;
            for (var i = 0; i < count; i++)
                switch (RpgShellOpeningRules.Reward(rng.NextInclusive(1, 100)))
                {
                    case ShellRewardKind.WhitePearl: white++; break;
                    case ShellRewardKind.BluePearl: blue++; break;
                    case ShellRewardKind.RedPearl: red++; break;
                    case ShellRewardKind.StoneChips: stones++; break;
                    case ShellRewardKind.Pearls:
                        pearls = checked(pearls + rng.NextInclusive(RpgShellOpeningRules.MinimumPearlReward,
                            RpgShellOpeningRules.MaximumPearlReward));
                        break;
                }
            if (account.Points > maximumBalance || pearls > maximumBalance - account.Points)
                return new(false, "Perlenlimit würde überschritten; keine Muscheln verbraucht.");
            stock.Shells = checked((int)remaining);
            stock.WhitePearls = checked(stock.WhitePearls + white);
            stock.BluePearls = checked(stock.BluePearls + blue);
            stock.RedPearls = checked(stock.RedPearls + red);
            stock.StoneChips = checked(stock.StoneChips + stones);
            account.Points = checked(account.Points + pearls);
            rewards = new(white, blue, red, stones, pearls);
            return new(true);
        }, token);
        if (!result.Success) return new(false, result.Error, result.Duplicate);
        return new(true, $"{count} Muscheln geöffnet: {rewards!.White} weiße, {rewards.Blue} blaue, {rewards.Red} rote Upgrade-Perlen, {rewards.Pearls} Perlen und {rewards.Stones} Steinstückchen.", Rewards: rewards);
    }
}
