using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace JustTerminal;

// the scripts as a list: turn them on and off, change how often and how they fire, test or edit them
public class CronApp : App
{
    private int selected;
    private int listTop;
    private List<Script> scripts = new List<Script>();
    private float readAt = -1f;
    // the script whose test shows on the bottom half
    private FileNode testing;
    private List<(string text, Ink ink)> testLines;
    private string message;
    // where things were drawn, for clicks
    private readonly List<(int y, int index)> rowHits = new List<(int, int)>();
    private int everyX;
    private int fireX;
    private int onX;

    private const int EveryW = 10;
    private const int FireW = 7;
    private const int OnW = 5;

    // read again each second, and after anything changes
    private void Read(bool now = false)
    {
        if (!now && Time.realtimeSinceStartup - readAt < 1f)
        {
            return;
        }
        readAt = Time.realtimeSinceStartup;
        scripts = Scripts.All();
        selected = Mathf.Clamp(selected, 0, Math.Max(0, scripts.Count - 1));
        if (testing != null)
        {
            Script t = scripts.FirstOrDefault(s => s.file == testing);
            testLines = t != null ? Scripts.Test(t) : null;
            if (t == null)
            {
                testing = null;
            }
        }
    }

    public override void Back()
    {
        Read(true);
        message = null;
    }

    private Script Current => selected >= 0 && selected < scripts.Count ? scripts[selected] : null;

    public override bool HandleKey(Event e)
    {
        Read();
        switch (e.keyCode)
        {
            case KeyCode.Escape:
                if (testing != null)
                {
                    testing = null;
                    return true;
                }
                Done = true;
                return true;
            case KeyCode.Q:
                Done = true;
                return true;
            case KeyCode.UpArrow:
                Select(selected - 1);
                return true;
            case KeyCode.DownArrow:
                Select(selected + 1);
                return true;
            case KeyCode.Home:
                Select(0);
                return true;
            case KeyCode.End:
                Select(scripts.Count - 1);
                return true;
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
            case KeyCode.Space:
                Toggle();
                return true;
            case KeyCode.LeftArrow:
                Every(-1);
                return true;
            case KeyCode.RightArrow:
                Every(1);
                return true;
            case KeyCode.F:
                Fire();
                return true;
            case KeyCode.T:
                Test();
                return true;
            case KeyCode.E:
                if (Current != null)
                {
                    FileNode f = Current.file;
                    Push = new NanoApp(f.parent, f.name, f);
                }
                return true;
        }
        return false;
    }

    private void Select(int index)
    {
        int before = selected;
        selected = Mathf.Clamp(index, 0, Math.Max(0, scripts.Count - 1));
        if (selected != before)
        {
            message = null;
            // the test follows the selection while it's open
            if (testing != null && Current != null)
            {
                testing = Current.file;
                Read(true);
            }
        }
    }

    private void Toggle()
    {
        Script s = Current;
        if (s == null)
        {
            return;
        }
        if (s.file.scriptOn)
        {
            s.file.scriptOn = false;
            message = s.Name + " is off";
        }
        else
        {
            string why = Scripts.TurnOn(s.file);
            message = why ?? s.Name + " is on, it checks at the next hour";
        }
        Read(true);
    }

    private void Every(int dir)
    {
        Script s = Current;
        if (s == null || s.bad != null)
        {
            message = s == null ? null : "fix the script first, e edits it";
            return;
        }
        if (s.when != null)
        {
            // a when script goes through the events instead
            int i = Array.IndexOf(Events.Names, s.when);
            Scripts.SetHeader(s.file, true, Events.Names[(i + dir + Events.Names.Length) % Events.Names.Length], true);
        }
        else
        {
            Scripts.SetHeader(s.file, true, Scripts.EveryText(Scripts.Step(s.every, dir)));
        }
        message = null;
        Read(true);
    }

    private void Fire()
    {
        Script s = Current;
        if (s == null || s.bad != null)
        {
            message = s == null ? null : "fix the script first, e edits it";
            return;
        }
        Scripts.SetHeader(s.file, false, Scripts.NextFire(s.fire));
        message = null;
        Read(true);
    }

    private void Test()
    {
        Script s = Current;
        testing = s == null || testing == s.file ? null : s.file;
        Read(true);
    }

    public override void Scroll(int rows) => Select(selected - Math.Sign(rows));

    public override void Click(int x, int y)
    {
        foreach ((int ry, int index) in rowHits)
        {
            if (ry != y)
            {
                continue;
            }
            Select(index);
            Script s = Current;
            if (s == null || s.bad != null)
            {
                return;
            }
            if (x >= everyX && x < everyX + 2)
            {
                Every(-1);
            }
            else if (x >= everyX + EveryW - 3 && x < everyX + EveryW)
            {
                Every(1);
            }
            else if (x >= fireX && x < fireX + FireW)
            {
                Fire();
            }
            else if (x >= onX && x < onX + OnW)
            {
                Toggle();
            }
            return;
        }
    }

