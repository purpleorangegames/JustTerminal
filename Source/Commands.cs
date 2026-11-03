using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace JustTerminal;

public class Command
{
    public string name;
    public string args = "";
    public string help;
    public string alias;
    public bool edit;
    // left out of help, tab and /bin
    public bool hidden;

    public string Key => (edit ? "edit:" : "") + name;

    // the name the player gave it in the mod options, or its own
    public string Shown => JustTerminalMod.Settings.renames.TryGetValue(Key, out string n) && !string.IsNullOrWhiteSpace(n) ? n.Trim() : name;

    public bool Matches(string word) =>
        Shown.Equals(word, StringComparison.OrdinalIgnoreCase) || name.Equals(word, StringComparison.OrdinalIgnoreCase) || alias != null && alias.Equals(word, StringComparison.OrdinalIgnoreCase);
    // example lines and what each does, shown by help <command>
    public (string line, string does)[] examples;
    public Action<Shell, string[]> run;
    // what tab offers, gets the words already typed after the command
    public Func<string[], List<string>> complete;
}

// everything here only reads the game
public static class Commands
{
    public static readonly List<Command> All = new List<Command>
    {
        new Command { name = "help", args = "[command]", examples = new[] { ("help", "list the commands"), ("help graph", "how graph works, with examples") }, complete = done => done.Length == 0 ? All.Select(c => c.Shown).ToList() : new List<string>(), help = "list the commands", run = Help },
        new Command { name = "clear", examples = new[] { ("clear", "clears the screen") }, help = "clear the screen (ctrl+l)", run = (sh, _) => sh.Clear() },
        new Command { name = "fetch", examples = new[] { ("fetch", "colony info next to a little picture") }, help = "colony info with a picture", run = Programs.Runner("fetch") },
        new Command { name = "colony", examples = new[] { ("colony", "name, storyteller, colonists, wealth and more") }, help = "colony summary", run = Programs.Runner("colony") },
        new Command { name = "date", examples = new[] { ("date", "date, hour and season where your colony is") }, help = "date, season and time", run = Programs.Runner("date") },
        new Command { name = "pawns", examples = new[] { ("pawns", "everyone with mood, health and what they're doing") }, help = "list the colonists", run = Programs.Runner("pawns") },
        new Command { name = "pawn", examples = new[] { ("pawn Tasca", "Tasca's needs, skills and traits"), ("pawn Red_Tasca", "use _ for spaces in names") }, args = "<name>", help = "one colonist in detail", run = Programs.Runner("pawn"), complete = done => done.Length == 0 ? ColonistNames() : new List<string>() },
        new Command { name = "rimtop", examples = new[] { ("top", "opens the monitor, q leaves it"), ("top, then tab", "one box on the whole screen"), ("top, then enter", "details of the selected colonist") }, alias = "top", help = "colony monitor like btop, q quits", run = (sh, _) => sh.App = new TopApp() },
        new Command { name = "graph", examples = new[] { ("graph temp", "temperature, 3 days back and 1 ahead"), ("graph temp 7 2", "7 days back and 2 ahead"), ("graph mood Tasca 3", "Tasca's mood the last 3 days"), ("graph wealth 30", "wealth the last 30 days") }, args = "<what> [days]", help = "graphs over time, graph alone lists them", run = GraphCommand.Run, complete = GraphCommand.Complete },
        new Command { name = "critical", examples = new[] { ("critical", "everyone with wounds no one has treated yet, worst bleeding first") }, help = "wounds not treated yet", run = Programs.Runner("critical") },
        new Command { name = "checkup", examples = new[] { ("checkup", "diseases with sick and immune bars, and treated wounds") }, help = "diseases and treated wounds", run = Programs.Runner("checkup") },
        new Command { name = "visitors", examples = new[] { ("visitors", "traders, guests and other people on the map, by faction") }, help = "people on the map from other factions", run = Programs.Runner("visitors") },
        new Command { name = "danger", examples = new[] { ("danger", "enemies and wild predators, closest to a colonist first") }, help = "enemies and predators on the map", run = Programs.Runner("danger") },
        new Command { name = "idle", alias = "wander", examples = new[] { ("idle", "colonists with nothing to do"), ("wander", "the same") }, help = "colonists with nothing to do", run = Programs.Runner("idle") },
        new Command { name = "attention", examples = new[] { ("attention", "the game's alerts, wounds no one treated and idle colonists") }, help = "what needs you now", run = Programs.Runner("attention") },
        new Command { name = "why", args = "<name>", help = "what a colonist is thinking, worst first", run = Programs.Runner("why"), complete = done => done.Length == 0 ? ColonistNames() : new List<string>(), examples = new[] { ("why Tasca", "each thought with how much mood it gives or takes") } },
        new Command { name = "rooms", help = "the rooms with temperature, cleanliness and impressiveness", run = Programs.Runner("rooms"), examples = new[] { ("rooms", "every room, bedrooms say whose"), ("rooms | grep bedroom", "only the bedrooms") } },
        new Command { name = "trade", args = "[number]", help = "the traders on the map and what they carry", run = Programs.Runner("trade"), examples = new[] { ("trade", "the traders on the map"), ("trade 1", "what the first one carries") } },
        new Command { name = "caravans", help = "your caravans, where they go and their food", run = Programs.Runner("caravans"), examples = new[] { ("caravans", "each caravan with where it's going and when it gets there") } },
        new Command { name = "bills", help = "the workbenches and their bills", run = Programs.Runner("bills"), examples = new[] { ("bills", "each workbench with its bills") } },
        new Command { name = "storage", args = "[save|apply|name ...]", help = "stockpiles and shelves, save what one takes and put it on others", run = Programs.Runner("storage"), examples = new[] { ("storage", "lists them with their room and priority"), ("storage save 3 meals", "keeps what 3 takes in ~/storage/meals"), ("storage apply meals shelves", "in edit mode, every shelf takes the same"), ("storage apply meals kitchen", "the stockpiles in kitchens"), ("storage apply meals group_1", "a link or zone by its name"), ("storage name 4 Food shelves", "in edit mode, renames a link or zone") }, complete = done => done.Length == 0 ? new List<string> { "save", "apply", "name" } : new List<string>() },
        new Command { name = "look", args = "<name>", help = "the camera goes to someone", run = Look, complete = done => done.Length == 0 ? ColonistNames() : new List<string>(), examples = new[] { ("look Tasca", "the camera goes to Tasca and selects her"), ("action letter {who} is down; look {who}", "in a when script") } },
        new Command { name = "factions", examples = new[] { ("factions", "every faction, how they see us and their leader") }, help = "factions and how they see us", run = Programs.Runner("factions") },
        new Command { name = "history", examples = new[] { ("history", "what you typed, up/down brings it back") }, help = "commands typed so far", run = History },
        new Command { name = "color", examples = new[] { ("color amber", "amber screen"), ("color", "shows the color in use") }, args = "<green|amber|white|custom>", help = "change the screen color, custom is set in the mod options", run = ColorCmd, complete = done => done.Length == 0 ? Phosphor.All.Select(p => p.name).Append("custom").ToList() : new List<string>() },
        new Command { name = "mod_options", examples = new[] { ("mod_options", "opens this mod's options: colors, font, effects, texts and command names") }, help = "open this mod's options", run = (sh, _) => JustTerminalMod.OpenOptions() },
        new Command { name = "ls", args = "[folder]", help = "files and folders here", run = Files.Ls, complete = done => done.Length == 0 ? Files.Names(Terminal.Shell) : new List<string>(), examples = new[] { ("ls", "what's in this folder"), ("ls notes", "what's in notes") } },
        new Command { name = "cd", args = "[folder]", help = "go into a folder, cd alone goes back to ~", run = Files.Cd, complete = done => done.Length == 0 ? Files.Names(Terminal.Shell).Where(n => n.EndsWith("/")).ToList() : new List<string>(), examples = new[] { ("cd notes", "into notes"), ("cd ..", "one folder up"), ("cd", "back to ~") } },
        new Command { name = "mkdir", args = "<name>", help = "make a folder", run = Files.Mkdir, examples = new[] { ("mkdir notes", "a folder called notes"), ("mkdir notes/raids", "raids inside notes") } },
        new Command { name = "cat", args = "<file>", help = "show a file", run = Files.Cat, complete = done => done.Length == 0 ? Files.Names(Terminal.Shell) : new List<string>(), examples = new[] { ("cat todo", "shows the file todo") } },
        new Command { name = "nano", args = "<file>", help = "write a file, ctrl+s saves, ctrl+k erases a line, esc leaves", run = Files.Nano, complete = done => done.Length == 0 ? Files.Names(Terminal.Shell) : new List<string>(), examples = new[] { ("nano todo", "opens todo, or a new one"), ("nano notes/raids", "a file inside notes") } },
        new Command { name = "rm", args = "<file|folder>", help = "remove a file or folder, asks first if the folder has things", run = Files.Rm, complete = done => done.Length == 0 ? Files.Names(Terminal.Shell) : new List<string>(), examples = new[] { ("rm todo", "removes the file todo"), ("rm notes", "removes notes, asks first if it has things in it") } },
        new Command { name = "mv", args = "<old> <new>", help = "rename or move a file or folder", run = Files.Mv, complete = done => done.Length <= 1 ? Files.Names(Terminal.Shell) : new List<string>(), examples = new[] { ("mv todo done", "renames todo to done"), ("mv todo notes", "moves todo into the folder notes") } },
        new Command { name = "cron", args = "[script] [on|off|every|fire|test]", help = "the scripts, when they run and what they'd do now", run = Scripts.Cron, complete = done => done.Length == 0 ? Scripts.AllFiles().Select(f => Scripts.NameOf(f).Replace(' ', '_')).ToList() : done.Length == 1 ? new List<string> { "on", "off", "every", "when", "fire", "test" } : done.Length == 2 && done[1] == "fire" ? Scripts.FireModes.ToList() : done.Length == 2 && done[1] == "when" ? Events.Names.ToList() : new List<string>(), examples = new[] { ("cron", "every script, on or off, and when it last fired"), ("cron lowfood on", "turns lowfood.cron on, it checks at the next hour"), ("cron lowfood every 6h", "checks every 6 hours, it changes the every line"), ("cron lowfood fire once", "fires one time and turns off"), ("cron raid when raid", "runs the moment enemies show up instead of every few hours"), ("cron lowfood test", "each if with its value and whether it would fire now") } },
        new Command { name = "cron_edit", help = "the scripts as a list you can change with the keys", run = (sh, _) => sh.App = new CronApp(), examples = new[] { ("cron_edit", "↑↓ pick, enter on/off, ←→ every, f fire, t test, e edit") } },
        new Command { name = "notify", args = "<text>", help = "a message at the top left of the screen", run = Scripts.Notify, examples = new[] { ("notify hello", "shows hello like the game's messages"), ("action notify {names} are sad", "in a script, with who matched") } },
        new Command { name = "letter", args = "<text>", help = "a letter on the right side of the screen", run = Scripts.Letter, examples = new[] { ("letter check the freezer", "a letter called Terminal"), ("action letter only {value} days of food", "in a script, the letter has the script's name") } },
        new Command { name = "grep", args = "[-v] <word>", help = "after |, keeps the lines with the word, -v the ones without", run = PipeOnly, examples = new[] { ("pawns | grep Sleeping", "only the sleeping ones"), ("factions | grep -v hostile", "every faction that isn't hostile") } },
        new Command { name = "head", args = "[n]", help = "after |, the first lines, 10 if no number", run = PipeOnly, examples = new[] { ("history | head 5", "the first 5 commands you typed") } },
        new Command { name = "tail", args = "[n]", help = "after |, the last lines, 10 if no number", run = PipeOnly, examples = new[] { ("cat ~/log/raids | tail 3", "the last 3 lines of the file") } },
        new Command { name = "wc", help = "after |, how many lines there are", run = PipeOnly, examples = new[] { ("idle | grep -v busy | wc", "how many lines idle printed") } },
        new Command { name = "watch", args = "[seconds] <command>", help = "runs a command again every few seconds, 2 if no number", run = WatchApp.Run, examples = new[] { ("watch pawns", "pawns, every 2 seconds"), ("watch 10 danger", "danger every 10 seconds"), ("watch 1 pawns | grep Tasca", "pipes work too") } },
        new Command { name = "alias", args = "[name] [command...]", help = "a short name for a command line, alias alone lists them", run = Alias, examples = new[] { ("alias sick checkup", "sick runs checkup"), ("alias who pawns | grep", "who Tasca runs pawns | grep Tasca"), ("alias", "lists them") } },
        new Command { name = "unalias", args = "<name>", help = "remove an alias", run = Unalias, complete = done => done.Length == 0 ? JustTerminalMod.Settings.aliases.Keys.ToList() : new List<string>(), examples = new[] { ("unalias sick", "sick is gone") } },
        new Command { name = "snake", help = "a game of snake, arrows turn", run = (sh, _) => sh.App = new SnakeApp(), examples = new[] { ("snake", "eat the dots, don't hit the walls") } },
        new Command { name = "sl", hidden = true, help = "a train", run = (sh, _) => sh.App = new TrainApp() },
        new Command { name = "less", args = "<file>", help = "read a file without changing it", run = Files.Less, complete = done => done.Length == 0 ? Files.Names(Terminal.Shell) : new List<string>(), examples = new[] { ("less /bin/idle.sh", "read how idle is made"), ("less todo", "up, down, page up and down move, q quits") } },
        new Command { name = "example", args = "[name]", help = "copies an example cron script or program to ~/examples", run = Programs.Runner("example"), complete = done => done.Length == 0 ? Programs.Examples.Keys.OrderBy(k => k).ToList() : new List<string>(), examples = new[] { ("example", "lists the examples"), ("example raid.cron", "~/examples/raid.cron, cron raid on turns it on"), ("example hungry", ".sh or .cron can be left out") } },
        new Command { name = "manual", args = "[programming|sections|section]", help = "the manual, how to use the terminal and how to write programs", run = Programs.ManualCmd, complete = done => done.Length == 0 ? Programs.SectionWords() : new List<string>(), examples = new[] { ("manual", "how to use the terminal"), ("manual programming", "how to write programs"), ("manual sections", "the parts of the manual"), ("manual cron", "only the parts about cron") } },
        new Command { name = "cp", args = "<from> <to>", help = "copy a file, from /bin too, so you can change a program", run = Files.Cp, complete = done => done.Length <= 1 ? Files.Names(Terminal.Shell) : new List<string>(), examples = new[] { ("cp todo todo2", "a copy of todo called todo2"), ("cp /bin/idle.sh ~/bin", "your own idle, it runs instead of the mod's"), ("rm ~/bin/idle.sh", "back to the mod's idle") } },
        new Command { name = "font", args = "[size|reset]", help = "text size, 10 to 28", run = Font, complete = done => done.Length == 0 ? new List<string> { "reset" } : new List<string>(), examples = new[] { ("font", "shows the size"), ("font 18", "bigger text"), ("font reset", "back to 15") } },
        new Command { name = "reset", args = "[font|place]", help = "put the text size or the window back as they started", run = Reset, complete = done => done.Length == 0 ? new List<string> { "font", "place" } : new List<string>(), examples = new[] { ("reset place", "window back in its first place and size"), ("reset font", "text size back to 15"), ("reset", "both") } },
        new Command { name = "pin", help = "pin or unpin the window, like the light on the frame", run = Pin, examples = new[] { ("pin", "pinned, it stays open when you click elsewhere, again to unpin") } },
        new Command { name = "edit", examples = new[] { ("edit", "red screen, the commands there change the game"), ("edit, then help", "the edit commands") }, help = "edit mode, to change colonists' settings", run = (sh, _) => sh.SetEditMode(true) },
        new Command { name = "exit", examples = new[] { ("exit", "closes the terminal") }, help = "close the terminal", run = (sh, _) => sh.WantsClose = true }
    };

