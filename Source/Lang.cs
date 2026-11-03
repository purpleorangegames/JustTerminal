using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace JustTerminal;

public class LangError : Exception
{
    public LangError(string message) : base(message)
    {
    }
}

// every value is text, numbers are text that reads as a number; path is set when it came from a file or folder
public struct Val
{
    public string text;
    public string path;

    public Val(string text, string path = null)
    {
        this.text = text ?? "";
        this.path = path;
    }
}

// the program language: lines of for, if, print, out, meter, run, stop and name = value, blocks close with end
public class Lang
{
    public const int MaxSteps = 100000;

    private abstract class Stmt
    {
        public int line;
    }

    private class Assign : Stmt
    {
        public string name;
        public Expr expr;
    }

    private class ForStmt : Stmt
    {
        public string var;
        public Expr source;
        public List<Stmt> body;
    }

    private class IfStmt : Stmt
    {
        public List<(Expr cond, List<Stmt> body)> branches = new List<(Expr, List<Stmt>)>();
        public List<Stmt> otherwise;
    }

    private class PrintStmt : Stmt
    {
        public bool newline;
        public Ink ink = Ink.Normal;
        public Expr inkExpr;
        public List<object> parts;
    }

    private class MeterStmt : Stmt
    {
        // bar is the block bar with a dotted track, meter the btop squares
        public bool bar;
        public Expr value;
        public Expr width;
        public Ink? ink;
    }

    private class RunStmt : Stmt
    {
        public List<object> parts;
    }

    private class StopStmt : Stmt
    {
    }

    // set <path> = <value>, only on files the game lets change, only in edit mode
    private class SetStmt : Stmt
    {
        public PathE target;
        public Expr value;
    }

    // write or append <path in ~> = <text>, append adds it as a new line
    private class WriteStmt : Stmt
    {
        public bool append;
        public PathE target;
        public Expr value;
    }

    // ask <name> <question>: waits for the player to type something, only at the top of a program
    private class AskStmt : Stmt
    {
        public string var;
        public List<object> question;
    }

    // try <line>: a mistake in it is written in warn and the program goes on, error says what it was
    private class TryStmt : Stmt
    {
        public Stmt inner;
    }

    private abstract class Expr
    {
    }

    private class Lit : Expr
    {
        public string text;
    }

    private class VarE : Expr
    {
        public string name;
    }

    // /stock/food, ~/notes or p/mood where p is a variable that holds a path
    private class PathE : Expr
    {
        public string var;
        public string rest;
    }

    private class Call : Expr
    {
        public string name;
        public List<Expr> args;
    }

    private class Bin : Expr
    {
        public string op;
        public Expr a;
        public Expr b;
    }

    private class Unary : Expr
    {
        public string op;
        public Expr a;
    }

    public static readonly Dictionary<string, Ink> Inks = new Dictionary<string, Ink>
    {
        { "normal", Ink.Normal }, { "dim", Ink.Dim }, { "bright", Ink.Bright }, { "good", Ink.Good }, { "warn", Ink.Warn }, { "warning", Ink.Warn }, { "bad", Ink.Bad }, { "accent", Ink.Accent }
    };

    private static readonly string[] functions = { "pad", "padl", "round", "num", "level", "cap", "lower", "upper", "len", "count", "exists", "path", "min", "max", "arg", "args", "bar", "rbar", "repeat", "prompt", "before", "after", "name", "floor", "cols", "rest", "pawns", "range" };

    private readonly List<Stmt> program;
    private readonly string name;

    private Lang(List<Stmt> program, string name)
    {
        this.program = program;
        this.name = name;
    }

    // parsing, errors say the line

    public static Lang Parse(string text, string name)
    {
        List<(int n, string text)> lines = new List<(int, string)>();
        string[] raw = (text ?? "").Replace("\r", "").Split('\n');
        for (int i = 0; i < raw.Length; i++)
        {
            string t = raw[i].Trim();
            if (t.Length > 0 && !t.StartsWith("#"))
            {
                lines.Add((i + 1, t));
            }
        }
        int at = 0;
        List<Stmt> body = Block(lines, ref at, out string ended);
        if (ended != null)
        {
            throw new LangError($"line {lines[at].n}: {Word(ended)} without a for or if before it");
        }
        return new Lang(body, name);
    }

    private static string Word(string line)
    {
        int space = line.IndexOf(' ');
        return (space < 0 ? line : line.Substring(0, space)).ToLowerInvariant();
    }

    private static string Rest(string line)
    {
        int space = line.IndexOf(' ');
        return space < 0 ? "" : line.Substring(space + 1);
    }

