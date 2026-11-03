using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace JustTerminal;

public struct Span
{
    public string text;
    public Ink ink;

    public Span(string text, Ink ink)
    {
        this.text = text;
        this.ink = ink;
    }
}

// prompt, history and the lines printed so far
public class Shell
{
    private const int MaxLines = 1000;

    private readonly List<List<Span>> lines = new List<List<Span>>();
    private List<Span> current = new List<Span>();
    private readonly List<string> history = new List<string>();
    private int historyIndex;
    private string input = "";
    private int caret;
    private int scroll;
    private string lastCompletion;

    public bool WantsClose;
    // commands that change the game only work in edit mode
    public bool EditMode;

    // the folder this shell is in, see Files
    public FileNode Cwd;
    // set when the shell is in the game's folders under /, those are built again each time
    public string GamePath;
    // the last error a command printed, scripts check it
    public string LastBad;
    // a question waiting for the next line
    private Action<string> askAnswer;
    // a shell no one types in can't wait for an answer
    public bool CanAsk = true;
    public bool Waiting => askAnswer != null;

    public string PromptEnd => EditMode ? ":edit# " : ":" + (GamePath ?? (Files.Root != null ? Files.Cwd(this)?.Path ?? "~" : "~")) + "$ ";

    public void Ask(string question, Action yes)
    {
        AskLine(question, answer =>
        {
            answer = answer.Trim().ToLowerInvariant();
            if (answer == "y" || answer == "yes")
            {
                yes();
            }
            else
            {
                Print("left as it was", Ink.Dim);
            }
        });
    }

    // the next line typed goes to answer instead of running
    public void AskLine(string question, Action<string> answer)
    {
        Print(question, Ink.Warn);
        askAnswer = answer;
    }

    public void SetEditMode(bool on)
    {
        EditMode = on;
        if (on)
        {
            Clear();
            Danger();
        }
        else
        {
            Print("left edit mode", Ink.Dim);
        }
    }

    private static readonly string[] danger =
    {
        "████    ███   █   █   ████  █████  ████ ",
        "█   █  █   █  ██  █  █      █      █   █",
        "█   █  █████  █ █ █  █  ██  ████   ████ ",
        "█   █  █   █  █  ██  █   █  █      █  █ ",
        "████   █   █  █   █   ███   █████  █   █"
    };

    // big letters when they fit, the word when they don't
    private void Danger()
    {
        Print();
        if (Cols < danger[0].Length + 2)
        {
            Print("DANGER", Ink.Bad);
        }
        else
        {
            foreach (string line in danger)
            {
                Print(" " + line, Ink.Bad);
            }
        }
        Print();
    }

    private Command FindCommand(string name) => (EditMode ? EditCommands.Find(name) : null) ?? Commands.Find(name);
    // a full screen program, keys and drawing go to it while it runs
    public App App;
    // set after keys and commands so a running program draws right away
    public bool Redraw;
    // width of the screen, for output that fills it
    public int Cols = 80;

    public Shell()
    {
        Banner();
        startup = JustTerminalMod.Settings.startup;
    }

    // run on the first draw, when the shell knows how wide the screen is
    private string startup;

