using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;

namespace JustTerminal;

// one if line: a path, how to compare and what with
public class Cond
{
    public int line;
    public string path;
    public string op;
    public string value;
}

// a .cron file read into its parts, bad says why it can't run
public class Script
{
    public FileNode file;
    public int every;
    // set instead of every for scripts that run when something happens
    public string when;
    public string fire;
    public List<Cond> ifs = new List<Cond>();
    public string action;
    public int actionLine;
    public string bad;
    // where the header lines are in the file, cron rewrites them
    public int everyLine = -1;
    public int fireLine = -1;

    public string Name => Scripts.NameOf(file);

    // what the every or when column shows
    public string Timing => when != null ? "on " + when : Scripts.EveryText(every);
}

// what one check found, for running and for test
public class Check
{
    public bool pass;
    public string error;
    public List<string> names;
    public string value;
    // for when scripts: who it happened to and what happened
    public string who;
    public string what;
    public List<(Cond cond, string shown, bool pass)> lines = new List<(Cond, string, bool)>();
}

// scripts in the save: a header with every and fire, if lines, one action, cron runs them each game hour
public static class Scripts
{
    public const int MaxIfs = 20;
    public static readonly int[] Steps = { 1, 2, 3, 4, 6, 12, 24, 48, 72, 168 };
    public static readonly string[] FireModes = { "each", "daily", "once" };
    // these open windows or programs, a script can't use them
    public static readonly HashSet<string> Blocked = new HashSet<string> { "rimtop", "nano", "cron_edit", "edit", "exit", "mod_options", "pin", "reset", "role", "exile", "caravan", "clear" };
    private static readonly string[] ops = { "<=", ">=", "!=", "==", "<", ">", "=", "has" };

    // the script running now, letters use its name
    public static Script Running;
    // who a when script's event happened to, /event shows them while it runs
    public static Pawn EventPawn;
    public static string EventWho;
    public static string EventWhat;

    public const string Template =
        "# every: how often it checks, like 1h, 6h or 2d\n" +
        "# fire: each (every time it passes), daily (once a day) or once (then it turns off)\n" +
        "# every if has to pass for the action to run, ; runs more than one command\n" +
        "# {value} is the first if's value, {name} and {names} who matched a *\n" +
        "every 2h\n" +
        "fire daily\n" +
        "if /stock/food < 3\n" +
        "action letter only {value} days of food left\n";

    public static string NameOf(FileNode f)
    {
        string path = f.Path;
        return path.StartsWith("~/") ? path.Substring(2) : path;
    }

    public static string EveryText(int hours) => hours % 24 == 0 ? hours / 24 + "d" : hours + "h";

    public static List<FileNode> AllFiles()
    {
        List<FileNode> found = new List<FileNode>();
        if (Files.Root != null)
        {
            Collect(Files.Root, found);
        }
        return found;
    }

    private static void Collect(FileNode n, List<FileNode> found)
    {
        foreach (FileNode c in n.children.OrderBy(c => c.name))
        {
            if (c.folder)
            {
                Collect(c, found);
            }
            else if (c.IsScript)
            {
                found.Add(c);
            }
        }
    }

    public static List<Script> All() => AllFiles().Select(Parse).ToList();

    // by its path from ~ or its file name, .cron can be left out
    public static FileNode FindFile(string word)
    {
        // _ can be a space or a real _ in the file name
        string spaced = word.Replace('_', ' ');
        List<FileNode> all = AllFiles();
        foreach (string w in new[] { word, word + ".cron", spaced, spaced + ".cron" })
        {
            FileNode f = all.FirstOrDefault(x => NameOf(x).Equals(w, StringComparison.OrdinalIgnoreCase)) ?? all.FirstOrDefault(x => x.name.Equals(w, StringComparison.OrdinalIgnoreCase));
            if (f != null)
            {
                return f;
            }
        }
        return null;
    }

    private static Command FindCommand(string name) => EditCommands.Find(name) ?? Commands.Find(name);

