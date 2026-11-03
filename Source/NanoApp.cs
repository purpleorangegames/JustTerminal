using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace JustTerminal;

// a small text editor for the files in the save: type, arrows, ctrl+s saves, ctrl+k erases a line, esc leaves
public class NanoApp : App
{
    private readonly FileNode parent;
    private readonly string name;
    private FileNode file;
    private readonly List<string> lines;
    private int row;
    private int col;
    private int top;
    private int left;
    private int viewRows = 20;
    private bool dirty;
    private bool asking;
    private string message;
    // less and the manual: moves around, changes nothing
    private readonly bool readOnly;
    // a numpad key that may still turn out to be a number, see Pad
    private KeyCode pendingPad = KeyCode.None;
    private int pendingFrame;
    // the last character typed and when, the number can come before its numpad key too
    private char lastTyped;
    private int lastTypedFrame = -10;

    public override float RedrawEvery => pendingPad != KeyCode.None ? 0f : 0.5f;

    public NanoApp(FileNode parent, string name, FileNode file, string start = null, bool readOnly = false)
    {
        this.readOnly = readOnly;
        this.parent = parent;
        this.name = name;
        this.file = file;
        lines = (file?.text ?? start ?? "").Replace("\r", "").Split('\n').ToList();
        if (readOnly)
        {
            message = null;
        }
        else if (file == null)
        {
            message = start != null ? "new " + (name.EndsWith(".cron", StringComparison.OrdinalIgnoreCase) ? "cron script" : "program") + " with an example" : "new file, it's made when you save";
        }
    }

    public override bool HandleKey(Event e)
    {
        if (asking)
        {
            // only the key itself, the letter typed after it is ignored
            if (e.keyCode == KeyCode.Y)
            {
                if (Save())
                {
                    Done = true;
                }
                asking = false;
            }
            else if (e.keyCode == KeyCode.N)
            {
                Done = true;
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                asking = false;
                message = null;
            }
            return true;
        }
        if (readOnly)
        {
            return ReadKey(e);
        }
        if (Pad(e))
        {
            return true;
        }
        if (e.control)
        {
            if (e.keyCode == KeyCode.S)
            {
                Save();
            }
            else if (e.keyCode == KeyCode.X)
            {
                Quit();
            }
            else if (e.keyCode == KeyCode.K)
            {
                EraseLine();
            }
            return true;
        }
        string line = lines[row];
        switch (e.keyCode)
        {
            case KeyCode.Escape:
                Quit();
                return true;
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                lines[row] = line.Substring(0, col);
                lines.Insert(row + 1, line.Substring(col));
                row++;
                col = 0;
                Changed();
                return true;
            case KeyCode.Backspace:
                if (col > 0)
                {
                    lines[row] = line.Remove(col - 1, 1);
                    col--;
                    Changed();
                }
                else if (row > 0)
                {
                    col = lines[row - 1].Length;
                    lines[row - 1] += line;
                    lines.RemoveAt(row);
                    row--;
                    Changed();
                }
                return true;
            case KeyCode.Delete:
                if (col < line.Length)
                {
                    lines[row] = line.Remove(col, 1);
                    Changed();
                }
                else if (row < lines.Count - 1)
                {
                    lines[row] = line + lines[row + 1];
                    lines.RemoveAt(row + 1);
                    Changed();
                }
                return true;
            case KeyCode.LeftArrow:
                if (col > 0)
                {
                    col--;
                }
                else if (row > 0)
                {
                    row--;
                    col = lines[row].Length;
                }
                return true;
            case KeyCode.RightArrow:
                if (col < line.Length)
                {
                    col++;
                }
                else if (row < lines.Count - 1)
                {
                    row++;
                    col = 0;
                }
                return true;
            case KeyCode.UpArrow:
                Move(-1);
                return true;
            case KeyCode.DownArrow:
                Move(1);
                return true;
            case KeyCode.PageUp:
                Move(-viewRows);
                return true;
            case KeyCode.PageDown:
                Move(viewRows);
                return true;
            case KeyCode.Home:
                col = 0;
                return true;
            case KeyCode.End:
                col = line.Length;
                return true;
            case KeyCode.Tab:
                Insert("    ");
                return true;
        }
        char c = e.character;
        if (c != '\0' && !char.IsControl(c))
        {
            Insert(c.ToString());
            lastTyped = c;
            lastTypedFrame = Time.frameCount;
        }
        return true;
    }

