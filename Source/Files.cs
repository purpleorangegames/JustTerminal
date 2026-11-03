using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace JustTerminal;

// a folder or a text file, kept in the save with the recordings, nothing touches the real disk
public class FileNode : IExposable
{
    public string name;
    public bool folder;
    public string text = "";
    public List<FileNode> children = new List<FileNode>();
    public FileNode parent;

    // the game's own folders under /, read only and never saved
    public bool game;
    public Func<string> read;
    // a game folder fills itself the first time something looks inside
    public Func<FileNode, List<FileNode>> fill;
    // game files a program can change with set, gives the reason when the value won't do
    public Func<string, string> write;
    // a game folder where set can make a new file, like /groups
    public Func<string, string, string> make;
    // other names a game folder's things go by, like mining for mine
    public Func<string, string> alias;
    // what the folder stands for, like the pawn
    public object tag;

    public List<FileNode> Kids
    {
        get
        {
            if (fill != null)
            {
                Func<FileNode, List<FileNode>> f = fill;
                fill = null;
                children = f(this);
            }
            return children;
        }
    }

    // a script's place in cron, kept here so it follows the file when it's moved
    public bool scriptOn;
    public int lastRun = -1;
    public long lastFired = -1;
    public string error;

    public bool IsScript => !folder && !game && name.EndsWith(".cron", StringComparison.OrdinalIgnoreCase);

    public string Text
    {
        get
        {
            if (read == null)
            {
                return text ?? "";
            }
            try
            {
                return read() ?? "";
            }
            catch (Exception e)
            {
                return "error: " + e.Message;
            }
        }
    }

    public int Size => folder ? 0 : Encoding.UTF8.GetByteCount(Text);

    public int Count => 1 + children.Sum(c => c.Count);

    // home under / is the ~ folder
    public FileNode Child(string n)
    {
        if (game && parent == null && n.Equals("home", StringComparison.OrdinalIgnoreCase))
        {
            return Files.Root;
        }
        FileNode c = Kids.FirstOrDefault(c => c.name.Equals(n, StringComparison.OrdinalIgnoreCase));
        if (c == null && alias?.Invoke(n) is string other)
        {
            c = Kids.FirstOrDefault(c => c.name.Equals(other, StringComparison.OrdinalIgnoreCase));
        }
        return c;
    }

    public string Path
    {
        get
        {
            List<string> parts = new List<string>();
            for (FileNode n = this; n?.parent != null; n = n.parent)
            {
                parts.Insert(0, n.name);
            }
            if (game)
            {
                return "/" + string.Join("/", parts);
            }
            return "~" + (parts.Count > 0 ? "/" + string.Join("/", parts) : "");
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref name, "name");
        Scribe_Values.Look(ref folder, "folder");
        Scribe_Values.Look(ref text, "text", "");
        Scribe_Collections.Look(ref children, "children", LookMode.Deep);
        Scribe_Values.Look(ref scriptOn, "scriptOn");
        Scribe_Values.Look(ref lastRun, "lastRun", -1);
        Scribe_Values.Look(ref lastFired, "lastFired", -1);
        Scribe_Values.Look(ref error, "error");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            text ??= "";
            children ??= new List<FileNode>();
            children.RemoveAll(c => c?.name == null);
            foreach (FileNode c in children)
            {
                c.parent = this;
            }
        }
    }
}

// ls, cd, mkdir, cat, rm, mv and nano over the files in the save
public static class Files
{
    public const int MaxFileBytes = 64 * 1024;
    public const int MaxNodes = 500;

    public static FileNode Root => Recorder.Current?.Files;

    // where each shell is, by folder node
    public static FileNode Cwd(Shell sh)
    {
        FileNode root = Root;
        if (root == null)
        {
            return null;
        }
        if (sh.GamePath != null)
        {
            FileNode there = Find(null, sh.GamePath);
            if (there != null && there.folder && there.game)
            {
                return there;
            }
            sh.GamePath = null;
        }
        // the folder could have been removed or the game changed
        for (FileNode n = sh.Cwd; n != null; n = n.parent)
        {
            if (n == root)
            {
                return sh.Cwd;
            }
        }
        sh.Cwd = root;
        return root;
    }

    public static string BadName(string n)
    {
        if (string.IsNullOrWhiteSpace(n) || n == "." || n == ".." || n == "~")
        {
            return "that can't be a name";
        }
        if (n.Contains('/') || n.Any(char.IsControl))
        {
            return "names can't have / in them";
        }
        return n.Length > 40 ? "names can be 40 letters at most" : null;
    }

