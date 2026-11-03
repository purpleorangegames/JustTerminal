using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace JustTerminal;

// the game's GUI can load any ttf through Font.SetFontData (internal), we give it DejaVu Sans Mono
public static class TermFont
{
    private static Font font;
    private static Font builtIn;
    private static string loaded = "";
    private static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

    // where players drop their own .ttf and .otf files
    public static string FolderPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "JustTerminal", "Fonts");

    public static bool IsBuiltIn => loaded == "";
    private static readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<int, Vector2> cells = new Dictionary<int, Vector2>();
    private static readonly Dictionary<(int, char), bool> fits = new Dictionary<(int, char), bool>();

    public static void Load(ModContentPack content)
    {
        string path = null;
        foreach (string folder in content.foldersToLoadDescendingOrder)
        {
            string p = Path.Combine(folder, "Fonts", "DejaVuSansMono.ttf");
            if (File.Exists(p))
            {
                path = p;
                break;
            }
        }
        if (path == null)
        {
            Log.Error("[JustTerminal] font file not found, using the game's font");
            return;
        }
        font = new Font("JustTerminal Mono");
        MethodInfo set = AccessTools.Method(typeof(Font), "SetFontData");
        if (set == null)
        {
            Log.Error("[JustTerminal] Font.SetFontData not found, using the game's font");
            font = null;
            return;
        }
        set.Invoke(font, new object[] { File.ReadAllBytes(path), 32f, 0 });
        builtIn = font;
    }

    // the font the settings ask for, made once and kept
    private static void Check()
    {
        string want = JustTerminalMod.Settings?.font ?? "";
        if (want == loaded)
        {
            return;
        }
        loaded = want;
        styles.Clear();
        cells.Clear();
        fits.Clear();
        if (want == "")
        {
            font = builtIn;
            return;
        }
        if (!fonts.TryGetValue(want, out Font f))
        {
            f = Make(want);
            fonts[want] = f;
        }
        font = f ?? builtIn;
    }

    private static Font Make(string key)
    {
        try
        {
            if (key.StartsWith("os:"))
            {
                return Font.CreateDynamicFontFromOSFont(key.Substring(3), 32);
            }
            if (key.StartsWith("file:"))
            {
                string path = Path.Combine(FolderPath, key.Substring(5));
                if (!File.Exists(path))
                {
                    Log.Warning("[JustTerminal] font file not found: " + path);
                    return null;
                }
                Font f = new Font("JustTerminal " + key.Substring(5));
                AccessTools.Method(typeof(Font), "SetFontData").Invoke(f, new object[] { File.ReadAllBytes(path), 32f, 0 });
                return f;
            }
        }
        catch (System.Exception e)
        {
            Log.Warning("[JustTerminal] couldn't load font " + key + ": " + e.Message);
        }
        return null;
    }

    public static List<string> FolderFonts()
    {
        if (!Directory.Exists(FolderPath))
        {
            return new List<string>();
        }
        List<string> list = new List<string>();
        foreach (string f in Directory.GetFiles(FolderPath))
        {
            string ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext == ".ttf" || ext == ".otf")
            {
                list.Add(Path.GetFileName(f));
            }
        }
        list.Sort();
        return list;
    }

    // installed fonts that look like they're made for code, the rest only when asked
    public static List<string> OsFonts(bool all)
    {
        string[] words = { "mono", "code", "console", "courier", "terminal", "fixed", "vga", "term", "hack", "consolas" };
        List<string> list = new List<string>();
        foreach (string name in Font.GetOSInstalledFontNames())
        {
            string low = name.ToLowerInvariant();
            if (all || System.Array.Exists(words, w => low.Contains(w)))
            {
                list.Add(name);
            }
        }
        return list;
    }

    public static GUIStyle Style(int size)
    {
        Check();
        if (!styles.TryGetValue(size, out GUIStyle style))
        {
            style = new GUIStyle
            {
                font = font,
                fontSize = size,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                richText = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            style.normal.textColor = Color.white;
            styles[size] = style;
        }
        return style;
    }

    // width of one column and height of one row
    public static Vector2 Cell(int size)
    {
        Check();
        if (!cells.TryGetValue(size, out Vector2 cell))
        {
            GUIStyle style = Style(size);
            float w = style.CalcSize(new GUIContent(new string('M', 100))).x / 100f;
            float h = Mathf.Ceil(style.lineHeight);
            cell = new Vector2(w, h);
            // the font registers on the first draw, don't keep a measure taken before that
            if (w > 0f && h > 0f)
            {
                cells[size] = cell;
            }
        }
        return cell;
    }

    // characters the font doesn't have come from a fallback font with another width,
    // those get drawn one by one in their own cell
    public static bool Fits(int size, char ch)
    {
        Check();
        // the built in font is monospaced, others might not be, so their letters get measured too
        if (ch < 128 && IsBuiltIn)
        {
            return true;
        }
        if (!fits.TryGetValue((size, ch), out bool ok))
        {
            Vector2 cell = Cell(size);
            float w = Style(size).CalcSize(new GUIContent(new string(ch, 20))).x / 20f;
            ok = Mathf.Abs(w - cell.x) < 0.05f;
            if (cell.x > 0f && w > 0f)
            {
                fits[(size, ch)] = ok;
            }
        }
        return ok;
    }
}