    // the view keys, the cursor stays on the first line on screen so it reads like a pager
    private bool ReadKey(Event e)
    {
        int rows = viewRows;
        switch (e.keyCode)
        {
            case KeyCode.Escape:
            case KeyCode.Q:
                Done = true;
                return true;
            // the numbers too, on the numpad or the top row: 8 2 9 3 7 1 like the arrows on the numpad
            case KeyCode.UpArrow:
            case KeyCode.Keypad8:
            case KeyCode.Alpha8:
                Scroll(1);
                return true;
            case KeyCode.DownArrow:
            case KeyCode.Keypad2:
            case KeyCode.Alpha2:
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                Scroll(-1);
                return true;
            case KeyCode.PageUp:
            case KeyCode.Keypad9:
            case KeyCode.Alpha9:
                Scroll(rows);
                return true;
            case KeyCode.PageDown:
            case KeyCode.Keypad3:
            case KeyCode.Alpha3:
            case KeyCode.Space:
                Scroll(-rows);
                return true;
            case KeyCode.Home:
            case KeyCode.Keypad7:
            case KeyCode.Alpha7:
                top = 0;
                return true;
            case KeyCode.End:
            case KeyCode.Keypad1:
            case KeyCode.Alpha1:
                top = Math.Max(0, lines.Count - rows);
                return true;
        }
        if (e.control && e.keyCode == KeyCode.X)
        {
            Done = true;
        }
        return true;
    }

    // with num lock on the numpad types numbers, off it moves like the keys printed on it.
    // the key and the number come apart, the number only with num lock on, so the move waits a frame for it
    private bool Pad(Event e)
    {
        bool pad = e.keyCode >= KeyCode.Keypad0 && e.keyCode <= KeyCode.Keypad9 || e.keyCode == KeyCode.KeypadPeriod;
        if (pad && e.character == '\0')
        {
            char digit = e.keyCode == KeyCode.KeypadPeriod ? '.' : (char)('0' + (e.keyCode - KeyCode.Keypad0));
            // the number came first, it's typed already
            if (lastTyped == digit && Time.frameCount - lastTypedFrame <= 1)
            {
                return true;
            }
            pendingPad = e.keyCode;
            pendingFrame = Time.frameCount;
            return true;
        }
        if (pendingPad != KeyCode.None && e.keyCode == KeyCode.None && e.character != '\0')
        {
            // it was a number after all, it gets typed below
            pendingPad = KeyCode.None;
        }
        return false;
    }

    private void PadMove(KeyCode key)
    {
        string line = lines[row];
        switch (key)
        {
            case KeyCode.Keypad8:
                Move(-1);
                break;
            case KeyCode.Keypad2:
                Move(1);
                break;
            case KeyCode.Keypad4:
                if (col > 0)
                {
                    col--;
                }
                else if (row > 0)
                {
                    row--;
                    col = lines[row].Length;
                }
                break;
            case KeyCode.Keypad6:
                if (col < line.Length)
                {
                    col++;
                }
                else if (row < lines.Count - 1)
                {
                    row++;
                    col = 0;
                }
                break;
            case KeyCode.Keypad9:
                Move(-viewRows);
                break;
            case KeyCode.Keypad3:
                Move(viewRows);
                break;
            case KeyCode.Keypad7:
                col = 0;
                break;
            case KeyCode.Keypad1:
                col = line.Length;
                break;
            case KeyCode.KeypadPeriod:
                if (col < line.Length)
                {
                    lines[row] = line.Remove(col, 1);
                    Changed();
                }
                else if (row < lines.Count - 1)
                {
                    lines[row] = line + lines[row + 1];
                    lines.RemoveAt(row + 1);
                    Changed();
                }
                break;
        }
    }

    // ctrl+x or esc, asks first when something isn't saved
    private void Quit()
    {
        if (dirty)
        {
            asking = true;
            message = "save the changes? y/n, esc keeps editing";
        }
        else
        {
            Done = true;
        }
    }

    // ctrl+k, the last line left is only emptied
    private void EraseLine()
    {
        if (lines.Count > 1)
        {
            lines.RemoveAt(row);
            row = Math.Min(row, lines.Count - 1);
        }
        else
        {
            lines[0] = "";
        }
        col = Math.Min(col, lines[row].Length);
        Changed();
    }