    public override void Draw(Screen s)
    {
        if (Files.Root == null)
        {
            s.Write(1, 1, "no game running, q quits", Ink.Bad);
            return;
        }
        Read();
        rowHits.Clear();
        int w = s.Cols, h = s.Rows;

        Bar(s, 0, w);
        int hx = s.Write(1, 0, "cron_edit", Ink.Screen, Ink.Normal);
        int on = scripts.Count(x => x.file.scriptOn);
        s.Write(hx + 2, 0, $"{scripts.Count} scripts, {on} on", Ink.Screen, Ink.Normal);
        string now = Scripts.When(Find.TickManager.TicksAbs);
        s.Write(w - now.Length - 1, 0, now, Ink.Screen, Ink.Normal);

        int listH = h - 3;
        if (testing != null && testLines != null)
        {
            int testH = Mathf.Clamp(testLines.Count + 2, 4, h / 2);
            listH = h - 3 - testH;
            Box(s, 0, 1 + listH, w, testH, "test " + Scripts.NameOf(testing));
            for (int i = 0; i < testLines.Count; i++)
            {
                s.Write(1, 2 + listH + i, App.Clip(testLines[i].text, w - 2), testLines[i].ink);
            }
            s.NoClip();
        }
        List(s, 1, w, listH);

        // the message line, then the keys
        if (message != null)
        {
            s.Write(1, h - 2, App.Clip(message, w - 2), message.StartsWith("can't") ? Ink.Bad : Ink.Warn);
        }
        int kx = 1;
        foreach ((string key, string label) in new[] { ("↑↓", "pick"), ("enter", "on/off"), ("←→", "every"), ("f", "fire"), ("t", "test"), ("e", "edit"), ("q", "quit") })
        {
            if (kx + key.Length + label.Length + 2 > w)
            {
                break;
            }
            kx = s.Write(kx, h - 1, key, Ink.Bright);
            kx = s.Write(kx + 1, h - 1, label, Ink.Dim) + 2;
        }
    }

    private void List(Screen s, int y, int w, int h)
    {
        if (scripts.Count == 0)
        {
            s.Write(2, y + 1, "no scripts yet", Ink.Bright);
            s.Write(2, y + 2, "a cron script is a file that ends in .cron, q and then nano lowfood.cron starts one with an example", Ink.Dim);
            return;
        }
        int lastW = 18;
        int nameW = Math.Max(8, w - 2 - EveryW - FireW - OnW - lastW - 4);
        int x = 2;
        everyX = x + nameW + 1;
        fireX = everyX + EveryW + 1;
        onX = fireX + FireW + 1;
        int lastX = onX + OnW + 1;
        s.Write(x, y, "SCRIPT", Ink.Dim);
        s.Write(everyX, y, "EVERY", Ink.Dim);
        s.Write(fireX, y, "FIRE", Ink.Dim);
        s.Write(onX, y, "ON", Ink.Dim);
        s.Write(lastX, y, "LAST FIRED", Ink.Dim);

        int rows = Math.Max(1, h - 1);
        if (selected < listTop)
        {
            listTop = selected;
        }
        if (selected >= listTop + rows)
        {
            listTop = selected - rows + 1;
        }
        for (int i = 0; i < rows && listTop + i < scripts.Count; i++)
        {
            Script sc = scripts[listTop + i];
            int ry = y + 1 + i;
            rowHits.Add((ry, listTop + i));
            bool sel = listTop + i == selected;
            Ink bg = sel ? Ink.Dim : Ink.None;
            // dark text on the bar, like the title bar
            Ink text = sel ? Ink.Screen : Ink.Normal;
            if (sel)
            {
                for (int cx = 0; cx < w; cx++)
                {
                    s.Put(cx, ry, ' ', Ink.Normal, Ink.Dim);
                }
                s.Put(0, ry, '>', Ink.Screen, Ink.Dim);
            }
            s.Write(x, ry, App.Clip(sc.Name, nameW), text, bg);
            if (sc.bad != null)
            {
                s.Write(everyX, ry, App.Clip("bad, " + sc.bad, w - everyX - 1), sel ? Ink.Screen : Ink.Bad, bg);
                continue;
            }
            s.Write(everyX, ry, "◂ " + (sc.when ?? Scripts.EveryText(sc.every)).PadRight(6) + "▸", text, bg);
            s.Write(fireX, ry, sc.fire, text, bg);
            bool on = sc.file.scriptOn;
            s.Write(onX, ry, on ? "on" : "off", sel ? Ink.Screen : on ? Ink.Good : Ink.Dim, bg);
            string last = !on && sc.file.error != null ? "stopped, " + sc.file.error : Scripts.When(sc.file.lastFired);
            s.Write(lastX, ry, App.Clip(last, w - lastX - 1), sel ? Ink.Screen : !on && sc.file.error != null ? Ink.Warn : Ink.Dim, bg);
        }
        if (scripts.Count > rows)
        {
            s.Write(w - 10, y + h - 1, $" {selected + 1}/{scripts.Count} ", Ink.Dim);
        }
    }

    private static void Bar(Screen s, int y, int w)
    {
        for (int i = 0; i < w; i++)
        {
            s.Put(i, y, ' ', Ink.Screen, Ink.Normal);
        }
    }
}