    private static readonly Regex assign = new Regex(@"^([A-Za-z_][A-Za-z0-9_]*)\s*=(?!=)\s*(.*)$");

    // statements until end, else or the end of the file, the line that stopped it is left for the caller
    private static List<Stmt> Block(List<(int n, string text)> lines, ref int at, out string ended)
    {
        List<Stmt> list = new List<Stmt>();
        while (at < lines.Count)
        {
            (int n, string line) = lines[at];
            string word = Word(line);
            if (word == "end" || word == "else")
            {
                ended = line;
                return list;
            }
            at++;
            try
            {
                list.Add(Statement(lines, ref at, n, line, word));
            }
            catch (LangError e) when (!e.Message.StartsWith("line "))
            {
                throw new LangError($"line {n}: {e.Message}");
            }
        }
        ended = null;
        return list;
    }

    private static Stmt Statement(List<(int n, string text)> lines, ref int at, int n, string line, string word)
    {
        string rest = Rest(line);
        switch (word)
        {
            case "for":
            {
                Match m = Regex.Match(rest, @"^([A-Za-z_][A-Za-z0-9_]*)\s+in\s+(.+)$");
                if (!m.Success)
                {
                    throw new LangError("for is like: for p in /pawns");
                }
                ForStmt f = new ForStmt { line = n, var = m.Groups[1].Value, source = ParseExpr(m.Groups[2].Value) };
                f.body = Block(lines, ref at, out string ended);
                if (ended == null || Word(ended) != "end")
                {
                    throw new LangError($"line {n}: the for has no end");
                }
                at++;
                return f;
            }
            case "if":
            {
                IfStmt s = new IfStmt { line = n };
                Expr cond = ParseExpr(rest);
                while (true)
                {
                    List<Stmt> body = Block(lines, ref at, out string ended);
                    s.branches.Add((cond, body));
                    if (ended == null)
                    {
                        throw new LangError($"line {n}: the if has no end");
                    }
                    int endLine = lines[at].n;
                    at++;
                    if (Word(ended) == "end")
                    {
                        return s;
                    }
                    string elseRest = Rest(ended).Trim();
                    if (elseRest.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
                    {
                        cond = Wrap(endLine, () => ParseExpr(elseRest.Substring(3)));
                        continue;
                    }
                    if (elseRest.Length > 0)
                    {
                        throw new LangError($"line {endLine}: else is alone on its line, or else if");
                    }
                    s.otherwise = Block(lines, ref at, out ended);
                    if (ended == null || Word(ended) != "end")
                    {
                        throw new LangError($"line {n}: the if has no end");
                    }
                    at++;
                    return s;
                }
            }
            case "print":
            case "out":
            {
                PrintStmt p = new PrintStmt { line = n, newline = word == "print" };
                // an ink word or [expression] first picks the color, the text after it is kept as it is
                string text = line.Length > word.Length ? line.Substring(word.Length + 1) : "";
                if (text.StartsWith("["))
                {
                    int close = text.IndexOf(']');
                    if (close < 0)
                    {
                        throw new LangError("the [ has no ]");
                    }
                    p.inkExpr = ParseExpr(text.Substring(1, close - 1));
                    text = Skip(text, close + 1);
                }
                else
                {
                    string first = Word(text);
                    if (Inks.TryGetValue(first, out Ink ink))
                    {
                        p.ink = ink;
                        text = Skip(text, first.Length);
                    }
                }
                p.parts = Text(text);
                return p;
            }
            case "meter":
            case "bar":
            {
                // meter or bar <value 0-100> <width> [ink], colored by how full when there's no ink
                List<string> words = SplitArgs(rest);
                if (words.Count < 2)
                {
                    throw new LangError(word + " is like: " + word + " p/health 10");
                }
                MeterStmt m = new MeterStmt { line = n, bar = word == "bar", value = ParseExpr(words[0]), width = ParseExpr(words[1]) };
                if (words.Count > 2)
                {
                    if (!Inks.TryGetValue(words[2].ToLowerInvariant(), out Ink ink))
                    {
                        throw new LangError(words[2] + " isn't a color");
                    }
                    m.ink = ink;
                }
                return m;
            }
            case "run":
                if (rest.Trim().Length == 0)
                {
                    throw new LangError("run needs a command, like run pawns");
                }
                return new RunStmt { line = n, parts = Text(rest.Trim()) };
            case "stop":
                return new StopStmt { line = n };
            case "set":
            {
                Match m = Regex.Match(rest, @"^(\S+)\s*=\s*(.+)$");
                if (!m.Success)
                {
                    throw new LangError("set is like: set p/medicine = \"herbal\"");
                }
                if (ParseExpr(m.Groups[1].Value) is not PathE target)
                {
                    throw new LangError("set changes a path, like p/medicine");
                }
                return new SetStmt { line = n, target = target, value = ParseExpr(m.Groups[2].Value) };
            }
            case "write":
            case "append":
            {
                Match m = Regex.Match(rest, @"^(\S+)\s*=\s*(.+)$");
                if (!m.Success || ParseExpr(m.Groups[1].Value) is not PathE target)
                {
                    throw new LangError(word + " is like: " + word + " ~/log = \"day \" + /colony/day");
                }
                return new WriteStmt { line = n, append = word == "append", target = target, value = ParseExpr(m.Groups[2].Value) };
            }
            case "ask":
            {
                Match m = Regex.Match(rest, @"^([A-Za-z_][A-Za-z0-9_]*)\s+(.+)$");
                if (!m.Success)
                {
                    throw new LangError("ask is like: ask answer change everyone? y/n");
                }
                return new AskStmt { line = n, var = m.Groups[1].Value, question = Text(m.Groups[2].Value) };
            }
            case "try":
            {
                string inner = rest.Trim();
                string innerWord = Word(inner);
                if (innerWord is "for" or "if" or "else" or "end" or "try" or "")
                {
                    throw new LangError("try goes before one line, like try set p/area = \"home\"");
                }
                return new TryStmt { line = n, inner = Statement(lines, ref at, n, inner, innerWord) };
            }
        }
        Match a = assign.Match(line);
        if (a.Success)
        {
            // these words always read as themselves
            if (a.Groups[1].Value.ToLowerInvariant() is "yes" or "no" or "none" or "true" or "false" or "and" or "or" or "not")
            {
                throw new LangError(a.Groups[1].Value + " is taken, use another name");
            }
            return new Assign { line = n, name = a.Groups[1].Value, expr = ParseExpr(a.Groups[2].Value) };
        }
        throw new LangError("don't know " + word + ", lines start with for, if, print, out, meter, bar, run, set, write, append, ask, try, stop or name =");
    }

    private static Expr Wrap(int line, Func<Expr> parse)
    {
        try
        {
            return parse();
        }
        catch (LangError e)
        {
            throw new LangError($"line {line}: {e.Message}");
        }
    }

    // the text after a word, without the one space that splits them
    private static string Skip(string text, int at) => at >= text.Length ? "" : text[at] == ' ' ? text.Substring(at + 1) : text.Substring(at);

    // words split by spaces, but not inside ( ) or quotes
    private static List<string> SplitArgs(string text)
    {
        List<string> words = new List<string>();
        StringBuilder sb = new StringBuilder();
        int depth = 0;
        bool quote = false;
        foreach (char c in text)
        {
            if (c == '"')
            {
                quote = !quote;
            }
            else if (!quote && c == '(')
            {
                depth++;
            }
            else if (!quote && c == ')')
            {
                depth--;
            }
            if (c == ' ' && depth == 0 && !quote)
            {
                if (sb.Length > 0)
                {
                    words.Add(sb.ToString());
                    sb.Clear();
                }
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0)
        {
            words.Add(sb.ToString());
        }
        return words;
    }

    // plain text with {expressions} in it
    private static List<object> Text(string text)
    {
        List<object> parts = new List<object>();
        int i = 0;
        while (i < text.Length)
        {
            int open = text.IndexOf('{', i);
            if (open < 0)
            {
                parts.Add(text.Substring(i));
                break;
            }
            if (open > i)
            {
                parts.Add(text.Substring(i, open - i));
            }
            // the } that closes this {, a path inside can have its own {name}
            int close = -1;
            int depth = 0;
            for (int j = open; j < text.Length; j++)
            {
                if (text[j] == '{')
                {
                    depth++;
                }
                else if (text[j] == '}' && --depth == 0)
                {
                    close = j;
                    break;
                }
            }
            if (close < 0)
            {
                throw new LangError("the { has no }");
            }
            parts.Add(ParseExpr(text.Substring(open + 1, close - open - 1)));
            i = close + 1;
        }
        return parts;
    }

    // expressions: tokens, then or, and, not, compare, + -, * / %, - and the rest

    private enum T
    {
        Num,
        Str,
        Name,
        Path,
        Op,
        Open,
        Close,
        Comma
    }

    private static List<(T type, string text)> Tokens(string s)
    {
        List<(T, string)> tokens = new List<(T, string)>();
        int i = 0;
        bool operand = true;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            int start = i;
            if (char.IsDigit(c) || c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1]))
            {
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.'))
                {
                    i++;
                }
                tokens.Add((T.Num, s.Substring(start, i - start)));
                operand = false;
            }
            else if (c == '"')
            {
                int close = s.IndexOf('"', i + 1);
                if (close < 0)
                {
                    throw new LangError("the \" has no closing \"");
                }
                tokens.Add((T.Str, s.Substring(i + 1, close - i - 1)));
                i = close + 1;
                operand = false;
            }
            else if ((c == '/' || c == '~') && operand)
            {
                i = PathEnd(s, i);
                tokens.Add((T.Path, s.Substring(start, i - start)));
                operand = false;
            }
            else if (char.IsLetter(c) || c == '_')
            {
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_'))
                {
                    i++;
                }
                // a name with / right after it is a path from that variable, p/mood
                if (i < s.Length && s[i] == '/' && i + 1 < s.Length && !char.IsWhiteSpace(s[i + 1]))
                {
                    i = PathEnd(s, i);
                    tokens.Add((T.Path, s.Substring(start, i - start)));
                }
                else
                {
                    tokens.Add((T.Name, s.Substring(start, i - start)));
                }
                operand = false;
            }
            else if (c == '(')
            {
                tokens.Add((T.Open, "("));
                i++;
                operand = true;
            }
            else if (c == ')')
            {
                tokens.Add((T.Close, ")"));
                i++;
                operand = false;
            }
            else if (c == ',')
            {
                tokens.Add((T.Comma, ","));
                i++;
                operand = true;
            }
            else
            {
                string two = i + 1 < s.Length ? s.Substring(i, 2) : "";
                string op = two is "<=" or ">=" or "!=" or "==" ? two : c is '<' or '>' or '=' or '+' or '-' or '*' or '/' or '%' ? c.ToString() : null;
                if (op == null)
                {
                    throw new LangError("don't know " + c);
                }
                tokens.Add((T.Op, op));
                i += op.Length;
                operand = true;
            }
        }
        return tokens;
    }

    private static int PathEnd(string s, int i)
    {
        while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != ',' && s[i] != ')' && s[i] != '(')
        {
            i++;
        }
        return i;
    }

    private class Parser
    {
        public List<(T type, string text)> tokens;
        public int at;

        public bool Is(T type, string text = null) => at < tokens.Count && tokens[at].type == type && (text == null || tokens[at].text == text);

        public bool IsName(string word) => at < tokens.Count && tokens[at].type == T.Name && tokens[at].text.Equals(word, StringComparison.OrdinalIgnoreCase);
    }

    private static Expr ParseExpr(string text)
    {
        Parser p = new Parser { tokens = Tokens(text) };
        if (p.tokens.Count == 0)
        {
            throw new LangError("something is missing");
        }
        Expr e = Or(p);
        if (p.at < p.tokens.Count)
        {
            throw new LangError("don't know what " + p.tokens[p.at].text + " does there");
        }
        return e;
    }

    private static Expr Or(Parser p)
    {
        Expr a = And(p);
        while (p.IsName("or"))
        {
            p.at++;
            a = new Bin { op = "or", a = a, b = And(p) };
        }
        return a;
    }

    private static Expr And(Parser p)
    {
        Expr a = Not(p);
        while (p.IsName("and"))
        {
            p.at++;
            a = new Bin { op = "and", a = a, b = Not(p) };
        }
        return a;
    }

    private static Expr Not(Parser p)
    {
        if (p.IsName("not"))
        {
            p.at++;
            return new Unary { op = "not", a = Not(p) };
        }
        return Compare(p);
    }

    private static Expr Compare(Parser p)
    {
        Expr a = Sum(p);
        while (p.at < p.tokens.Count && p.tokens[p.at].type == T.Op && p.tokens[p.at].text is "<" or ">" or "<=" or ">=" or "=" or "==" or "!=")
        {
            string op = p.tokens[p.at++].text;
            a = new Bin { op = op, a = a, b = Sum(p) };
        }
        return a;
    }

    private static Expr Sum(Parser p)
    {
        Expr a = Product(p);
        while (p.Is(T.Op, "+") || p.Is(T.Op, "-"))
        {
            string op = p.tokens[p.at++].text;
            a = new Bin { op = op, a = a, b = Product(p) };
        }
        return a;
    }

    private static Expr Product(Parser p)
    {
        Expr a = Negative(p);
        while (p.Is(T.Op, "*") || p.Is(T.Op, "/") || p.Is(T.Op, "%"))
        {
            string op = p.tokens[p.at++].text;
            a = new Bin { op = op, a = a, b = Negative(p) };
        }
        return a;
    }

    private static Expr Negative(Parser p)
    {
        if (p.Is(T.Op, "-"))
        {
            p.at++;
            return new Unary { op = "-", a = Negative(p) };
        }
        return Primary(p);
    }

    private static Expr Primary(Parser p)
    {
        if (p.at >= p.tokens.Count)
        {
            throw new LangError("something is missing at the end");
        }
        (T type, string text) = p.tokens[p.at++];
        switch (type)
        {
            case T.Num:
            case T.Str:
                return new Lit { text = text };
            case T.Path:
                if (text.StartsWith("/") || text.StartsWith("~"))
                {
                    return new PathE { rest = text };
                }
                int slash = text.IndexOf('/');
                return new PathE { var = text.Substring(0, slash), rest = text.Substring(slash + 1) };
            case T.Name:
                if (p.Is(T.Open))
                {
                    if (!functions.Contains(text))
                    {
                        throw new LangError(text + " isn't a function, they are " + string.Join(", ", functions));
                    }
                    p.at++;
                    List<Expr> args = new List<Expr>();
                    if (!p.Is(T.Close))
                    {
                        args.Add(Or(p));
                        while (p.Is(T.Comma))
                        {
                            p.at++;
                            args.Add(Or(p));
                        }
                    }
                    if (!p.Is(T.Close))
                    {
                        throw new LangError("the ( after " + text + " has no )");
                    }
                    p.at++;
                    return new Call { name = text, args = args };
                }
                // these read as themselves, other words need quotes
                if (text.ToLowerInvariant() is "yes" or "no" or "none" or "true" or "false")
                {
                    return new Lit { text = text.ToLowerInvariant() };
                }
                return new VarE { name = text };
            case T.Open:
                Expr e = Or(p);
                if (!p.Is(T.Close))
                {
                    throw new LangError("the ( has no )");
                }
                p.at++;
                return e;
        }
        throw new LangError("don't know what " + text + " does there");
    }

    // running

    private Shell sh;
    private string[] args;
    private FileNode top;
    private int steps;
    private readonly Dictionary<string, Val> vars = new Dictionary<string, Val>();
    private int line;

    private class Stop : Exception
    {
    }

    public void Run(Shell shell, string[] arguments)
    {
        sh = shell;
        args = arguments;
        // one tree for the whole run, the values are still read live
        top = GameFs.Top();
        From(0);
    }

    // runs the top lines from one on, an ask stops here and comes back with the answer
    private void From(int start)
    {
        try
        {
            for (int i = start; i < program.Count; i++)
            {
                if (program[i] is AskStmt ask)
                {
                    line = ask.line;
                    if (!sh.CanAsk)
                    {
                        throw new LangError("ask needs someone at the terminal, it doesn't work from cron or watch");
                    }
                    int next = i + 1;
                    sh.AskLine(Join(ask.question), answer =>
                    {
                        vars[ask.var] = new Val(answer.Trim());
                        // the game may have changed while waiting
                        top = GameFs.Top();
                        From(next);
                    });
                    return;
                }
                Exec(new List<Stmt> { program[i] });
            }
        }
        catch (Stop)
        {
        }
        catch (LangError e)
        {
            sh.Print($"{name} line {line}: {e.Message}", Ink.Bad);
        }
    }

    private void Exec(List<Stmt> list)
    {
        foreach (Stmt s in list)
        {
            if (++steps > MaxSteps)
            {
                line = s.line;
                throw new LangError($"it ran {MaxSteps} steps, is a loop stuck?");
            }
            line = s.line;
            switch (s)
            {
                case Assign a:
                    vars[a.name] = Eval(a.expr);
                    break;
                case ForStmt f:
                    foreach (Val v in Items(f.source))
                    {
                        vars[f.var] = v;
                        Exec(f.body);
                        if (++steps > MaxSteps)
                        {
                            throw new LangError($"it ran {MaxSteps} steps, is a loop stuck?");
                        }
                    }
                    break;
                case IfStmt i:
                    bool done = false;
                    foreach ((Expr cond, List<Stmt> body) in i.branches)
                    {
                        line = s.line;
                        if (True(Eval(cond).text))
                        {
                            Exec(body);
                            done = true;
                            break;
                        }
                    }
                    if (!done && i.otherwise != null)
                    {
                        Exec(i.otherwise);
                    }
                    break;
                case PrintStmt p:
                    Ink ink = p.ink;
                    if (p.inkExpr != null)
                    {
                        ink = Inks.TryGetValue(Eval(p.inkExpr).text.ToLowerInvariant(), out Ink found) ? found : Ink.Normal;
                    }
                    sh.Out(Join(p.parts), ink);
                    if (p.newline)
                    {
                        sh.NewLine();
                    }
                    break;
                case MeterStmt m:
                    float value = Number(Eval(m.value).text) / 100f;
                    int width = Mathf.Clamp((int)Number(Eval(m.width).text), 1, 200);
                    if (m.bar)
                    {
                        Commands.OutBar(sh, value, width, m.ink ?? Commands.Level(value));
                    }
                    else
                    {
                        Commands.OutMeter(sh, value, width, m.ink ?? Commands.Level(value));
                    }
                    break;
                case RunStmt r:
                    sh.Execute(Join(r.parts));
                    break;
                case StopStmt:
                    throw new Stop();
                case SetStmt st:
                    Set(st);
                    break;
                case WriteStmt w:
                    string error = Files.WriteText(Resolve(w.target), Eval(w.value).text, w.append);
                    if (error != null)
                    {
                        throw new LangError(error);
                    }
                    break;
                case AskStmt:
                    throw new LangError("ask goes at the top of a program, not inside for or if");
                case TryStmt t:
                    try
                    {
                        Exec(new List<Stmt> { t.inner });
                        vars["error"] = new Val("none");
                    }
                    catch (LangError e)
                    {
                        vars["error"] = new Val(e.Message);
                        sh.Print(e.Message, Ink.Warn);
                    }
                    line = t.line;
                    break;
            }
        }
    }

    private string Join(List<object> parts)
    {
        StringBuilder sb = new StringBuilder();
        foreach (object o in parts)
        {
            sb.Append(o is Expr e ? Eval(e).text : (string)o);
        }
        return sb.ToString();
    }

    private void Set(SetStmt st)
    {
        if (!sh.EditMode)
        {
            throw new LangError("set only changes things in edit mode, type edit first");
        }
        string path = Resolve(st.target);
        string value = Eval(st.value).text;
        List<(FileNode node, string star)> found = GameFs.Expand(path, top);
        string error;
        if (found.Count == 0)
        {
            // a folder like /groups can make a new one
            int slash = path.TrimEnd('/').LastIndexOf('/');
            List<(FileNode node, string star)> parent = slash > 0 ? GameFs.Expand(path.Substring(0, slash), top) : new List<(FileNode, string)>();
            if (parent.Count == 0 || parent[0].node.make == null)
            {
                throw new LangError("nothing at " + path);
            }
            error = parent[0].node.make(path.Substring(slash + 1), value);
        }
        else
        {
            FileNode node = found[0].node;
            if (node.write == null)
            {
                throw new LangError(path + " can only be read");
            }
            error = node.write(value);
        }
        if (error != null)
        {
            throw new LangError(error);
        }
        // the next reads see the change
        top = GameFs.Top();
    }

    // what a for goes over: a folder's insides, everything a path with * stands for, pawns() or range()
    private IEnumerable<Val> Items(Expr source)
    {
        if (source is Call call && call.name == "pawns")
        {
            if (call.args.Count != 1)
            {
                throw new LangError("pawns takes a name, @group or all");
            }
            List<Verse.Pawn> pawns = EditCommands.Who(Eval(call.args[0]).text, out string error) ?? throw new LangError(error);
            FileNode folder = GameFs.Expand("/pawns", top).FirstOrDefault().node;
            return pawns.Select(p => folder?.Kids.FirstOrDefault(k => k.tag == p)).Where(k => k != null).Select(Read).ToList();
        }
        if (source is Call r && r.name == "range")
        {
            // range(5) is 0 to 4, range(2, 5) is 2 to 4, range(0, 10, 3) is 0 3 6 9
            List<float> a = r.args.Select(x => Number(Eval(x).text)).ToList();
            if (a.Count == 0)
            {
                throw new LangError("range takes how many, like range(5)");
            }
            float from = a.Count > 1 ? a[0] : 0f, to = a.Count > 1 ? a[1] : a[0], step = a.Count > 2 ? a[2] : 1f;
            if (step <= 0f)
            {
                throw new LangError("range's step has to be more than 0");
            }
            List<Val> list = new List<Val>();
            for (float i = from; i < to && list.Count < MaxSteps; i += step)
            {
                list.Add(new Val(Num(i)));
            }
            return list;
        }
        string path;
        if (source is PathE pe)
        {
            path = Resolve(pe);
        }
        else
        {
            // a value that came from a folder, like k in for k in /policies
            Val v = Eval(source);
            path = v.path ?? throw new LangError("for goes over a folder, a path with *, pawns() or range(), like for p in /pawns");
        }
        List<(FileNode node, string star)> found = GameFs.Expand(path, top);
        if (!path.Contains('*'))
        {
            if (found.Count == 0)
            {
                throw new LangError("nothing at " + path);
            }
            if (!found[0].node.folder)
            {
                throw new LangError(path + " is a file, for goes over a folder");
            }
            return found[0].node.Kids.ToList().Select(Read);
        }
        return found.Select(f => Read(f.node)).ToList();
    }

    // a folder reads as its name, a file as what's in it
    private static Val Read(FileNode n) => new Val(n.folder ? n.name : n.Text, n.Path);

    private static readonly Regex pathVar = new Regex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}");

    // {name} in a path is that variable's text, like p/work/{w}
    private string Resolve(PathE p)
    {
        string rest = pathVar.Replace(p.rest, m => vars.TryGetValue(m.Groups[1].Value, out Val v) ? v.text : throw new LangError("no variable called " + m.Groups[1].Value));
        if (p.var == null)
        {
            return rest;
        }
        if (!vars.TryGetValue(p.var, out Val v))
        {
            throw new LangError("no variable called " + p.var);
        }
        if (v.path == null)
        {
            throw new LangError(p.var + " isn't a folder, so " + p.var + "/" + p.rest + " doesn't work");
        }
        return v.path.TrimEnd('/') + "/" + rest;
    }

    private Val Eval(Expr e)
    {
        switch (e)
        {
            case Lit l:
                return new Val(l.text);
            case VarE v:
                if (vars.TryGetValue(v.name, out Val val))
                {
                    return val;
                }
                throw new LangError("no variable called " + v.name + ", text goes in quotes, like \"" + v.name + "\"");
            case PathE p:
            {
                string path = Resolve(p);
                if (path.Contains('*'))
                {
                    throw new LangError("a path with * goes in a for, or in count()");
                }
                List<(FileNode node, string star)> found = GameFs.Expand(path, top);
                if (found.Count == 0)
                {
                    throw new LangError("nothing at " + path);
                }
                return Read(found[0].node);
            }
            case Unary u:
                Val x = Eval(u.a);
                return u.op == "not" ? Bool(!True(x.text)) : new Val(Num(-Number(x.text)));
            case Bin b:
                return Binary(b);
            case Call c:
                return Function(c);
        }
        throw new LangError("can't work that out");
    }

    private Val Binary(Bin b)
    {
        if (b.op == "and")
        {
            return Bool(True(Eval(b.a).text) && True(Eval(b.b).text));
        }
        if (b.op == "or")
        {
            return Bool(True(Eval(b.a).text) || True(Eval(b.b).text));
        }
        string x = Eval(b.a).text;
        string y = Eval(b.b).text;
        switch (b.op)
        {
            case "+":
                // numbers add, anything else is joined
                return IsNumber(x) && IsNumber(y) ? new Val(Num(Number(x) + Number(y))) : new Val(x + y);
            case "-":
                return new Val(Num(Number(x) - Number(y)));
            case "*":
                return new Val(Num(Number(x) * Number(y)));
            case "/":
                return Number(y) == 0f ? throw new LangError("can't divide by 0") : new Val(Num(Number(x) / Number(y)));
            case "%":
                return Number(y) == 0f ? throw new LangError("can't divide by 0") : new Val(Num(Number(x) % Number(y)));
            default:
                return Bool(Scripts.Compare(x, b.op, y));
        }
    }

    private Val Function(Call c)
    {
        // count, exists and path look at the path itself, it can be written out or made from text
        if (c.name is "count" or "exists" or "path")
        {
            if (c.args.Count != 1)
            {
                throw new LangError(c.name + " takes a path, like " + c.name + "(/pawns)");
            }
            string path = c.args[0] is PathE pe ? Resolve(pe) : Eval(c.args[0]).text;
            if (!path.StartsWith("/") && !path.StartsWith("~"))
            {
                throw new LangError(path + " isn't a path, paths start with / or ~");
            }
            List<(FileNode node, string star)> found = GameFs.Expand(path, top);
            if (c.name == "exists")
            {
                return Bool(found.Count > 0);
            }
            if (c.name == "path")
            {
                return found.Count == 0 ? throw new LangError("nothing at " + path) : Read(found[0].node);
            }
            return new Val(Num(path.Contains('*') ? found.Count : found.Count == 1 && found[0].node.folder ? found[0].node.Kids.Count : found.Count));
        }
        List<string> a = c.args.Select(x => Eval(x).text).ToList();
        void Need(int n)
        {
            if (a.Count < n)
            {
                throw new LangError($"{c.name} needs {n} value{(n == 1 ? "" : "s")}");
            }
        }
        switch (c.name)
        {
            case "pad":
                Need(2);
                return new Val(Commands.Pad(a[0], Mathf.Clamp((int)Number(a[1]), 0, 200)));
            case "padl":
                Need(2);
                int w = Mathf.Clamp((int)Number(a[1]), 0, 200);
                return new Val(a[0].Length >= w ? a[0] : a[0].PadLeft(w));
            case "round":
                Need(1);
                int d = a.Count > 1 ? Mathf.Clamp((int)Number(a[1]), 0, 6) : 0;
                return new Val(Number(a[0]).ToString("F" + d, CultureInfo.InvariantCulture));
            case "num":
                Need(1);
                return new Val(Number(a[0]).ToString("N0", CultureInfo.InvariantCulture));
            case "level":
                Need(1);
                Ink ink = Commands.Level(Number(a[0]) / 100f);
                return new Val(ink == Ink.Bad ? "bad" : ink == Ink.Warn ? "warn" : "good");
            case "cap":
                Need(1);
                return new Val(a[0].Length == 0 ? "" : char.ToUpperInvariant(a[0][0]) + a[0].Substring(1));
            case "lower":
                Need(1);
                return new Val(a[0].ToLowerInvariant());
            case "upper":
                Need(1);
                return new Val(a[0].ToUpperInvariant());
            case "len":
                Need(1);
                return new Val(a[0].Length.ToString());
            case "min":
                Need(2);
                return new Val(Num(Math.Min(Number(a[0]), Number(a[1]))));
            case "max":
                Need(2);
                return new Val(Num(Math.Max(Number(a[0]), Number(a[1]))));
            case "arg":
                Need(1);
                int i = (int)Number(a[0]);
                return new Val(i >= 1 && i <= args.Length ? args[i - 1] : "");
            case "args":
                return new Val(args.Length.ToString());
            case "bar":
                // the block bar alone, value 0-100, no track
                Need(2);
                return new Val(Commands.Bar(Number(a[0]) / 100f, Mathf.Clamp((int)Number(a[1]), 1, 200)).TrimEnd(' '));
            case "rbar":
                // the same filling from the right
                Need(2);
                return new Val(Commands.BarFromRight(Number(a[0]) / 100f, Mathf.Clamp((int)Number(a[1]), 1, 200)));
            case "repeat":
                Need(2);
                int times = Mathf.Clamp((int)Number(a[1]), 0, 500);
                return new Val(string.Concat(Enumerable.Repeat(a[0], times)));
            case "prompt":
                return new Val(sh.Prompt);
            case "name":
                // the last part of where a value came from, for files read in a for
                Need(1);
                Val from = Eval(c.args[0]);
                return new Val(from.path == null ? from.text : from.path.TrimEnd('/').Substring(from.path.TrimEnd('/').LastIndexOf('/') + 1));
            case "floor":
                Need(1);
                return new Val(Num(Mathf.Floor(Number(a[0]))));
            case "cols":
                return new Val(sh.Cols.ToString());
            case "rest":
                // the words typed from that one on, rest(2) is everything after the first
                Need(1);
                int from2 = Math.Max(1, (int)Number(a[0]));
                return new Val(string.Join(" ", args.Skip(from2 - 1)));
            case "pawns":
            case "range":
                throw new LangError(c.name + "() goes in a for, like for p in " + c.name + "(...)");
            case "before":
                // the text before the first time the second one shows up, all of it when it doesn't
                Need(2);
                int b = a[0].IndexOf(a[1], StringComparison.Ordinal);
                return new Val(b < 0 ? a[0] : a[0].Substring(0, b));
            case "after":
                Need(2);
                int f = a[0].IndexOf(a[1], StringComparison.Ordinal);
                return new Val(f < 0 ? "" : a[0].Substring(f + a[1].Length));
        }
        throw new LangError(c.name + " isn't a function");
    }

    public static bool True(string text)
    {
        string t = text.Trim().ToLowerInvariant();
        if (t is "yes" or "true")
        {
            return true;
        }
        if (t is "" or "no" or "false" or "none")
        {
            return false;
        }
        return !IsNumber(t) || Number(t) != 0f;
    }

    private static Val Bool(bool b) => new Val(b ? "yes" : "no");

    private static bool IsNumber(string s) => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static float Number(string s)
    {
        if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
        {
            return f;
        }
        throw new LangError($"\"{s}\" isn't a number");
    }

    private static string Num(float f) => Mathf.Approximately(f, Mathf.Round(f)) ? Mathf.Round(f).ToString("0", CultureInfo.InvariantCulture) : f.ToString("0.##", CultureInfo.InvariantCulture);
}