    public override void Scroll(int rows)
    {
        top = Math.Max(0, Math.Min(readOnly ? Math.Max(0, lines.Count - viewRows) : lines.Count - 1, top - rows));
        if (readOnly)
        {
            row = top;
        }
    }

    private void Move(int rows)
    {
        row = Mathf.Clamp(row + rows, 0, lines.Count - 1);
        col = Math.Min(col, lines[row].Length);
    }

    private void Insert(string text)
    {
        lines[row] = lines[row].Insert(col, text);
        col += text.Length;
        Changed();
    }

    private void Changed()
    {
        dirty = true;
        message = null;
    }

    private bool Save()
    {
        string text = string.Join("\n", lines);
        if (Encoding.UTF8.GetByteCount(text) > Files.MaxFileBytes)
        {
            message = $"too big to save, files can be {Files.MaxFileBytes / 1024} KB";
            return false;
        }
        if (file == null || file.parent == null || !file.parent.children.Contains(file))
        {
            FileNode there = parent.Child(name);
            if (there != null && there.folder)
            {
                message = "there's a folder called " + name + " now, can't save";
                return false;
            }
            file = there ?? new FileNode { name = name, parent = parent };
            if (there == null)
            {
                parent.children.Add(file);
            }
        }
        file.text = text;
        dirty = false;
        message = "saved";
        if (file.name.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
        {
            string bad = Programs.Check(text);
            message = bad == null ? "saved, the program reads fine" : "saved, but " + bad;
        }
        if (file.IsScript)
        {
            // says right away if cron can run it
            Script script = Scripts.Parse(file);
            message = script.bad == null ? "saved, the script is fine" + (file.scriptOn ? "" : ", cron turns it on") : "saved, but the script is bad, " + script.bad;
        }
        return true;
    }

    public override void Draw(Screen s)
    {
        int w = s.Cols;
        int h = s.Rows;
        viewRows = Math.Max(1, h - 2);
        if (pendingPad != KeyCode.None && Time.frameCount > pendingFrame)
        {
            // no number came, so it's a move
            PadMove(pendingPad);
            pendingPad = KeyCode.None;
        }
        // keep the cursor on screen
        if (row < top)
        {
            top = row;
        }
        if (row >= top + viewRows)
        {
            top = row - viewRows + 1;
        }
        int textW = Math.Max(1, w);
        if (col < left)
        {
            left = col;
        }
        if (col >= left + textW)
        {
            left = col - textW + 1;
        }

        Bar(s, 0, w);
        int x = s.Write(1, 0, readOnly ? "less" : "nano", Ink.Screen, Ink.Normal);
        s.Write(x + 2, 0, (readOnly ? name : parent.Path + "/" + name) + (dirty ? "  *" : ""), Ink.Screen, Ink.Normal);

        for (int i = 0; i < viewRows && top + i < lines.Count; i++)
        {
            string line = lines[top + i];
            if (left < line.Length)
            {
                s.Write(0, 1 + i, line.Substring(left, Math.Min(textW, line.Length - left)), Ink.Normal);
            }
        }
        if (readOnly)
        {
            Bar(s, h - 1, w);
            int end = Math.Min(lines.Count, top + viewRows);
            s.Write(1, h - 1, $"lines {top + 1} to {end} of {lines.Count}", Ink.Screen, Ink.Normal);
            string moves = "↑↓ pgup pgdn move · q quit";
            s.Write(w - moves.Length - 1, h - 1, moves, Ink.Screen, Ink.Normal);
            return;
        }
        // the cursor
        int cx = col - left;
        int cy = row - top + 1;
        string current = lines[row];
        char under = col < current.Length ? current[col] : ' ';
        s.Put(cx, cy, under, Ink.Screen, Ink.Bright);

        Bar(s, h - 1, w);
        string status = message ?? $"line {row + 1}, col {col + 1}" + (dirty ? "  changed" : "");
        s.Write(1, h - 1, App.Clip(status, w / 2), Ink.Screen, Ink.Normal);
        string keys = asking ? "y save · n don't save · esc back" : "ctrl+s save · ctrl+x or esc quit";
        s.Write(w - keys.Length - 1, h - 1, keys, Ink.Screen, Ink.Normal);
    }

    private static void Bar(Screen s, int y, int w)
    {
        for (int i = 0; i < w; i++)
        {
            s.Put(i, y, ' ', Ink.Screen, Ink.Normal);
        }
    }
}