    // follows a path from the current folder, null when something on the way isn't there
    public static FileNode Find(Shell sh, string path)
    {
        // / is the game's folders, ~ the player's, scripts have no shell and start at ~
        FileNode n = path.StartsWith("/") ? GameFs.Top() : path.StartsWith("~") || sh == null ? Root : Cwd(sh);
        foreach (string part in path.TrimStart('~').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (n == null)
            {
                return null;
            }
            if (part == ".")
            {
                continue;
            }
            if (part == "..")
            {
                n = n.parent ?? (n == Root ? GameFs.Top() : n);
                continue;
            }
            if (!n.folder)
            {
                return null;
            }
            n = n.Child(part);
        }
        return n;
    }

    // the folder a new thing goes in and its name, from a path like notes/raids
    public static FileNode ParentOf(Shell sh, string path, out string name)
    {
        string trimmed = path.TrimEnd('/');
        int slash = trimmed.LastIndexOf('/');
        name = slash < 0 ? trimmed.TrimStart('~') : trimmed.Substring(slash + 1);
        string dir = slash < 0 ? (trimmed.StartsWith("~") ? "~" : ".") : slash == 0 ? "/" : trimmed.Substring(0, slash);
        FileNode p = Find(sh, dir);
        return p != null && p.folder ? p : null;
    }

    // nothing under / can be changed
    private static bool ReadOnly(Shell sh, FileNode n)
    {
        if (n != null && n.game)
        {
            sh.Print(n.Path + " is the game's, it can only be read", Ink.Bad);
            return true;
        }
        return false;
    }

    public static List<string> Names(Shell sh) =>
        Cwd(sh)?.Kids.OrderBy(c => c.folder ? 0 : 1).ThenBy(c => c.name).Select(c => c.name.Replace(' ', '_') + (c.folder ? "/" : "")).ToList() ?? new List<string>();

    private static bool Ready(Shell sh)
    {
        if (Root == null)
        {
            sh.Print("files need a game running", Ink.Bad);
            return false;
        }
        return true;
    }

    private static bool Room(Shell sh)
    {
        if (Root.Count >= MaxNodes)
        {
            sh.Print($"there are {MaxNodes} files and folders already, remove some first", Ink.Bad);
            return false;
        }
        return true;
    }

    // names can have spaces when typed with _, like the colonist names
    private static string Arg(string[] args, int i) => args[i].Replace('_', ' ');

    public static void Ls(Shell sh, string[] args)
    {
        if (!Ready(sh))
        {
            return;
        }
        FileNode dir = args.Length > 0 ? Find(sh, Arg(args, 0)) : Cwd(sh);
        if (dir == null)
        {
            sh.Print("nothing called " + Arg(args, 0), Ink.Bad);
            return;
        }
        if (!dir.folder)
        {
            sh.Print(dir.game ? $"{dir.name}  {dir.Text}" : $"{dir.name}  {dir.Size} bytes");
            return;
        }
        // .. like a real terminal, ~ goes up to /
        if (dir.parent != null || dir == Root)
        {
            sh.Print("../", Ink.Accent);
        }
        if (dir.game && dir.parent == null)
        {
            sh.Print("home/", Ink.Accent);
        }
        // the game's folders keep their order, values show where the size would be
        IEnumerable<FileNode> list = dir.game ? dir.Kids : dir.Kids.OrderBy(c => c.folder ? 0 : 1).ThenBy(c => c.name);
        foreach (FileNode c in list)
        {
            if (c.folder)
            {
                sh.Out(Commands.Pad(c.name + "/", 30), Ink.Accent);
                sh.Print(c.game ? "" : c.children.Count + " inside", Ink.Dim);
            }
            else
            {
                sh.Out(Commands.Pad(c.name, 30), c.IsScript ? Ink.Bright : Ink.Normal);
                // game values show as they are, except /bin where they're programs or built in commands
                sh.Out(!c.game || c.name.EndsWith(".sh") ? c.Size + " bytes" : c.parent?.name == "bin" && c.parent.parent?.parent == null ? "built in" : FirstLine(c.Text), Ink.Dim);
                sh.Print(c.write != null ? "  can change" : "", Ink.Accent);
            }
        }
    }

    // long values show their first line, cat shows all of it
    private static string FirstLine(string text)
    {
        int n = text.IndexOf('\n');
        return n < 0 ? text : text.Substring(0, n) + " …";
    }

    public static void Cd(Shell sh, string[] args)
    {
        if (!Ready(sh))
        {
            return;
        }
        if (args.Length == 0)
        {
            sh.Cwd = Root;
            sh.GamePath = null;
            return;
        }
        FileNode dir = Find(sh, Arg(args, 0));
        if (dir == null || !dir.folder)
        {
            sh.Print(dir == null ? "no folder called " + Arg(args, 0) : dir.name + " is a file", Ink.Bad);
            return;
        }
        if (dir.game)
        {
            sh.GamePath = dir.Path;
            return;
        }
        sh.Cwd = dir;
        sh.GamePath = null;
    }

