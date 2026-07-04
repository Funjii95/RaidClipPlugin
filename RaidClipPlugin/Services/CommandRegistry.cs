using System.Text;
using System.Text.Json;
using RaidClipPlugin.Config;
using RaidClipPlugin.Models;

namespace RaidClipPlugin.Services;

public sealed class CommandRegistry
{
    private readonly object _sync = new();
    private IReadOnlyList<ChatCommandDefinition> _commands = Array.Empty<ChatCommandDefinition>();
    public event Action? Changed;

    public IReadOnlyList<ChatCommandDefinition> Commands
    {
        get { lock (_sync) return _commands.ToArray(); }
    }

    public void Update(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var list = new List<ChatCommandDefinition>();
        void Add(string id, string command, IEnumerable<string>? aliases, string module,
            string moduleName, string name, string description, string usage,
            string example, bool enabled, CommandRole role = CommandRole.Viewer,
            int userCooldown = 0, int globalCooldown = 0, int cost = 0, int order = 0,
            bool visible = true)
        {
            var normalized = Normalize(command);
            if (normalized.Length == 0) return;
            if (config.Commands.CommandRoleOverrides.TryGetValue(id, out var roleOverride))
                role = ParseRole(roleOverride, role);
            list.Add(new ChatCommandDefinition(id, normalized,
                (aliases ?? Array.Empty<string>()).Select(Normalize)
                    .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                module, moduleName, name, description, usage, example, enabled,
                visible, role, TimeSpan.FromSeconds(Math.Max(0, userCooldown)),
                TimeSpan.FromSeconds(Math.Max(0, globalCooldown)), Math.Max(0, cost), order));
        }

        var m = config.Minigame;
        Add("points.de", "!punkte", null, "points", "Punkte", "Punktestand",
            "Zeigt deinen aktuellen Punktestand.", "!punkte", "!punkte",
            m.PointsEnabled && m.PointsCommandPunkteEnabled, userCooldown:m.PointsCommandCooldownSeconds, order:10);
        Add("points.en", "!points", null, "points", "Punkte", "Punktestand",
            "Zeigt deinen aktuellen Punktestand.", "!points", "!points",
            m.PointsEnabled && m.PointsCommandPointsEnabled, userCooldown:m.PointsCommandCooldownSeconds, order:11);
        Add("points.perlen", "!perlen", null, "points", "Punkte", "Punktestand",
            "Zeigt deinen aktuellen Punktestand.", "!perlen", "!perlen",
            m.PointsEnabled && m.PointsCommandPerlenEnabled, userCooldown:m.PointsCommandCooldownSeconds, order:12);
        if (!string.IsNullOrWhiteSpace(m.CustomPointsCommand))
            Add("points.custom", m.CustomPointsCommand, null, "points", "Punkte", "Punktestand",
                "Zeigt deinen aktuellen Punktestand.", m.CustomPointsCommand, m.CustomPointsCommand,
                m.PointsEnabled, userCooldown:m.PointsCommandCooldownSeconds, order:13);
        Add("points.daily", "!daily", null, "points", "Punkte", "Daily",
            "Holt den täglichen Punktebonus ab.", "!daily", "!daily", m.PointsEnabled && m.DailyEnabled, order:20);
        Add("points.top", "!top", new[]{"!rang"}, "points", "Punkte", "Rangliste",
            "Zeigt die Punkte-Rangliste.", "!top [Anzahl]", "!top 5", m.PointsEnabled && m.LeaderboardEnabled,
            userCooldown:m.LeaderboardCooldownSeconds, order:21);
        Add("points.profile", "!profil", null, "points", "Punkte", "Profil",
            "Zeigt dein Zuschauerprofil.", "!profil", "!profil", m.PointsEnabled && m.ProfileEnabled,
            userCooldown:m.ProfileCooldownSeconds, order:22);
        Add("points.give", "!give", null, "points", "Punkte", "Punkte schenken",
            "Schenkt einem Zuschauer eigene Punkte.", "!give @name <Betrag>", "!give @name 100", m.PointsEnabled, order:23);
        Add("points.add", "!addpoints", null, "points", "Punkte", "Punkte erzeugen",
            "Erzeugt Punkte für einen Nutzer oder alle gespeicherten Nutzer.", "!addpoints <@name|all> <Betrag>", "!addpoints all 100",
            m.PointsEnabled, CommandRole.Moderator, order:24);
        Add("points.remove", "!removepoints", null, "points", "Punkte", "Punkte zurücksetzen",
            "Setzt den Punktestand eines Nutzers auf null.", "!removepoints @name", "!removepoints @name",
            m.PointsEnabled, CommandRole.Broadcaster, order:25);
        Add("points.lurk", "!lurk", new[]{"!unlurk"}, "points", "Punkte", "Lurk",
            "Wechselt den Anwesenheitsstatus.", "!lurk", "!lurk", m.PointsEnabled, order:26);
        Add("casino.gamble", "!gamble", new[]{"!gambel"}, "casino", "Casino", "Gamble",
            "Würfelt mit einem Punkteinsatz.", "!gamble <Betrag|all>", "!gamble 100",
            m.Enabled && m.GambleEnabled, userCooldown:m.GambleCooldownSeconds, cost:m.MinimumBet, order:40);
        Add("casino.jackpot", "!jackpot", null, "casino", "Casino", "Jackpot",
            "Zeigt den aktuellen Jackpot.", "!jackpot", "!jackpot", m.Enabled && m.JackpotEnabled, order:41);
        Add("casino.coinflip", "!coinflip", null, "casino", "Casino", "Coinflip",
            "Wirft eine Münze.", "!coinflip <kopf|zahl> <Betrag>", "!coinflip kopf 100",
            m.Enabled && m.CoinflipEnabled, userCooldown:m.CoinflipCooldownSeconds, cost:m.CoinflipMinimumBet, order:42);
        Add("casino.slots", "!slots", null, "casino", "Casino", "Slots",
            "Spielt am Spielautomaten.", "!slots <Betrag>", "!slots 100",
            m.Enabled && m.SlotsEnabled, userCooldown:m.SlotsCooldownSeconds, cost:m.SlotsMinimumBet, order:43);
        Add("casino.roulette", "!roulette", null, "casino", "Casino", "Roulette",
            "Setzt Punkte beim Roulette.", "!roulette <Tipp> <Betrag>", "!roulette rot 100",
            m.Enabled && m.RouletteEnabled, userCooldown:m.RouletteCooldownSeconds, cost:m.RouletteMinimumBet, order:44);

        Add("heist.start", config.Heist.StartCommand, null, "heist", "Heist", "Heist starten",
            "Startet einen gemeinsamen Raubzug um den Jackpot.", config.Heist.StartCommand,
            config.Heist.StartCommand, config.Heist.Enabled, userCooldown:config.Heist.UserCooldownMinutes*60,
            globalCooldown:config.Heist.GlobalCooldownMinutes*60, order:60);
        Add("heist.join", config.Heist.JoinCommand, null, "heist", "Heist", "Heist beitreten",
            "Tritt der laufenden Beitrittsphase bei.", config.Heist.JoinCommand,
            config.Heist.JoinCommand, config.Heist.Enabled, order:61);
        Add("duel.challenge", config.Duel.DuelCommand, null, "duel", "Duel", "Duel starten",
            "Fordert einen Zuschauer zu einem Punkte-Duell heraus.", config.Duel.DuelCommand+" <user> <punkte|all>",
            config.Duel.DuelCommand+" Funjii 100", config.Duel.Enabled,
            userCooldown:config.Duel.UserCooldownSeconds, globalCooldown:config.Duel.GlobalCooldownSeconds,
            cost:config.Duel.MinimumBet, order:65);
        Add("duel.accept", config.Duel.AcceptCommand, null, "duel", "Duel", "Duel annehmen",
            "Nimmt eine offene Duel-Anfrage an.", config.Duel.AcceptCommand,
            config.Duel.AcceptCommand, config.Duel.Enabled, order:66);
        Add("duel.deny", config.Duel.DenyCommand, null, "duel", "Duel", "Duel ablehnen",
            "Lehnt eine offene Duel-Anfrage ab.", config.Duel.DenyCommand,
            config.Duel.DenyCommand, config.Duel.Enabled, order:67);
        Add("commands.list", config.Commands.Command, null, "commands", "Commands", "Command-Liste",
            "Zeigt verfügbare Chat-Commands.", config.Commands.Command+" [Seite|Modul]",
            config.Commands.Command+" heist", config.Commands.Enabled,
            userCooldown:config.Commands.UserCooldownSeconds,
            globalCooldown:config.Commands.GlobalCooldownSeconds, order:1);

        Add("clips.create", config.ClipCommand.Command, config.ClipCommand.Aliases, "clips", "Clips", "Clip erstellen",
            "Erstellt einen Twitch-Clip.", config.ClipCommand.Command+" [Titel]",
            config.ClipCommand.Command+" Highlight", config.ClipCommand.Enabled,
            userCooldown:config.ClipCommand.UserCooldownSeconds,
            globalCooldown:config.ClipCommand.GlobalCooldownSeconds, order:80);
        Add("discord.invite", config.DiscordClips.InviteCommand, null, "discord", "Discord", "Discord-Einladung",
            "Gibt den RaidClip-Discord-Link aus.", config.DiscordClips.InviteCommand,
            config.DiscordClips.InviteCommand, config.DiscordClips.InviteCommandEnabled,
            userCooldown:config.DiscordClips.InviteCooldownSeconds, order:90);

        foreach (var custom in config.Commands.CustomCommands)
        {
            var id = string.IsNullOrWhiteSpace(custom.Id)
                ? Guid.NewGuid().ToString("N") : custom.Id.Trim();
            Add("custom." + id, custom.Command, custom.Aliases, "custom", "Custom Commands",
                custom.Command, "Eigene Chatantwort: " + LimitDescription(custom.Response),
                custom.Command, custom.Command,
                config.Commands.CustomCommandsEnabled && custom.Enabled,
                ParseRole(custom.RequiredRole),
                userCooldown: custom.UserCooldownSeconds,
                globalCooldown: custom.GlobalCooldownSeconds,
                order: 70);
        }

        var music=config.MusicRequests;
        Add("music.request", music.ChatCommand, music.ChatCommandAliases, "music", "Musik", "Musikwunsch",
            "Wünscht einen Song.", music.ChatCommand+" <Song|Spotify-Link>", music.ChatCommand+" Songname",
            music.Enabled && music.ChatCommandEnabled, userCooldown:music.UserCooldownMinutes*60, order:100);
        Add("music.song", music.ModeratorCommands.Song, null, "music", "Musik", "Aktueller Song",
            "Zeigt den aktuellen Song.", music.ModeratorCommands.Song, music.ModeratorCommands.Song,
            music.Enabled && music.ModeratorCommands.SongEnabled, order:101);
        foreach (var item in new[]{
            ("skip",music.ModeratorCommands.Skip,music.ModeratorCommands.SkipEnabled,"Song überspringen"),
            ("queue",music.ModeratorCommands.Queue,music.ModeratorCommands.QueueEnabled,"Warteschlange"),
            ("remove",music.ModeratorCommands.Remove,music.ModeratorCommands.RemoveEnabled,"Song entfernen"),
            ("pause",music.ModeratorCommands.Pause,music.ModeratorCommands.PauseEnabled,"Wiedergabe pausieren"),
            ("resume",music.ModeratorCommands.Resume,music.ModeratorCommands.ResumeEnabled,"Wiedergabe fortsetzen")})
            Add("music."+item.Item1,item.Item2,null,"music","Musik",item.Item4,item.Item4,item.Item2,item.Item2,
                music.Enabled&&item.Item3,CommandRole.Moderator,order:102);

        var giveaway=config.Giveaways;
        Add("giveaway.join",giveaway.Command,giveaway.Aliases,"giveaway","Giveaway","Giveaway-Teilnahme",
            "Nimmt am aktiven Giveaway teil.",giveaway.Command,giveaway.Command,giveaway.Enabled,
            cost:giveaway.EntryCost,order:120);
        if(giveaway.ModeratorCommands.Enabled)
            foreach(var command in new[]{giveaway.ModeratorCommands.Start,giveaway.ModeratorCommands.Stop,
                giveaway.ModeratorCommands.Pause,giveaway.ModeratorCommands.Resume,giveaway.ModeratorCommands.Draw,
                giveaway.ModeratorCommands.Reroll,giveaway.ModeratorCommands.Status})
                Add("giveaway.mod."+command.Replace(" ","."),command,null,"giveaway","Giveaway","Giveaway verwalten",
                    "Verwaltet das Giveaway.",command,command,giveaway.Enabled,CommandRole.Moderator,order:121);

        lock (_sync) _commands=list.OrderBy(x=>x.SortOrder).ThenBy(x=>x.CommandText).ToArray();
        Changed?.Invoke();
    }