    public void RunStartup()
    {
        if (string.IsNullOrWhiteSpace(startup))
        {
            return;
        }
        string lines = startup;
        startup = null;
        foreach (string line in lines.Split(';'))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                Run(line.Trim());
            }
        }
    }

    private void Banner()
    {
        // the welcome text from the mod options, first line bright
        string[] lines = (JustTerminalMod.Settings.welcome ?? "").Replace("\\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            Print(lines[i], i == 0 ? Ink.Bright : Ink.Dim);
        }
        NewLine();
    }

    // output

    public void Out(string text, Ink ink = Ink.Normal)
    {
        // mods can put color tags in labels, the terminal keeps its own colors
        if (ink == Ink.Bad)
        {
            LastBad = Screen.Plain(text);
        }
        string[] parts = Screen.Plain(text).Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                NewLine();
            }
            if (parts[i].Length > 0)
            {
                current.Add(new Span(parts[i], ink));
            }
        }
    }

    public void Print(string text = "", Ink ink = Ink.Normal)
    {
        Out(text, ink);
        NewLine();
    }

    // how many lines were ever printed, pipes count from it
    private long printed;

    public void NewLine()
    {
        printed++;
        lines.Add(current);
        current = new List<Span>();
        if (lines.Count > MaxLines)
        {
            lines.RemoveRange(0, lines.Count - MaxLines);
        }
    }

    public void Clear()
    {
        lines.Clear();
        current = new List<Span>();
        scroll = 0;
    }

    public string Prompt
    {
        get
        {
            JustTerminalSettings set = JustTerminalMod.Settings;
            string user = string.IsNullOrWhiteSpace(set.promptUser) ? "rim" : set.promptUser.Trim();
            if (!string.IsNullOrWhiteSpace(set.promptHost))
            {
                return user + "@" + set.promptHost.Trim();
            }
            string colony = Faction.OfPlayerSilentFail?.Name ?? "rimworld";
            colony = new string(colony.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (colony.Length == 0)
            {
                colony = "colony";
            }
            if (colony.Length > 16)
            {
                colony = colony.Substring(0, 16);
            }
            return user + "@" + colony;
        }
    }

    // running

    public void Run(string line)
    {
        Redraw = true;
        if (askAnswer != null)
        {
            Action<string> answer = askAnswer;
            askAnswer = null;
            Print(line);
            answer(line);
            if (current.Count > 0)
            {
                NewLine();
            }
            return;
        }
        Out(Prompt, Ink.Accent);
        Out(PromptEnd, Ink.Dim);
        Print(line);
        line = line.Trim();
        if (line.Length == 0)
        {
            return;
        }
        if (history.Count == 0 || history[history.Count - 1] != line)
        {
            history.Add(line);
        }
        historyIndex = history.Count;
        Execute(line);
    }

    // runs a line without showing it, programs use this for their run lines
    public void Execute(string line)
    {
        // command | grep word | head 5: what the command prints goes through the filters
        // alias keeps its | for when it runs
        string[] pipe = line.Split('|');
        if (pipe.Length > 1 && !line.TrimStart().StartsWith("alias ", StringComparison.OrdinalIgnoreCase))
        {
            Pipe(pipe);
            return;
        }
        string[] words = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return;
        }
        // an alias is swapped for what it stands for, once
        if (JustTerminalMod.Settings.aliases.TryGetValue(words[0], out string alias) && !expanding)
        {
            expanding = true;
            try
            {
                Execute(alias + " " + string.Join(" ", words.Skip(1)));
            }
            finally
            {
                expanding = false;
            }
            return;
        }
        // a path to a .sh, or a program in ~/bin, comes before the commands
        FileNode file = Programs.FindFile(this, words[0]);
        if (file != null)
        {
            Programs.Run(this, file.Text, file.name, words.Skip(1).ToArray());
            if (current.Count > 0)
            {
                NewLine();
            }
            return;
        }
        Command command = FindCommand(words[0]);
        if (command == null)
        {
            Print(words[0] + ": command not found", Ink.Bad);
            return;
        }
        try
        {
            command.run(this, words.Skip(1).ToArray());
        }
        catch (Exception e)
        {
            Print(words[0] + ": " + e.Message, Ink.Bad);
            Log.Warning("[JustTerminal] " + words[0] + " failed: " + e);
        }
        if (current.Count > 0)
        {
            NewLine();
        }
    }

    private bool expanding;

    public static readonly string[] Filters = { "grep", "head", "tail", "wc" };

    private void Pipe(string[] parts)
    {
        long before = printed;
        Execute(parts[0]);
        if (current.Count > 0)
        {
            NewLine();
        }
        int made = (int)Math.Min(printed - before, lines.Count);
        List<List<Span>> output = lines.GetRange(lines.Count - made, made);
        lines.RemoveRange(lines.Count - made, made);
        foreach (string part in parts.Skip(1))
        {
            string[] words = part.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string filter = words.Length > 0 ? words[0].ToLowerInvariant() : "";
            string[] args = words.Skip(1).ToArray();
            switch (filter)
            {
                case "grep":
                    // grep -v keeps the lines without the word
                    bool invert = args.Length > 0 && args[0] == "-v";
                    string word = string.Join(" ", args.Skip(invert ? 1 : 0)).Replace('_', ' ');
                    output = output.Where(l => string.Concat(l.Select(sp => sp.text)).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 != invert).ToList();
                    break;
                case "head":
                    output = output.Take(args.Length > 0 && int.TryParse(args[0], out int h) ? h : 10).ToList();
                    break;
                case "tail":
                    int t = args.Length > 0 && int.TryParse(args[0], out int tn) ? tn : 10;
                    output = output.Skip(Math.Max(0, output.Count - t)).ToList();
                    break;
                case "wc":
                    output = new List<List<Span>> { new List<Span> { new Span(output.Count + " lines", Ink.Normal) } };
                    break;
                default:
                    output = new List<List<Span>> { new List<Span> { new Span((filter.Length == 0 ? "nothing" : filter) + " can't go after |, these can: " + string.Join(", ", Filters), Ink.Bad) } };
                    LastBad = output[0][0].text;
                    break;
            }
        }
        foreach (List<Span> l in output)
        {
            current = l;
            NewLine();
        }
    }

    // the lines from the top down, for watch
    public void RenderLines(Screen screen, int y0)
    {
        List<Cell[]> rows = new List<Cell[]>();
        foreach (List<Span> l in lines.Concat(current.Count > 0 ? new[] { current } : Array.Empty<List<Span>>()))
        {
            List<Cell[]> wrapped = new List<Cell[]>();
            Wrap(Flatten(l), screen.Cols, wrapped);
            wrapped.Reverse();
            rows.AddRange(wrapped);
        }
        for (int i = 0; i < rows.Count && y0 + i < screen.Rows; i++)
        {
            for (int x = 0; x < rows[i].Length && x < screen.Cols; x++)
            {
                screen.At(x, y0 + i) = rows[i][x];
            }
        }
    }

    public IReadOnlyList<string> History => history;

    // keys, true when the shell used the event

    public bool HandleKey(Event e)
    {
        Redraw = true;
        if (JustTerminalMod.Settings.keySounds && (e.character != '\0' || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Backspace))
        {
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }
        if (App != null)
        {
            bool used = App.HandleKey(e);
            if (App.Push != null)
            {
                // a program opening another one, like cron_edit opening nano, comes back when that one quits
                App next = App.Push;
                App.Push = null;
                next.Then = App;
                App = next;
            }
            else if (App.Done)
            {
                App = App.Then;
                App?.Back();
            }
            return used || e.keyCode != KeyCode.None || e.character != '\0';
        }
        scroll = e.keyCode == KeyCode.PageUp || e.keyCode == KeyCode.PageDown ? scroll : 0;
        if (e.keyCode != KeyCode.Tab)
        {
            lastCompletion = null;
        }
        if (e.control)
        {
            switch (e.keyCode)
            {
                case KeyCode.L:
                    Clear();
                    return true;
                case KeyCode.C:
                    Out(Prompt, Ink.Accent);
                    Out(PromptEnd, Ink.Dim);
                    Print(input + "^C");
                    input = "";
                    caret = 0;
                    return true;
                case KeyCode.U:
                    input = input.Substring(caret);
                    caret = 0;
                    return true;
                case KeyCode.A:
                    caret = 0;
                    return true;
                case KeyCode.E:
                    caret = input.Length;
                    return true;
                case KeyCode.Equals:
                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                    FontSize(1);
                    return true;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    FontSize(-1);
                    return true;
            }
            return e.keyCode != KeyCode.None || e.character != '\0';
        }
        switch (e.keyCode)
        {
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                string line = input;
                input = "";
                caret = 0;
                Run(line);
                return true;
            case KeyCode.Backspace:
                if (caret > 0)
                {
                    input = input.Remove(caret - 1, 1);
                    caret--;
                }
                return true;
            case KeyCode.Delete:
                if (caret < input.Length)
                {
                    input = input.Remove(caret, 1);
                }
                return true;
            case KeyCode.LeftArrow:
                caret = Math.Max(0, caret - 1);
                return true;
            case KeyCode.RightArrow:
                caret = Math.Min(input.Length, caret + 1);
                return true;
            case KeyCode.Home:
                caret = 0;
                return true;
            case KeyCode.End:
                caret = input.Length;
                return true;
            case KeyCode.UpArrow:
                if (historyIndex > 0)
                {
                    historyIndex--;
                    input = history[historyIndex];
                    caret = input.Length;
                }
                return true;
            case KeyCode.DownArrow:
                if (historyIndex < history.Count)
                {
                    historyIndex++;
                    input = historyIndex < history.Count ? history[historyIndex] : "";
                    caret = input.Length;
                }
                return true;
            case KeyCode.PageUp:
                ScrollBy(10);
                return true;
            case KeyCode.PageDown:
                ScrollBy(-10);
                return true;
            case KeyCode.Tab:
                Complete();
                return true;
        }
        char c = e.character;
        if (c != '\0' && !char.IsControl(c))
        {
            input = input.Insert(caret, c.ToString());
            caret++;
            return true;
        }
        return e.keyCode != KeyCode.None;
    }

    public void ScrollBy(int rows)
    {
        if (App != null)
        {
            App.Scroll(rows);
            return;
        }
        scroll = Math.Max(0, scroll + rows);
    }

    public static void FontSize(int change)
    {
        JustTerminalMod.Settings.fontSize = Mathf.Clamp(JustTerminalMod.Settings.fontSize + change, 10, 28);
        JustTerminalMod.Save();
    }

    private void Complete()
    {
        string before = input.Substring(0, caret);
        int space = before.LastIndexOf(' ');
        string word = before.Substring(space + 1);
        List<string> options = space < 0
            ? (EditMode ? EditCommands.All.Select(c => c.Shown).Concat(Commands.All.Select(c => c.Shown)) : Commands.All.Where(c => !c.hidden).Select(c => c.Shown)).Distinct().ToList()
            : FindCommand(before.Split(' ')[0])?.complete?.Invoke(before.Substring(0, space).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray()) ?? new List<string>();
        List<string> found = options.Where(o => o.StartsWith(word, StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
        if (found.Count == 0)
        {
            return;
        }
        string common = found[0];
        foreach (string f in found)
        {
            int n = 0;
            while (n < common.Length && n < f.Length && char.ToLowerInvariant(common[n]) == char.ToLowerInvariant(f[n]))
            {
                n++;
            }
            common = common.Substring(0, n);
        }
        if (found.Count == 1)
        {
            common = found[0] + " ";
        }
        if (common.Length > word.Length)
        {
            input = input.Substring(0, space + 1) + common + input.Substring(caret);
            caret = space + 1 + common.Length;
        }
        else if (lastCompletion == before)
        {
            // second tab shows what fits
            Out(Prompt, Ink.Accent);
            Out(PromptEnd, Ink.Dim);
            Print(input);
            Print(string.Join("  ", found), Ink.Dim);
        }
        lastCompletion = before;
    }

    // drawing

    public void Render(Screen screen, bool cursorOn)
    {
        screen.Clear();
        if (App != null)
        {
            App.Draw(screen);
            // a program can end on its own, like the train
            if (App.Done)
            {
                App = App.Then;
                App?.Back();
                Redraw = true;
            }
            return;
        }
        int cols = screen.Cols;
        // rows from the bottom up, the prompt line first
        List<Cell[]> rows = new List<Cell[]>();
        List<Cell> prompt = new List<Cell>();
        Add(prompt, Prompt, Ink.Accent);
        Add(prompt, PromptEnd, Ink.Dim);
        int caretAt = prompt.Count + caret;
        Add(prompt, input, Ink.Bright);
        if (caretAt >= prompt.Count)
        {
            prompt.Add(new Cell { ch = ' ', fg = Ink.Normal, bg = Ink.None });
        }
        if (cursorOn && scroll == 0)
        {
            Cell c = prompt[caretAt];
            prompt[caretAt] = new Cell { ch = c.ch, fg = Ink.Screen, bg = Ink.Normal };
        }
        Wrap(prompt, cols, rows);
        if (current.Count > 0)
        {
            Wrap(Flatten(current), cols, rows);
        }
        for (int i = lines.Count - 1; i >= 0 && rows.Count < screen.Rows + scroll; i--)
        {
            Wrap(Flatten(lines[i]), cols, rows);
        }
        scroll = Math.Min(scroll, Math.Max(0, rows.Count - screen.Rows));
        for (int y = 0; y < screen.Rows; y++)
        {
            int index = scroll + screen.Rows - 1 - y;
            if (index < 0 || index >= rows.Count)
            {
                continue;
            }
            Cell[] row = rows[index];
            for (int x = 0; x < row.Length && x < cols; x++)
            {
                screen.At(x, y) = row[x];
            }
        }
        if (scroll > 0)
        {
            string tag = $" -{scroll}- ";
            screen.Write(cols - tag.Length, 0, tag, Ink.Screen, Ink.Dim);
        }
    }

    private static List<Cell> Flatten(List<Span> spans)
    {
        List<Cell> cells = new List<Cell>();
        foreach (Span s in spans)
        {
            Add(cells, s.text, s.ink);
        }
        return cells;
    }

    private static void Add(List<Cell> cells, string text, Ink ink)
    {
        foreach (char c in text)
        {
            if (c == '\t')
            {
                do
                {
                    cells.Add(new Cell { ch = ' ', fg = ink, bg = Ink.None });
                }
                while (cells.Count % 4 != 0);
                continue;
            }
            cells.Add(new Cell { ch = c, fg = ink, bg = Ink.None });
        }
    }

    // adds the line's rows to the list, last row first
    private static void Wrap(List<Cell> cells, int cols, List<Cell[]> rows)
    {
        int count = Math.Max(1, (cells.Count + cols - 1) / cols);
        for (int r = count - 1; r >= 0; r--)
        {
            int start = r * cols;
            int length = Math.Max(0, Math.Min(cols, cells.Count - start));
            rows.Add(cells.GetRange(start, length).ToArray());
        }
    }
}