    public static void Mkdir(Shell sh, string[] args)
    {
        if (!Ready(sh))
        {
            return;
        }
        if (args.Length == 0)
        {
            sh.Print("usage: mkdir <name>", Ink.Dim);
            return;
        }
        foreach (string a in args)
        {
            FileNode parent = ParentOf(sh, a.Replace('_', ' '), out string name);
            if (ReadOnly(sh, parent))
            {
                return;
            }
            string bad = BadName(name);
            if (parent == null || bad != null)
            {
                sh.Print(parent == null ? "no folder for " + a : bad, Ink.Bad);
                continue;
            }
            if (parent.Child(name) != null)
            {
                sh.Print(name + " is there already", Ink.Bad);
                continue;
            }
            if (!Room(sh))
            {
                return;
            }
            parent.children.Add(new FileNode { name = name, folder = true, parent = parent });
        }
    }

    public static void Cat(Shell sh, string[] args)
    {
        if (!Ready(sh))
        {
            return;
        }
        if (args.Length == 0)
        {
            sh.Print("usage: cat <file>", Ink.Dim);
            return;
        }
        FileNode f = Find(sh, Arg(args, 0));
        if (f == null || f.folder)
        {
            sh.Print(f == null ? "no file called " + Arg(args, 0) : f.name + " is a folder, ls shows what's in it", Ink.Bad);
            return;
        }
        foreach (string line in f.Text.Split('\n'))
        {
            sh.Print(line);
        }
    }

    public static void Rm(Shell sh, string[] args)
    {
        if (!Ready(sh))
        {
            return;
        }
        if (args.Length == 0)
        {
            sh.Print("usage: rm <file or folder>", Ink.Dim);
            return;
        }
        FileNode n = Find(sh, Arg(args, 0));
        if (ReadOnly(sh, n))
        {
            return;
        }
        if (n == null || n.parent == null)
        {
            sh.Print(n == null ? "nothing called " + Arg(args, 0) : "can't remove ~", Ink.Bad);
            return;
        }
        if (n.folder && n.children.Count > 0)
        {
            // a folder with things in it asks first
            int inside = n.Count - 1;
            sh.Ask($"remove {n.name} and the {inside} thing{(inside == 1 ? "" : "s")} in it? y/n", () => Remove(sh, n));
            return;
        }
        Remove(sh, n);
    }

    private static void Remove(Shell sh, FileNode n)
    {
        n.parent.children.Remove(n);
        sh.Print("removed " + n.name, Ink.Dim);
    }

    public static void Mv(Shell sh, string[] args)
    {
        if (!Ready(sh))
        {
            return;
        }
        if (args.Length < 2)
        {
            sh.Print("usage: mv <old> <new>, a folder as new moves it there", Ink.Dim);
            return;
        }
        FileNode n = Find(sh, Arg(args, 0));
        if (ReadOnly(sh, n))
        {
            return;
        }
        if (n == null || n.parent == null)
        {
            sh.Print(n == null ? "nothing called " + Arg(args, 0) : "can't move ~", Ink.Bad);
            return;
        }
        // into a folder keeps the name, otherwise it's a new name (and maybe a new folder)
        FileNode target = Find(sh, Arg(args, 1));
        FileNode parent;
        string name;
        if (target != null && target.folder)
        {
            parent = target;
            name = n.name;
        }
        else
        {
            parent = ParentOf(sh, Arg(args, 1), out name);
        }
        if (ReadOnly(sh, parent))
        {
            return;
        }
        string bad = BadName(name);
        if (parent == null || bad != null)
        {
            sh.Print(parent == null ? "no folder for " + Arg(args, 1) : bad, Ink.Bad);
            return;
        }
        for (FileNode p = parent; p != null; p = p.parent)
        {
            if (p == n)
            {
                sh.Print("can't move a folder inside itself", Ink.Bad);
                return;
            }
        }
        FileNode there = parent.Child(name);
        if (there != null && there != n)
        {
            sh.Print(name + " is there already", Ink.Bad);
            return;
        }
        n.parent.children.Remove(n);
        n.name = name;
        n.parent = parent;
        parent.children.Add(n);
    }