    public IReadOnlyList<CommandCollision> FindCollisions(bool includeDisabled = true)
    {
        var owners=new Dictionary<string,ChatCommandDefinition>(StringComparer.OrdinalIgnoreCase);
        var collisions=new List<CommandCollision>();
        foreach(var definition in Commands.Where(x => includeDisabled || x.Enabled))
        foreach(var text in new[]{definition.CommandText}.Concat(definition.Aliases))
        {
            var normalized=Normalize(text);
            if(owners.TryGetValue(normalized,out var owner) && owner.CommandId!=definition.CommandId)
                collisions.Add(new CommandCollision(normalized,owner,definition));
            else owners.TryAdd(normalized,definition);
        }
        return collisions.DistinctBy(x=>(x.Command,x.First.ModuleId,x.Second.ModuleId)).ToArray();
    }

    public IReadOnlyList<ChatCommandDefinition> VisibleFor(ChatMessage user) => Commands
        .Where(x=>x.Enabled&&x.IsVisible&&HasRole(user,x.RequiredRole)).ToArray();

    public static bool HasRole(ChatMessage user,CommandRole role)=>role switch
    {
        CommandRole.Broadcaster=>user.IsBroadcaster,
        CommandRole.Moderator=>user.IsBroadcaster||user.IsModerator,
        CommandRole.Vip=>user.IsBroadcaster||user.IsModerator||user.IsVip,
        CommandRole.Subscriber=>user.IsBroadcaster||user.IsModerator||user.IsVip||user.IsSubscriber,
        _=>true
    };

