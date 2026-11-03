using System;
using System.Collections.Generic;
using Verse;
using System.Text.RegularExpressions;
using UnityEngine;

namespace JustTerminal;

// colors by role, each phosphor gives them its own shade
public enum Ink : byte
{
    Normal,
    Dim,
    Bright,
    Good,
    Warn,
    Bad,
    Accent,
    Screen,
    None = 255
}

public class Phosphor
{
    public string name;
    public Color glass;
    public Color frame = new Color(0.12f, 0.12f, 0.115f);
    public Color[] inks;

    public Color this[Ink ink] => ink == Ink.Screen ? glass : inks[(int)ink];

    public static readonly Phosphor[] All =
    {
        new Phosphor
        {
            name = "green",
            glass = new Color(0.02f, 0.06f, 0.035f),
            inks = new[]
            {
                new Color(0.35f, 1f, 0.5f),
                new Color(0.16f, 0.5f, 0.25f),
                new Color(0.75f, 1f, 0.8f),
                new Color(0.35f, 1f, 0.5f),
                new Color(0.85f, 1f, 0.35f),
                new Color(1f, 0.45f, 0.35f),
                new Color(0.4f, 1f, 0.85f)
            }
        },
        new Phosphor
        {
            name = "amber",
            glass = new Color(0.07f, 0.04f, 0.01f),
            inks = new[]
            {
                new Color(1f, 0.7f, 0.2f),
                new Color(0.55f, 0.35f, 0.08f),
                new Color(1f, 0.88f, 0.6f),
                new Color(1f, 0.78f, 0.3f),
                new Color(1f, 0.95f, 0.45f),
                new Color(1f, 0.38f, 0.2f),
                new Color(1f, 0.85f, 0.5f)
            }
        },
        new Phosphor
        {
            name = "white",
            glass = new Color(0.04f, 0.045f, 0.055f),
            inks = new[]
            {
                new Color(0.85f, 0.88f, 0.92f),
                new Color(0.45f, 0.48f, 0.52f),
                new Color(1f, 1f, 1f),
                new Color(0.55f, 0.95f, 0.6f),
                new Color(1f, 0.85f, 0.4f),
                new Color(1f, 0.45f, 0.4f),
                new Color(0.5f, 0.8f, 1f)
            }
        }
    };

    // edit mode, not one of the colors to pick
    public static readonly Phosphor Red = new Phosphor
    {
        name = "red",
        glass = new Color(0.075f, 0.02f, 0.02f),
        inks = new[]
        {
            new Color(1f, 0.38f, 0.32f),
            new Color(0.55f, 0.16f, 0.13f),
            new Color(1f, 0.75f, 0.68f),
            new Color(1f, 0.6f, 0.4f),
            new Color(1f, 0.85f, 0.45f),
            new Color(1f, 0.25f, 0.25f),
            new Color(1f, 0.55f, 0.5f)
        }
    };

    // the inks by name, in the order of the Ink enum
    public static readonly string[] InkNames = { "normal", "dim", "bright", "good", "warning", "bad", "accent" };

    public static Phosphor Get(string name)
    {
        if (name == "custom")
        {
            return JustTerminalMod.Settings.custom.ToPhosphor();
        }
        foreach (Phosphor p in All)
        {
            if (p.name == name)
            {
                return p;
            }
        }
        return All[0];
    }
}

public struct Cell
{
    public char ch;
    public Ink fg;
    public Ink bg;
}

// the character grid everything draws into, the window turns it into text
public class Screen
{
    public int Cols;
    public int Rows;
    public Cell[] Cells = Array.Empty<Cell>();
    // area writes are kept inside, the whole screen when not set
    private int clipX0, clipY0, clipX1 = int.MaxValue, clipY1 = int.MaxValue;

    public void Clip(int x, int y, int w, int h)
    {
        clipX0 = x;
        clipY0 = y;
        clipX1 = x + w;
        clipY1 = y + h;
    }

    public void NoClip()
    {
        clipX0 = clipY0 = 0;
        clipX1 = clipY1 = int.MaxValue;
    }

    // true when the size changed, the cells are then blank
    public bool Resize(int cols, int rows)
    {
        cols = Math.Max(cols, 1);
        rows = Math.Max(rows, 1);
        if (cols == Cols && rows == Rows)
        {
            return false;
        }
        Cols = cols;
        Rows = rows;
        Cells = new Cell[cols * rows];
        Clear();
        return true;
    }

    public void Clear()
    {
        NoClip();
        for (int i = 0; i < Cells.Length; i++)
        {
            Cells[i] = new Cell { ch = ' ', fg = Ink.Normal, bg = Ink.None };
        }
    }

    public void Put(int x, int y, char ch, Ink fg, Ink bg = Ink.None)
    {
        if (x < 0 || y < 0 || x >= Cols || y >= Rows || x < clipX0 || y < clipY0 || x >= clipX1 || y >= clipY1)
        {
            return;
        }
        Cells[y * Cols + x] = new Cell { ch = ch, fg = fg, bg = bg };
    }

    // mods put color tags in labels (quality, traits), the terminal keeps its own colors
    // only real text tags, so things like <name> stay
    private static readonly Regex richTags = new Regex(@"</?(color|b|i|size|material)(=[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Plain(string text) => text == null || text.IndexOf('<') < 0 ? text : richTags.Replace(text, "");

    public int Write(int x, int y, string text, Ink fg, Ink bg = Ink.None)
    {
        foreach (char c in Plain(text))
        {
            Put(x++, y, c, fg, bg);
        }
        return x;
    }

    public ref Cell At(int x, int y) => ref Cells[y * Cols + x];
}

// the player's own colors, saved in the mod settings
public class CustomTheme : IExposable
{
    public Color glass;
    public Color frame;
    public List<Color> inks = new List<Color>();
    private Phosphor cached;

    public static CustomTheme From(Phosphor p) => new CustomTheme { glass = p.glass, frame = p.frame, inks = new List<Color>(p.inks) };

    public Phosphor ToPhosphor()
    {
        while (inks.Count < Phosphor.InkNames.Length)
        {
            inks.Add(Color.white);
        }
        if (cached == null)
        {
            cached = new Phosphor { name = "custom" };
        }
        cached.glass = glass;
        cached.frame = frame;
        cached.inks = inks.ToArray();
        return cached;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref glass, "glass");
        Scribe_Values.Look(ref frame, "frame", new Color(0.12f, 0.12f, 0.115f));
        Scribe_Collections.Look(ref inks, "inks", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            inks ??= new List<Color>();
        }
    }
}