    // for programs: a file in ~ gets the text, or the text added as a new line, made if it isn't there
    public static string WriteText(string path, string text, bool append)
    {
        if (Root == null)
        {
            return "files need a game running";
        }
        if (!path.StartsWith("~"))
        {
            return "programs write in ~ only, like ~/log";
        }
        FileNode f = Find(null, path);
        if (f != null && f.folder)
        {
            return path + " is a folder";
        }
        if (f == null)
        {
            FileNode parent = ParentOf(null, path, out string name);
            string bad = parent == null ? "no folder for " + path + ", make it with mkdir first" : BadName(name);
            if (bad != null)
            {
                return bad;
            }
            if (Root.Count >= MaxNodes)
            {
                return $"there are {MaxNodes} files and folders already";
            }
            f = new FileNode { name = name, parent = parent };
            parent.children.Add(f);
            append = false;
        }
        string next = append && f.text.Length > 0 ? f.text + "\n" + text : text;
        if (Encoding.UTF8.GetByteCount(next) > MaxFileBytes)
        {
            return $"{path} would be over {MaxFileBytes / 1024} KB, remove some of it first";
        }
        f.text = next;
        return null;
    }

    // copies a file, into a folder keeps the name
    public static void Cp(Shell sh, string[] args)
    {
        if (!Ready(sh))
        {
            return;
        }
        if (args.Length < 2)
        {
            sh.Print("usage: cp <from> <to>, a folder as to copies it there", Ink.Dim);
            return;
        }
        FileNode n = Find(sh, Arg(args, 0));
        if (n == null || n.folder)
        {
            sh.Print(n == null ? "nothing called " + Arg(args, 0) : "cp copies files, not folders", Ink.Bad);
            return;
        }
        string text = n.Text;
        if (n.game && n.name.IndexOf('.') < 0 && text.Contains('\n'))
        {
            sh.Print(n.name + " is built into the mod, only the .sh programs can be copied", Ink.Bad);
            return;
        }
        FileNode target = Find(sh, Arg(args, 1));
        // ~/bin is made the first time a program is copied there
        if (target == null && Arg(args, 1).TrimEnd('/') is "~/bin" or "bin" && (Arg(args, 1).StartsWith("~") || Cwd(sh) == Root) && Room(sh))
        {
            target = new FileNode { name = "bin", folder = true, parent = Root };
            Root.children.Add(target);
        }
        FileNode parent;
        string name;
        if (target != null && target.folder)
        {
            parent = target;
            name = n.name;
        }
        else
        {
            parent = ParentOf(sh, Arg(args, 1), out name);
        }
        if (ReadOnly(sh, parent))
        {
            return;
        }
        string bad = BadName(name);
        if (parent == null || bad != null)
        {
            sh.Print(parent == null ? "no folder for " + Arg(args, 1) : bad, Ink.Bad);
            return;
        }
        FileNode there = parent.Child(name);
        if (there != null && there.folder)
        {
            sh.Print(name + " is a folder there", Ink.Bad);
            return;
        }
        if (there == null)
        {
            if (!Room(sh))
            {
                return;
            }
            there = new FileNode { name = name, parent = parent };
            parent.children.Add(there);
        }
        there.text = text;
    }

    // a file to read, it can't be changed from here
    public static void Less(Shell sh, string[] args)
    {
        if (!Ready(sh))
        {
            return;
        }
        if (args.Length == 0)
        {
            sh.Print("usage: less <file>", Ink.Dim);
            return;
        }
        FileNode f = Find(sh, Arg(args, 0));
        if (f == null || f.folder)
        {
            sh.Print(f == null ? "no file called " + Arg(args, 0) : f.name + " is a folder", Ink.Bad);
            return;
        }
        sh.App = new NanoApp(null, f.Path, null, f.Text, true);
    }

    public static void Nano(Shell sh, string[] args)
    {
        if (!Ready(sh))
        {
            return;
        }
        if (args.Length == 0)
        {
            sh.Print("usage: nano <file>", Ink.Dim);
            return;
        }
        FileNode f = Find(sh, Arg(args, 0));
        if (ReadOnly(sh, f))
        {
            return;
        }
        if (f != null && f.folder)
        {
            sh.Print(f.name + " is a folder", Ink.Bad);
            return;
        }
        if (f == null)
        {
            // a new file, made when it's saved
            FileNode parent = ParentOf(sh, Arg(args, 0), out string name);
            if (ReadOnly(sh, parent))
            {
                return;
            }
            string bad = BadName(name);
            if (parent == null || bad != null)
            {
                sh.Print(parent == null ? "no folder for " + Arg(args, 0) : bad, Ink.Bad);
                return;
            }
            if (!Room(sh))
            {
                return;
            }
            // a new script starts with an example to change
            sh.App = new NanoApp(parent, name, null, name.EndsWith(".cron", StringComparison.OrdinalIgnoreCase) ? Scripts.Template : name.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) ? Programs.Template : null);
            return;
        }
        sh.App = new NanoApp(f.parent, f.name, f);
    }
}
