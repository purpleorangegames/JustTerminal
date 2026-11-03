using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace JustTerminal;

// commands written in the program language, they come with the mod in Common/Programs and show in /bin
public static class Programs
{
    // name without .sh -> the program
    public static readonly Dictionary<string, string> Shipped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private const int MaxDepth = 8;
    private static int depth;

    public const string Template =
        "# a program: in ~/bin type its name to run it, anywhere else its path\n" +
        "# lines: name = value, for x in /folder, if, else if, else, end\n" +
        "# print, out (no new line), meter, run a command, stop\n" +
        "# print and out take a color first: dim bright good warn bad accent\n" +
        "# {} puts a value in the text, /bin has the mod's programs to read\n" +
        "for p in /pawns\n" +
        "  out bright {pad(p/name, 14)}\n" +
        "  meter p/mood 10\n" +
        "  print dim  {round(p/mood)}\n" +
        "end\n";

    // use and programming, from Common/Manual
    public static readonly Dictionary<string, string> Manual = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    // cron scripts and programs to copy with example, from Common/Examples
    public static readonly Dictionary<string, string> Examples = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static void Load(ModContentPack content)
    {
        string examples = Path.Combine(content.RootDir, "Common", "Examples");
        if (Directory.Exists(examples))
        {
            foreach (string file in Directory.GetFiles(examples))
            {
                Examples[Path.GetFileName(file)] = File.ReadAllText(file).Replace("\r", "");
            }
        }
        string manual = Path.Combine(content.RootDir, "Common", "Manual");
        if (Directory.Exists(manual))
        {
            foreach (string file in Directory.GetFiles(manual, "*.txt"))
            {
                Manual[Path.GetFileName(file)] = File.ReadAllText(file).Replace("\r", "");
            }
        }
        string dir = Path.Combine(content.RootDir, "Common", "Programs");
        if (!Directory.Exists(dir))
        {
            return;
        }
        foreach (string file in Directory.GetFiles(dir, "*.sh"))
        {
            Shipped[Path.GetFileNameWithoutExtension(file)] = File.ReadAllText(file).Replace("\r", "");
        }
    }

    // /bin: the programs to read, and the built in commands by name, those can't be read
    public static void Bin(FileNode d)
    {
        foreach (KeyValuePair<string, string> p in Shipped.OrderBy(p => p.Key))
        {
            string text = p.Value;
            GameFs.File(d, p.Key + ".sh", () => text);
        }
        foreach (Command c in Commands.All.Where(c => !c.hidden && !Shipped.ContainsKey(c.name)).OrderBy(c => c.name))
        {
            string help = c.help;
            GameFs.File(d, c.name, () => "built into the mod, it can't be read or copied\n" + help);
        }
    }

    // the cron scripts first, then the programs
    public static void ExamplesDir(FileNode d)
    {
        foreach (KeyValuePair<string, string> e in Examples.OrderBy(e => e.Key.EndsWith(".cron") ? 0 : 1).ThenBy(e => e.Key))
        {
            string text = e.Value;
            GameFs.File(d, e.Key, () => text);
        }
    }

    public static void ManualDir(FileNode d)
    {
        foreach (KeyValuePair<string, string> m in Manual.OrderBy(m => m.Key == "use.txt" ? 0 : 1))
        {
            string text = m.Value;
            GameFs.File(d, m.Key, () => text);
        }
    }

    // the manual's parts by their headings, like STORAGE or CRON SCRIPTS
    public static List<(string part, string title, string text)> Sections()
    {
        List<(string, string, string)> found = new List<(string, string, string)>();
        foreach (KeyValuePair<string, string> m in Manual.OrderBy(m => m.Key == "use.txt" ? 0 : 1))
        {
            string[] lines = m.Value.Split('\n');
            int start = -1;
            for (int i = 0; i <= lines.Length; i++)
            {
                bool heading = i < lines.Length && (i == 0 || lines[i - 1].Trim().Length == 0) && Regex.IsMatch(lines[i], "^[A-Z][A-Z ,&/-]+$");
                if (!heading && i < lines.Length)
                {
                    continue;
                }
                // the title at the top isn't a section
                if (start >= 0 && !lines[start].StartsWith("JUSTTERMINAL MANUAL"))
                {
                    found.Add((Path.GetFileNameWithoutExtension(m.Key), lines[start].Trim().ToLowerInvariant(), string.Join("\n", lines, start, i - start).TrimEnd()));
                }
                start = i;
            }
        }
        return found;
    }