    public async Task ExportAsync(string path,bool json,CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)??".");
        if(json)
            await File.WriteAllTextAsync(path,JsonSerializer.Serialize(Commands,new JsonSerializerOptions{WriteIndented=true}),cancellationToken);
        else
        {
            var text=string.Join(Environment.NewLine+Environment.NewLine,Commands.GroupBy(x=>x.ModuleDisplayName)
                .Select(g=>g.Key+":"+Environment.NewLine+string.Join(Environment.NewLine,g.Select(x=>$"{x.Usage} – {x.Description}"))));
            await File.WriteAllTextAsync(path,text,cancellationToken);
        }
        Console.WriteLine("Command-Export erstellt: "+path);
    }

    public static CommandRole ParseRole(string? value, CommandRole fallback = CommandRole.Viewer) =>
        Enum.TryParse<CommandRole>((value ?? "").Trim(), true, out var role)
            ? role : fallback;

    private static string LimitDescription(string? value)
    {
        var text = (value ?? "").Trim();
        return text.Length <= 90 ? text : text[..87] + "…";
    }

    public static string Normalize(string? command)
    {
        var value=(command??"").Trim().ToLowerInvariant();
        if(value.Length==0)return "";
        value=string.Join(' ',value.Split(' ',StringSplitOptions.RemoveEmptyEntries));
        return value.StartsWith('!')?value:"!"+value;
    }
}