    public static Script Parse(FileNode f)
    {
        Script s = new Script { file = f };
        string[] lines = f.Text.Replace("\r", "").Split('\n');
        int meaningful = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#"))
            {
                continue;
            }
            meaningful++;
            int space = line.IndexOf(' ');
            string word = (space < 0 ? line : line.Substring(0, space)).ToLowerInvariant();
            string rest = space < 0 ? "" : line.Substring(space + 1).Trim();
            string at = $"line {i + 1}: ";
            if (s.action != null)
            {
                return Bad(s, at + "nothing can come after action");
            }
            // every or when, and fire, are the first two lines, in any order
            if (meaningful <= 2 && word != "every" && word != "when" && word != "fire")
            {
                return Bad(s, at + "the first two lines are every (or when) and fire");
            }
            switch (word)
            {
                case "when":
                    if (s.everyLine >= 0)
                    {
                        return Bad(s, at + "a script has every or when, once");
                    }
                    s.everyLine = i;
                    s.when = rest.ToLowerInvariant();
                    if (!Events.Names.Contains(s.when))
                    {
                        return Bad(s, at + "when is one of " + string.Join(", ", Events.Names));
                    }
                    break;
                case "every":
                    if (s.everyLine >= 0)
                    {
                        return Bad(s, at + "a script has every or when, once");
                    }
                    s.everyLine = i;
                    s.every = ParseEvery(rest);
                    if (s.every <= 0)
                    {
                        return Bad(s, at + "every needs a time like 1h, 6h or 2d");
                    }
                    break;
                case "fire":
                    if (s.fireLine >= 0)
                    {
                        return Bad(s, at + "fire is there twice");
                    }
                    s.fireLine = i;
                    s.fire = rest.ToLowerInvariant();
                    if (!FireModes.Contains(s.fire))
                    {
                        return Bad(s, at + "fire is each, daily or once");
                    }
                    break;
                case "if":
                    if (s.everyLine < 0 || s.fireLine < 0)
                    {
                        return Bad(s, at + "the first two lines are every (or when) and fire");
                    }
                    if (s.ifs.Count >= MaxIfs)
                    {
                        return Bad(s, at + $"a script can have {MaxIfs} if lines");
                    }
                    string why = ParseIf(rest, i + 1, out Cond c);
                    if (why != null)
                    {
                        return Bad(s, at + why);
                    }
                    s.ifs.Add(c);
                    break;
                case "action":
                    if (rest.Length == 0)
                    {
                        return Bad(s, at + "action needs a command, like notify hello");
                    }
                    foreach (string part in rest.Split(';'))
                    {
                        string name = part.Trim().Split(' ')[0];
                        Command command = name.Length == 0 ? null : FindCommand(name);
                        if (command == null && name.Length > 0 && Programs.FindFile(null, name) != null)
                        {
                            // a program of the player's
                            continue;
                        }
                        if (command == null)
                        {
                            return Bad(s, at + (name.Length == 0 ? "empty command between ;" : name + " isn't a command"));
                        }
                        if (Blocked.Contains(command.name))
                        {
                            return Bad(s, at + command.Shown + " opens a window, scripts can't use it");
                        }
                    }
                    s.action = rest;
                    s.actionLine = i + 1;
                    break;
                default:
                    return Bad(s, at + "don't know " + word + ", lines are every or when, fire, if or action");
            }
        }
        if (s.everyLine < 0)
        {
            return Bad(s, "no every or when line");
        }
        if (s.fireLine < 0)
        {
            return Bad(s, "no fire line");
        }
        if (s.action == null)
        {
            return Bad(s, "no action line");
        }
        return s;
    }

    private static Script Bad(Script s, string why)
    {
        s.bad = why;
        return s;
    }

    public static int ParseEvery(string text)
    {
        Match m = Regex.Match(text.Trim().ToLowerInvariant(), @"^(\d+)\s*(h|d)$");
        if (!m.Success || !int.TryParse(m.Groups[1].Value, out int n))
        {
            return 0;
        }
        int hours = m.Groups[2].Value == "d" ? n * 24 : n;
        return hours >= 1 && hours <= 24 * 60 ? hours : 0;
    }

    private static string ParseIf(string rest, int line, out Cond c)
    {
        c = null;
        string[] words = rest.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 3 || !ops.Contains(words[1]))
        {
            return "if is like: if /stock/food < 3";
        }
        string path = words[0];
        if (!path.StartsWith("/") && !path.StartsWith("~"))
        {
            return "paths in scripts start with / or ~";
        }
        // a * can stand for something not there now, a plain path has to be there, /event only is when something happens
        if (!path.Contains('*') && !path.StartsWith("/event") && GameFs.Expand(path).Count == 0)
        {
            return "nothing at " + path;
        }
        c = new Cond { line = line, path = path, op = words[1], value = words[2].Trim().Trim('"') };
        return null;
    }

    public static bool Compare(string a, string op, string b)
    {
        a = a.Trim();
        if (op == "has")
        {
            // the text has it somewhere, like a gene in the list of genes
            return a.IndexOf(b.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }
        if (float.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) && float.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
        {
            return op switch
            {
                "<" => x < y,
                ">" => x > y,
                "<=" => x <= y,
                ">=" => x >= y,
                "!=" => !Mathf.Approximately(x, y),
                _ => Mathf.Approximately(x, y)
            };
        }
        bool same = a.Equals(b, StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            "!=" => !same,
            "=" or "==" => same,
            _ => false
        };
    }

    public static Check Evaluate(Script s)
    {
        Check check = new Check { pass = true };
        HashSet<string> names = null;
        Dictionary<string, string> firstValues = null;
        foreach (Cond c in s.ifs)
        {
            // /event is only there while a when script runs for something that happened
            if (c.path.StartsWith("/event") && EventWho == null)
            {
                check.lines.Add((c, "only there when it happens", false));
                check.pass = false;
                continue;
            }
            List<(FileNode node, string star)> found = GameFs.Expand(c.path);
            bool star = c.path.Contains('*');
            if (found.Count == 0 && !star)
            {
                check.error = $"line {c.line}: nothing at {c.path}";
                check.pass = false;
                return check;
            }
            if (found.Any(f => f.node.folder))
            {
                check.error = $"line {c.line}: {c.path} is a folder";
                check.pass = false;
                return check;
            }
            if (!star)
            {
                string v = found[0].node.Text;
                bool ok = Compare(v, c.op, c.value);
                check.lines.Add((c, v, ok));
                check.value ??= v;
                check.pass &= ok;
                continue;
            }
            // with a * the names that pass are kept, more lines with * keep the names that pass all of them
            Dictionary<string, string> passing = new Dictionary<string, string>();
            foreach ((FileNode node, string word) in found)
            {
                string v = node.Text;
                if (Compare(v, c.op, c.value) && !passing.ContainsKey(word))
                {
                    passing[word] = v;
                }
            }
            names = names == null ? new HashSet<string>(passing.Keys) : new HashSet<string>(names.Where(passing.ContainsKey));
            firstValues ??= passing;
            string shown = passing.Count == 0 ? "none of " + found.Count : string.Join(", ", passing.Take(6).Select(kv => kv.Key + " " + kv.Value)) + (passing.Count > 6 ? $" and {passing.Count - 6} more" : "");
            check.lines.Add((c, shown, passing.Count > 0));
            check.pass &= names.Count > 0;
        }
        if (names != null)
        {
            check.names = names.OrderBy(n => n).ToList();
            if (check.value == null && check.names.Count > 0 && firstValues.TryGetValue(check.names[0], out string v))
            {
                check.value = v;
            }
        }
        return check;
    }

    private static readonly Regex pathTag = new Regex(@"\{([/~][^}]*)\}");

    public static string Fill(string text, Check check)
    {
        List<string> names = check.names ?? new List<string>();
        text = text.Replace("{value}", check.value ?? "")
            .Replace("{name}", names.FirstOrDefault() ?? "")
            .Replace("{names}", string.Join(", ", names.Select(n => n.Replace('_', ' '))))
            .Replace("{count}", names.Count.ToString())
            .Replace("{who}", check.who ?? "")
            .Replace("{event}", check.what ?? "");
        return pathTag.Replace(text, m =>
        {
            List<(FileNode node, string star)> found = GameFs.Expand(m.Groups[1].Value);
            return found.Count > 0 ? found[0].node.Text : "?";
        });
    }

    public static int LocalDay(long abs)
    {
        Map map = Commands.HomeMap;
        float longitude = map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;
        return GenDate.Year(abs, longitude) * GenDate.DaysPerYear + GenDate.DayOfYear(abs, longitude);
    }

    public static string When(long abs)
    {
        if (abs < 0)
        {
            return "never";
        }
        Map map = Commands.HomeMap;
        float longitude = map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;
        return $"{GenDate.DayOfQuadrum(abs, longitude) + 1} {GenDate.Quadrum(abs, longitude).Label()}, {GenDate.HourOfDay(abs, longitude)}h";
    }

    // called each game hour
    public static void Tick()
    {
        int hour = Recorder.HourNow;
        foreach (FileNode f in AllFiles())
        {
            if (!f.scriptOn || f.lastRun >= 0 && hour - f.lastRun < Math.Max(1, ParseEvery(EveryOf(f))) || EveryOf(f) == null)
            {
                continue;
            }
            f.lastRun = hour;
            try
            {
                RunOne(Parse(f));
            }
            catch (Exception e)
            {
                Stop(f, e.Message);
                Log.Warning("[JustTerminal] script " + NameOf(f) + " failed: " + e);
            }
        }
    }

    // only the every line, so a broken script still waits its time before it's turned off, null for when scripts
    private static string EveryOf(FileNode f)
    {
        foreach (string line in f.Text.Split('\n'))
        {
            string t = line.Trim();
            if (t.StartsWith("every ", StringComparison.OrdinalIgnoreCase))
            {
                return t.Substring(6);
            }
            if (t.StartsWith("when ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }
        return "1h";
    }

    // something happened, the when scripts for it run now
    public static void RunEvent(string name, string who, string what, Pawn pawn = null)
    {
        EventPawn = pawn;
        EventWho = who;
        EventWhat = what;
        try
        {
            RunEventScripts(name, who, what);
        }
        finally
        {
            EventPawn = null;
            EventWho = null;
            EventWhat = null;
        }
    }

    private static void RunEventScripts(string name, string who, string what)
    {
        foreach (FileNode f in AllFiles())
        {
            if (!f.scriptOn || EveryOf(f) != null)
            {
                continue;
            }
            try
            {
                Script s = Parse(f);
                if (s.when == name || s.bad != null)
                {
                    RunOne(s, who, what);
                }
            }
            catch (Exception e)
            {
                Stop(f, e.Message);
                Log.Warning("[JustTerminal] script " + NameOf(f) + " failed: " + e);
            }
        }
    }

    private static void RunOne(Script s, string who = null, string what = null)
    {
        if (s.bad != null)
        {
            Stop(s.file, s.bad);
            return;
        }
        Check check = Evaluate(s);
        check.who = who;
        check.what = what;
        if (check.error != null)
        {
            Stop(s.file, check.error);
            return;
        }
        if (!check.pass || s.fire == "daily" && FiredToday(s.file))
        {
            return;
        }
        string error = Act(s, check);
        if (error != null)
        {
            Stop(s.file, error);
            return;
        }
        s.file.lastFired = Find.TickManager.TicksAbs;
        if (s.fire == "once")
        {
            s.file.scriptOn = false;
        }
    }

    public static bool FiredToday(FileNode f) => f.lastFired >= 0 && LocalDay(f.lastFired) == LocalDay(Find.TickManager.TicksAbs);

    // runs the action's commands in a shell no one sees, null when it all went fine
    private static string Act(Script s, Check check)
    {
        Shell sh = new Shell { EditMode = true, CanAsk = false };
        Running = s;
        try
        {
            foreach (string part in s.action.Split(';'))
            {
                string command = Fill(part.Trim(), check);
                sh.LastBad = null;
                sh.Run(command);
                if (sh.LastBad != null)
                {
                    return $"line {s.actionLine}: {command.Split(' ')[0]}: {sh.LastBad}";
                }
            }
        }
        finally
        {
            Running = null;
        }
        return null;
    }

    private static void Stop(FileNode f, string why)
    {
        f.scriptOn = false;
        f.error = why;
        Messages.Message($"Script {NameOf(f)} stopped, {why}", MessageTypeDefOf.NegativeEvent, false);
    }

    // turning on clears the old error and runs it at the next hour
    public static string TurnOn(FileNode f)
    {
        Script s = Parse(f);
        if (s.bad != null)
        {
            return "can't turn on, " + s.bad;
        }
        f.scriptOn = true;
        f.lastRun = -1;
        f.error = null;
        return null;
    }

    // rewrites one header line, the rest of the file stays as it is, every can become when and back
    public static bool SetHeader(FileNode f, bool every, string value, bool when = false)
    {
        Script s = Parse(f);
        int line = every ? s.everyLine : s.fireLine;
        if (line < 0)
        {
            return false;
        }
        string[] lines = f.text.Replace("\r", "").Split('\n');
        lines[line] = (every ? when ? "when " : "every " : "fire ") + value;
        f.text = string.Join("\n", lines);
        return true;
    }

    public static int Step(int hours, int dir)
    {
        int i = Array.FindIndex(Steps, x => x >= hours);
        if (i < 0)
        {
            i = Steps.Length - 1;
        }
        else if (Steps[i] != hours && dir < 0)
        {
            // between two steps, going down lands on the lower one
            return Steps[Math.Max(0, i - 1)];
        }
        else if (Steps[i] != hours)
        {
            return Steps[i];
        }
        return Steps[Mathf.Clamp(i + dir, 0, Steps.Length - 1)];
    }

    public static string NextFire(string fire) => FireModes[(Array.IndexOf(FireModes, fire) + 1) % FireModes.Length];

    // what test shows: each if with its value, and whether the action would run
    public static List<(string text, Ink ink)> Test(Script s)
    {
        List<(string, Ink)> lines = new List<(string, Ink)>();
        if (s.bad != null)
        {
            lines.Add(("bad, " + s.bad, Ink.Bad));
            return lines;
        }
        lines.Add(($"{(s.when != null ? "when " + s.when : "every " + EveryText(s.every))} · fire {s.fire} · {(s.file.scriptOn ? "on" : "off")} · last fired {When(s.file.lastFired)}", Ink.Dim));
        Check check = Evaluate(s);
        if (check.error != null)
        {
            lines.Add((check.error, Ink.Bad));
            return lines;
        }
        foreach ((Cond c, string shown, bool pass) in check.lines)
        {
            lines.Add(($"{(pass ? "yes" : "no ")}  if {c.path} {c.op} {c.value}   is {shown}", pass ? Ink.Good : Ink.Warn));
        }
        string action = string.Join("; ", s.action.Split(';').Select(p => Fill(p.Trim(), check)));
        if (!check.pass)
        {
            lines.Add(("it wouldn't fire now", Ink.Dim));
        }
        else if (s.fire == "daily" && FiredToday(s.file))
        {
            lines.Add(("it passes, but it fired today already", Ink.Warn));
        }
        else
        {
            lines.Add(("it would fire now", Ink.Good));
        }
        lines.Add(("action " + action, check.pass ? Ink.Bright : Ink.Dim));
        return lines;
    }

    // commands

    public static void Notify(Shell sh, string[] args)
    {
        if (args.Length == 0)
        {
            sh.Print("usage: notify <text>", Ink.Dim);
            return;
        }
        Messages.Message(string.Join(" ", args).Trim('"'), MessageTypeDefOf.NeutralEvent, false);
    }

    public static void Letter(Shell sh, string[] args)
    {
        if (args.Length == 0)
        {
            sh.Print("usage: letter <text>", Ink.Dim);
            return;
        }
        string label = Running != null ? Running.file.name.Substring(0, Running.file.name.LastIndexOf('.')) : "Terminal";
        // a letter from a script doesn't count as a letter event, or a when letter script would never stop
        Events.Quiet = Running != null;
        try
        {
            Find.LetterStack.ReceiveLetter(label.CapitalizeFirst(), string.Join(" ", args).Trim('"'), LetterDefOf.NeutralEvent);
        }
        finally
        {
            Events.Quiet = false;
        }
    }

    public static void Cron(Shell sh, string[] args)
    {
        if (Files.Root == null)
        {
            sh.Print("cron needs a game running", Ink.Bad);
            return;
        }
        if (args.Length == 0)
        {
            List(sh);
            return;
        }
        FileNode f = FindFile(args[0]);
        if (f == null)
        {
            sh.Print("no script called " + args[0] + ", scripts are files that end in .cron", Ink.Bad);
            return;
        }
        string what = args.Length > 1 ? args[1].ToLowerInvariant() : "test";
        switch (what)
        {
            case "on":
                string why = TurnOn(f);
                sh.Print(why ?? NameOf(f) + " is on", why != null ? Ink.Bad : Ink.Good);
                break;
            case "off":
                f.scriptOn = false;
                sh.Print(NameOf(f) + " is off", Ink.Dim);
                break;
            case "every":
                int hours = args.Length > 2 ? ParseEvery(args[2]) : 0;
                if (hours <= 0)
                {
                    sh.Print("every needs a time like 1h, 6h or 2d", Ink.Bad);
                    break;
                }
                sh.Print(SetHeader(f, true, EveryText(hours)) ? $"{NameOf(f)} checks every {EveryText(hours)}" : "the script has no every line to change", Ink.Dim);
                break;
            case "when":
                string evt = args.Length > 2 ? args[2].ToLowerInvariant() : "";
                if (!Events.Names.Contains(evt))
                {
                    sh.Print("when is one of " + string.Join(", ", Events.Names), Ink.Bad);
                    break;
                }
                sh.Print(SetHeader(f, true, evt, true) ? $"{NameOf(f)} runs when {evt}" : "the script has no every or when line to change", Ink.Dim);
                break;
            case "fire":
                string mode = args.Length > 2 ? args[2].ToLowerInvariant() : "";
                if (!FireModes.Contains(mode))
                {
                    sh.Print("fire is each, daily or once", Ink.Bad);
                    break;
                }
                sh.Print(SetHeader(f, false, mode) ? $"{NameOf(f)} fires {mode}" : "the script has no fire line to change", Ink.Dim);
                break;
            case "test":
                foreach ((string text, Ink ink) in Test(Parse(f)))
                {
                    sh.Print(text, ink);
                }
                break;
            default:
                sh.Print("cron <script> on, off, every <time>, when <event>, fire <each|daily|once> or test", Ink.Dim);
                break;
        }
    }

    private static void List(Shell sh)
    {
        List<Script> all = All();
        if (all.Count == 0)
        {
            sh.Print("no scripts", Ink.Dim);
            return;
        }
        int nameW = Mathf.Clamp(all.Max(s => s.Name.Length) + 2, 10, 30);
        sh.Out(Commands.Pad("SCRIPT", nameW), Ink.Dim);
        sh.Out(Commands.Pad("EVERY", 11), Ink.Dim);
        sh.Out(Commands.Pad("FIRE", 7), Ink.Dim);
        sh.Out(Commands.Pad("ON", 5), Ink.Dim);
        sh.Print("LAST FIRED", Ink.Dim);
        foreach (Script s in all)
        {
            sh.Out(Commands.Pad(s.Name, nameW), Ink.Bright);
            if (s.bad != null)
            {
                sh.Print("bad, " + s.bad, Ink.Bad);
                continue;
            }
            sh.Out(Commands.Pad(s.Timing, 11));
            sh.Out(Commands.Pad(s.fire, 7));
            sh.Out(Commands.Pad(s.file.scriptOn ? "on" : "off", 5), s.file.scriptOn ? Ink.Good : Ink.Dim);
            sh.Print(When(s.file.lastFired), Ink.Dim);
            if (!s.file.scriptOn && s.file.error != null)
            {
                sh.Print("  stopped, " + s.file.error, Ink.Warn);
            }
        }
    }
}