    // every section whose heading has the words, cron gives both cron parts
    public static string Section(string words)
    {
        string w = words.Replace('_', ' ').Trim().ToLowerInvariant();
        List<string> texts = Sections().Where(x => x.title.Contains(w)).Select(x => x.text).ToList();
        return texts.Count == 0 ? null : string.Join("\n\n\n", texts);
    }

    // the first words of the headings, for tab
    public static List<string> SectionWords() =>
        new[] { "programming", "sections" }.Concat(Sections().Select(x => x.title.Split(' ').First(word => word != "the" && word != "an").TrimEnd(','))).Distinct().ToList();

    // manual opens the first part, manual programming the second, manual cron the parts about cron
    public static void ManualCmd(Shell sh, string[] args)
    {
        string word = string.Join(" ", args).Trim().ToLowerInvariant();
        if (word == "sections")
        {
            foreach ((string part, string title, string _) in Sections())
            {
                sh.Out(title.PadRight(28));
                sh.Print(part, Ink.Dim);
            }
            return;
        }
        string text;
        string name;
        if (word.Length == 0 || word == "use" || word == "programming" || word == "prog")
        {
            name = word.StartsWith("prog") ? "programming.txt" : "use.txt";
            if (!Manual.TryGetValue(name, out text))
            {
                sh.Print(name + " is missing from the mod's Common/Manual", Ink.Bad);
                return;
            }
        }
        else
        {
            text = Section(word);
            name = word.Replace(' ', '_');
            if (text == null)
            {
                sh.Print("no section about " + word + ", manual sections lists them", Ink.Bad);
                return;
            }
        }
        // a shell nobody types in gets the text, like scripts and other mods
        if (!sh.CanAsk)
        {
            sh.Print(text);
            return;
        }
        sh.App = new NanoApp(null, "/manual/" + name, null, text, true);
    }

    // a path to a .sh file, or a program with that name in ~/bin
    public static FileNode FindFile(Shell sh, string word)
    {
        if (Files.Root == null)
        {
            return null;
        }
        if (word.Contains('/') || word.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
        {
            FileNode f = Files.Find(sh, word.Replace('_', ' '));
            return f != null && !f.folder && f.name.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) ? f : null;
        }
        FileNode mine = Files.Find(null, "~/bin/" + word.Replace('_', ' ') + ".sh");
        return mine != null && !mine.folder ? mine : null;
    }

    // a command that is a program: the player's copy in ~/bin first, then the one that came with the mod
    public static Action<Shell, string[]> Runner(string name) => (sh, args) =>
    {
        FileNode mine = Files.Root != null ? Files.Find(null, "~/bin/" + name + ".sh") : null;
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null)
        {
            sh.Print("no game running", Ink.Bad);
        }
        else if (mine != null && !mine.folder)
        {
            Run(sh, mine.Text, mine.name, args);
        }
        else if (Shipped.TryGetValue(name, out string text))
        {
            Run(sh, text, name + ".sh", args);
        }
        else
        {
            sh.Print(name + ".sh is missing from the mod's Common/Programs", Ink.Bad);
        }
    };

    public static void Run(Shell sh, string text, string name, string[] args)
    {
        if (depth >= MaxDepth)
        {
            sh.Print($"{name}: programs ran each other {MaxDepth} deep, stopped there", Ink.Bad);
            return;
        }
        Lang lang;
        try
        {
            lang = Lang.Parse(text, name);
        }
        catch (LangError e)
        {
            sh.Print(name + " " + e.Message, Ink.Bad);
            return;
        }
        depth++;
        try
        {
            lang.Run(sh, args);
        }
        finally
        {
            depth--;
        }
    }

    // says if a program reads fine, for nano when it saves
    public static string Check(string text)
    {
        try
        {
            Lang.Parse(text, "");
            return null;
        }
        catch (LangError e)
        {
            return e.Message;
        }
    }
}