    // anyone on our maps or in a caravan, colonists first
    private static void Look(Shell sh, string[] args)
    {
        if (args.Length == 0)
        {
            sh.Print("usage: look <name>", Ink.Dim);
            return;
        }
        string wanted = string.Join(" ", args).Replace('_', ' ');
        IEnumerable<Pawn> all = Colonists().Concat(Verse.Find.Maps.SelectMany(m => m.mapPawns.AllPawnsSpawned));
        Pawn p = all.FirstOrDefault(x => x.LabelShort.Equals(wanted, StringComparison.OrdinalIgnoreCase));
        if (p == null)
        {
            sh.Print("nobody called " + wanted, Ink.Bad);
            return;
        }
        CameraJumper.TryJumpAndSelect(p);
    }

    private static void PipeOnly(Shell sh, string[] args) => sh.Print("this one goes after |, like pawns | grep Sleeping", Ink.Dim);

    private static void Alias(Shell sh, string[] args)
    {
        Dictionary<string, string> aliases = JustTerminalMod.Settings.aliases;
        if (args.Length == 0)
        {
            if (aliases.Count == 0)
            {
                sh.Print("no aliases yet, like alias sick checkup", Ink.Dim);
            }
            foreach (KeyValuePair<string, string> a in aliases.OrderBy(a => a.Key))
            {
                sh.Out(Pad(a.Key, 14), Ink.Bright);
                sh.Print(a.Value);
            }
            return;
        }
        if (args.Length == 1)
        {
            sh.Print(aliases.TryGetValue(args[0], out string line) ? line : "no alias called " + args[0], line != null ? Ink.Normal : Ink.Bad);
            return;
        }
        aliases[args[0]] = string.Join(" ", args.Skip(1));
        JustTerminalMod.Save();
        sh.Print($"{args[0]} runs {aliases[args[0]]}", Ink.Good);
    }

    private static void Unalias(Shell sh, string[] args)
    {
        if (args.Length == 0)
        {
            sh.Print("usage: unalias <name>", Ink.Dim);
            return;
        }
        bool removed = JustTerminalMod.Settings.aliases.Remove(args[0]);
        JustTerminalMod.Save();
        sh.Print(removed ? "removed " + args[0] : "no alias called " + args[0], removed ? Ink.Normal : Ink.Bad);
    }

    // the player's name first, so a rename can take another command's old name
    public static Command Find(string name) => All.FirstOrDefault(c => c.Shown.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? All.FirstOrDefault(c => c.Matches(name));

    // helpers

    public static string Pad(string text, int width)
    {
        text ??= "";
        if (text.Length > width)
        {
            return width <= 1 ? text.Substring(0, width) : text.Substring(0, width - 1) + "…";
        }
        return text.PadRight(width);
    }

    private static readonly char[] partial = { ' ', '▏', '▎', '▍', '▌', '▋', '▊', '▉' };

    public static string Bar(float fraction, int width)
    {
        fraction = Mathf.Clamp01(fraction);
        float filled = fraction * width;
        int full = (int)filled;
        char[] bar = new char[width];
        for (int i = 0; i < width; i++)
        {
            if (i < full)
            {
                bar[i] = '█';
            }
            else if (i == full)
            {
                bar[i] = partial[(int)((filled - full) * 8f)];
            }
            else
            {
                bar[i] = ' ';
            }
        }
        return new string(bar);
    }

    // a bar that grows to the left; the font only has half and eighth blocks that sit on the right side
    public static string BarFromRight(float fraction, int width)
    {
        float filled = Mathf.Clamp01(fraction) * width;
        int full = (int)filled;
        float rest = filled - full;
        string part = rest >= 0.375f && full < width ? "▐" : rest >= 0.0625f && full < width ? "▕" : "";
        return part + new string('█', full);
    }

    // bar with a dim track behind it
    public static void OutBar(Shell sh, float fraction, int width, Ink ink)
    {
        string bar = Bar(fraction, width);
        string filled = bar.TrimEnd(' ');
        sh.Out(filled, ink);
        if (filled.Length < width)
        {
            sh.Out(new string('·', width - filled.Length), Ink.Dim);
        }
    }

    // btop style squares, rows of these don't merge into a block like the bars do
    public static void OutMeter(Shell sh, float fraction, int width, Ink ink)
    {
        int filled = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(fraction) * width), fraction > 0f ? 1 : 0, width);
        sh.Out(new string('■', filled), ink);
        sh.Out(new string('■', width - filled), Ink.Dim);
    }

    public static Ink Level(float fraction) => fraction < 0.25f ? Ink.Bad : fraction < 0.5f ? Ink.Warn : Ink.Good;

    private static bool NeedGame(Shell sh)
    {
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null)
        {
            sh.Print("no game running", Ink.Bad);
            return false;
        }
        return true;
    }

    private static List<Pawn> Colonists() =>
        PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep
            .OrderBy(p => p.LabelShort)
            .ToList();

    private static List<string> ColonistNames() =>
        Current.Game == null ? new List<string>() : Colonists().Select(p => p.LabelShort.Replace(' ', '_')).ToList();

    public static string Where(Pawn p)
    {
        if (p.Map != null)
        {
            return p.Map.Parent?.LabelCap ?? "map";
        }
        if (p.GetCaravan() != null)
        {
            return "caravan";
        }
        return "away";
    }

    public static Map HomeMap => Verse.Find.Maps.FirstOrDefault(m => m.IsPlayerHome) ?? Verse.Find.CurrentMap;

    // commands

    // usage and examples of one command
    public static void HelpFor(Shell sh, Command c)
    {
        sh.Out(c.Shown + " ", Ink.Bright);
        sh.Out(c.args + (c.args.Length > 0 ? "  " : ""), Ink.Dim);
        sh.Print(c.help);
        if (c.alias != null)
        {
            sh.Print("also " + c.alias, Ink.Dim);
        }
        if (c.examples == null)
        {
            return;
        }
        sh.Print();
        int width = c.examples.Max(e => e.line.Length) + 3;
        foreach ((string line, string does) in c.examples)
        {
            sh.Out("  " + Pad(line, width), Ink.Accent);
            sh.Print(does, Ink.Dim);
        }
    }

    private static void Help(Shell sh, string[] args)
    {
        if (args.Length > 0)
        {
            Command c = (sh.EditMode ? EditCommands.Find(args[0]) : null) ?? Find(args[0]);
            if (c == null)
            {
                sh.Print("no command called " + args[0], Ink.Bad);
                return;
            }
            HelpFor(sh, c);
            return;
        }
        int width = All.Max(c => (c.Shown + " " + c.args).Length) + 2;
        foreach (Command c in All.Where(c => !c.hidden))
        {
            sh.Out(Pad(c.Shown, c.Shown.Length + 1), Ink.Bright);
            sh.Out(Pad(c.args, width - c.Shown.Length - 1), Ink.Dim);
            sh.Print(c.help);
        }
        sh.Print();
        sh.Out("examples: ", Ink.Dim);
        sh.Out("pawns", Ink.Accent);
        sh.Out(", ", Ink.Dim);
        sh.Out("graph temp 7 2", Ink.Accent);
        sh.Out(", ", Ink.Dim);
        sh.Print("top", Ink.Accent);
        sh.Out("help <command> shows examples for it, like ", Ink.Dim);
        sh.Print("help graph", Ink.Accent);
        sh.Print("keys: up/down history, tab completes, pgup/pgdn or wheel scroll,", Ink.Dim);
        sh.Print("      ctrl+c cancel, ctrl+ +/- text size, esc closes", Ink.Dim);
    }

    private static void History(Shell sh, string[] args)
    {
        for (int i = 0; i < sh.History.Count; i++)
        {
            sh.Out($"{i + 1,4}  ", Ink.Dim);
            sh.Print(sh.History[i]);
        }
    }

    private const int DefaultFont = 15;

    private static void Font(Shell sh, string[] args)
    {
        JustTerminalSettings set = JustTerminalMod.Settings;
        if (args.Length == 0)
        {
            sh.Print($"text size {set.fontSize}, from 10 to 28", Ink.Normal);
            return;
        }
        if (args[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            set.fontSize = DefaultFont;
        }
        else if (int.TryParse(args[0], out int size))
        {
            set.fontSize = Mathf.Clamp(size, 10, 28);
        }
        else
        {
            sh.Print("give a size from 10 to 28, or reset", Ink.Bad);
            return;
        }
        JustTerminalMod.Save();
        sh.Print($"text size {set.fontSize}", Ink.Good);
    }

    private static void Reset(Shell sh, string[] args)
    {
        string what = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
        if (what != "all" && what != "font" && what != "place")
        {
            sh.Print("reset font, reset place, or reset alone for both", Ink.Bad);
            return;
        }
        if (what != "place")
        {
            Font(sh, new[] { "reset" });
        }
        if (what != "font")
        {
            Terminal.ResetPlace();
            sh.Print("window back in its first place", Ink.Good);
        }
    }

    private static void Pin(Shell sh, string[] args)
    {
        switch (Terminal.Open)
        {
            case MainTabWindow_Terminal tab:
                tab.Pin();
                break;
            case Window_TerminalPinned pinned:
                pinned.Unpin();
                break;
        }
    }

    private static void ColorCmd(Shell sh, string[] args)
    {
        if (args.Length == 0)
        {
            sh.Print("color " + JustTerminalMod.Settings.phosphor + ", options: " + string.Join(", ", Phosphor.All.Select(p => p.name).Append("custom")));
            return;
        }
        Phosphor p = args[0].ToLowerInvariant() == "custom" ? Phosphor.Get("custom") : Phosphor.All.FirstOrDefault(x => x.name == args[0].ToLowerInvariant());
        if (p == null)
        {
            sh.Print("unknown color " + args[0], Ink.Bad);
            return;
        }
        JustTerminalMod.Settings.phosphor = p.name;
        JustTerminalMod.Save();
    }





    public static string Doing(Pawn p)
    {
        if (p.InMentalState)
        {
            return p.MentalStateDef.label;
        }
        if (p.Map == null)
        {
            return Where(p);
        }
        string report = p.jobs?.curDriver?.GetReport();
        return string.IsNullOrEmpty(report) ? "idle" : report.CapitalizeFirst();
    }





}
