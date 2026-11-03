using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace JustTerminal;

// the mod options page: general, look, text and command names
public static class Options
{
    private enum Tab
    {
        General,
        Look,
        Text,
        Commands
    }

    private static Tab tab = Tab.General;
    private static Vector2 lookScroll;
    private static Vector2 commandScroll;
    private static float lookHeight = 900f;
    private static readonly Dictionary<string, string> hexBuffers = new Dictionary<string, string>();
    private static readonly Screen preview = new Screen();

    private static JustTerminalSettings Set => JustTerminalMod.Settings;

    public static void Draw(Rect inRect)
    {
        Rect body = new Rect(inRect.x, inRect.y + 32f, inRect.width, inRect.height - 32f);
        List<TabRecord> tabs = new List<TabRecord>
        {
            new TabRecord("JTM_TabGeneral".Translate(), () => tab = Tab.General, tab == Tab.General),
            new TabRecord("JTM_TabLook".Translate(), () => tab = Tab.Look, tab == Tab.Look),
            new TabRecord("JTM_TabText".Translate(), () => tab = Tab.Text, tab == Tab.Text),
            new TabRecord("JTM_TabCommands".Translate(), () => tab = Tab.Commands, tab == Tab.Commands)
        };
        Widgets.DrawMenuSection(body);
        TabDrawer.DrawTabs(body, tabs, 1, 160f);
        Rect content = body.ContractedBy(12f);
        switch (tab)
        {
            case Tab.Look:
                try
                {
                    Look(content);
                }
                catch (Exception e)
                {
                    Log.ErrorOnce("[JustTerminal] options look tab: " + e, 0x4a544d02);
                }
                break;
            case Tab.Text:
                Text(content);
                break;
            case Tab.Commands:
                CommandNames(content);
                break;
            default:
                General(content);
                break;
        }
    }

    private static void General(Rect rect)
    {
        Listing_Standard list = new Listing_Standard();
        list.Begin(rect.LeftPart(0.5f));
        list.CheckboxLabeled("JTM_EditRed".Translate(), ref Set.editRed, "JTM_EditRedDesc".Translate());
        list.CheckboxLabeled("JTM_KeySounds".Translate(), ref Set.keySounds, "JTM_KeySoundsDesc".Translate());
        list.Gap();
        list.CheckboxLabeled("JTM_SaveRecordings".Translate(), ref Set.saveRecordings, "JTM_SaveRecordingsDesc".Translate());
        list.Gap();
        Recorder rec = Current.ProgramState == ProgramState.Playing ? Recorder.Current : null;
        if (rec != null)
        {
            list.Label("JTM_Recorded".Translate(rec.HoursRecorded));
        }
        if (list.ButtonText("JTM_ClearRecordings".Translate()))
        {
            if (rec == null)
            {
                Messages.Message("JTM_NoGame".Translate(), MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("JTM_ClearRecordingsConfirm".Translate(), () =>
                {
                    rec.Clear();
                    Messages.Message((Set.saveRecordings ? "JTM_ClearedSaving" : "JTM_ClearedNotSaving").Translate(), MessageTypeDefOf.NeutralEvent, false);
                }, destructive: true));
            }
        }
        list.Gap();
        if (rec != null)
        {
            list.Label("JTM_FilesCount".Translate(rec.Files.Count - 1));
        }
        if (list.ButtonText("JTM_DeleteFiles".Translate()))
        {
            if (rec == null)
            {
                Messages.Message("JTM_NoGameFiles".Translate(), MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("JTM_DeleteFilesConfirm".Translate(), () =>
                {
                    rec.ClearFiles();
                    Terminal.Shell.Cwd = null;
                }, destructive: true));
            }
        }
        list.Gap();
        list.Label("JTM_ExportAbout".Translate());
        if (list.ButtonText("JTM_Export".Translate()))
        {
            if (rec == null)
            {
                Messages.Message("JTM_NoGameFiles".Translate(), MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                string path = Transfer.Export(rec.Files);
                Messages.Message("JTM_Exported".Translate(path), MessageTypeDefOf.NeutralEvent, false);
            }
        }
        if (list.ButtonText("JTM_Import".Translate()))
        {
            List<string> exports = Transfer.Exports();
            if (rec == null)
            {
                Messages.Message("JTM_NoGameFiles".Translate(), MessageTypeDefOf.RejectInput, false);
            }
            else if (exports.Count == 0)
            {
                Messages.Message("JTM_NothingExported".Translate(), MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                Find.WindowStack.Add(new FloatMenu(exports.Select(path => new FloatMenuOption("JTM_ExportEntry".Translate(Path.GetFileNameWithoutExtension(path), Transfer.Count(path), File.GetLastWriteTime(path).ToString("g")), () =>
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("JTM_ImportConfirm".Translate(Path.GetFileNameWithoutExtension(path)), () =>
                    {
                        Messages.Message("JTM_Imported".Translate(Transfer.Import(path, rec.Files)), MessageTypeDefOf.NeutralEvent, false);
                    }));
                })).ToList()));
            }
        }
        if (list.ButtonText("JTM_OpenExports".Translate()))
        {
            OpenFolder(Transfer.FolderPath);
        }
        list.End();
    }

    // look: colors, effects and font on the left, a preview on the right

    private static readonly string[] presets = { "green", "amber", "white", "custom" };

    private static void Look(Rect rect)
    {
        Rect left = rect.LeftPart(0.48f);
        Rect right = rect.RightPart(0.5f);
        Rect view = new Rect(0f, 0f, left.width - 18f, lookHeight);
        Widgets.BeginScrollView(left, ref lookScroll, view);
        Listing_Standard list = new Listing_Standard();
        // a listing starts a second column when it runs out of height, so it gets all the height it wants
        list.Begin(new Rect(0f, 0f, view.width, 100000f));

        list.Label("JTM_Colors".Translate());
        foreach (string name in presets)
        {
            if (list.RadioButton(name, Set.phosphor == name))
            {
                Set.phosphor = name;
            }
        }
        list.Gap(6f);
        if (Set.phosphor == "custom")
        {
            Rect copy = list.GetRect(28f);
            Widgets.Label(copy.LeftPart(0.34f), "JTM_StartFrom".Translate());
            float bx = copy.x + copy.width * 0.34f;
            float bw = (copy.width * 0.66f - 8f) / 3f;
            foreach (Phosphor p in Phosphor.All)
            {
                if (Widgets.ButtonText(new Rect(bx, copy.y, bw, copy.height), p.name))
                {
                    Set.custom = CustomTheme.From(p);
                    hexBuffers.Clear();
                }
                bx += bw + 4f;
            }
            list.Gap(4f);
            CustomTheme c = Set.custom;
            c.ToPhosphor();
            ColorRow(list, "JTM_Glass".Translate(), "glass", () => c.glass, v => c.glass = v);
            ColorRow(list, "JTM_Frame".Translate(), "frame", () => c.frame, v => c.frame = v);
            for (int i = 0; i < Phosphor.InkNames.Length; i++)
            {
                int index = i;
                ColorRow(list, "JTM_InkText".Translate(Phosphor.InkNames[i]), "ink" + i, () => c.inks[index], v => c.inks[index] = v);
            }
        }
        list.GapLine();

        list.Label("JTM_Effects".Translate());
        Strength(list, "JTM_Glow".Translate(), ref Set.glow);
        Strength(list, "JTM_Scanlines".Translate(), ref Set.scanlines);
        Strength(list, "JTM_Vignette".Translate(), ref Set.vignette);
        Strength(list, "JTM_Band".Translate(), ref Set.band);
        Strength(list, "JTM_Reflection".Translate(), ref Set.reflection);
        list.CheckboxLabeled("JTM_Warmup".Translate(), ref Set.warmup);
        list.GapLine();

        list.Label("JTM_Font".Translate());
        Rect fontRow = list.GetRect(30f);
        if (Widgets.ButtonText(fontRow.LeftPart(0.6f), FontLabel(Set.font)))
        {
            FontMenu(false);
        }
        if (Widgets.ButtonText(fontRow.RightPart(0.38f), "JTM_AllInstalled".Translate()))
        {
            FontMenu(true);
        }
        if (list.ButtonText("JTM_OpenFonts".Translate()))
        {
            OpenFolder(TermFont.FolderPath);
        }
        list.Label("JTM_FontsAbout".Translate(), tooltip: TermFont.FolderPath);
        list.Gap(6f);
        list.Label("JTM_TextSize".Translate(Set.fontSize));
        Set.fontSize = Mathf.RoundToInt(list.Slider(Set.fontSize, 10f, 28f));

        lookHeight = list.CurHeight + 20f;
        list.End();
        Widgets.EndScrollView();

        Preview(new Rect(right.x, right.y, right.width, Mathf.Min(right.height, right.width * 0.72f)));
    }

    // 0 is off, 100% how it comes, up to 400%
    private static void Strength(Listing_Standard list, string label, ref float value)
    {
        Rect row = list.GetRect(26f);
        Widgets.Label(row.LeftPart(0.45f), label);
        Rect slider = new Rect(row.x + row.width * 0.45f, row.y + 4f, row.width * 0.38f, 20f);
        value = Widgets.HorizontalSlider(slider, value, 0f, 4f, roundTo: 0.05f);
        Widgets.Label(new Rect(slider.xMax + 8f, row.y, row.width * 0.17f - 8f, 26f), value <= 0f ? "JTM_Off".Translate().ToString() : (value * 100f).ToString("0") + "%");
    }

    private static void ColorRow(Listing_Standard list, string label, string key, Func<Color> get, Action<Color> set)
    {
        Rect row = list.GetRect(26f);
        Widgets.Label(row.LeftPart(0.4f), label);
        Rect swatch = new Rect(row.x + row.width * 0.4f, row.y + 2f, 40f, 22f);
        Widgets.DrawBoxSolid(swatch, get());
        Widgets.DrawBox(swatch);
        if (Widgets.ButtonInvisible(swatch))
        {
            Find.WindowStack.Add(new Dialog_TermColor(label, get(), v =>
            {
                set(v);
                hexBuffers.Remove(key);
            }));
        }
        Rect hex = new Rect(swatch.xMax + 8f, row.y + 1f, 90f, 24f);
        if (!hexBuffers.TryGetValue(key, out string buffer))
        {
            buffer = "#" + ColorUtility.ToHtmlStringRGB(get());
        }
        string typed = Widgets.TextField(hex, buffer, 7);
        hexBuffers[key] = typed;
        if (typed != buffer && typed.Length == 7 && ColorUtility.TryParseHtmlString(typed, out Color parsed))
        {
            set(parsed);
        }
    }

    private static string FontLabel(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return "DejaVu Sans Mono";
        }
        return key.Substring(key.IndexOf(':') + 1);
    }

    private static void FontMenu(bool all)
    {
        List<FloatMenuOption> options = new List<FloatMenuOption>
        {
            new FloatMenuOption(FontLabel(""), () => Set.font = "")
        };
        foreach (string file in TermFont.FolderFonts())
        {
            options.Add(new FloatMenuOption(file + " (fonts folder)", () => Set.font = "file:" + file));
        }
        foreach (string name in TermFont.OsFonts(all))
        {
            options.Add(new FloatMenuOption(name, () => Set.font = "os:" + name));
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            if (Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor)
            {
                Process.Start("xdg-open", "\"" + path + "\"");
            }
            else
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
        catch (Exception e)
        {
            Messages.Message("JTM_CantOpen".Translate(path, e.Message), MessageTypeDefOf.RejectInput, false);
        }
    }

    // a small terminal with every color in it, drawn like the real one
    private static void Preview(Rect rect)
    {
        Phosphor p = Phosphor.Get(Set.phosphor);
        Rect glass = Crt.DrawFrame(rect, p);
        Rect area = glass.ContractedBy(14f, 12f);
        Vector2 cell = TermFont.Cell(Set.fontSize);
        if (cell.x <= 0f || cell.y <= 0f || Event.current.type != EventType.Repaint)
        {
            return;
        }
        preview.Resize((int)(area.width / cell.x), (int)(area.height / cell.y));
        preview.Clear();
        int y = 0;
        string[] welcome = (Set.welcome ?? "").Replace("\\n", "\n").Split('\n');
        foreach (string line in welcome)
        {
            preview.Write(0, y++, line, y == 1 ? Ink.Bright : Ink.Dim);
        }
        y++;
        string prompt = Terminal.Shell.Prompt;
        int x = preview.Write(0, y, prompt, Ink.Accent);
        x = preview.Write(x, y, ":~$ ", Ink.Dim);
        preview.Write(x, y++, "pawns", Ink.Bright);
        preview.Write(0, y++, "NAME       MOOD", Ink.Dim);
        Row(y++, "Tasca", 0.9f, Ink.Good);
        Row(y++, "Drill", 0.45f, Ink.Warn);
        Row(y++, "Wolf", 0.15f, Ink.Bad);
        preview.Write(0, y++, "accent text", Ink.Accent);
        x = preview.Write(0, y, prompt, Ink.Accent);
        x = preview.Write(x, y, ":~$ ", Ink.Dim);
        preview.Put(x, y, ' ', Ink.Screen, Ink.Normal);
        Crt.DrawText(area, preview, p, Set.fontSize, 1f);
        Crt.DrawGlass(glass, p);
        Crt.DrawCorners(glass);
    }

    private static void Row(int y, string name, float value, Ink ink)
    {
        preview.Write(0, y, name, Ink.Normal);
        App.Bar(preview, 11, y, value, 8, ink);
        preview.Write(20, y, (value * 100f).ToString("0"), ink);
    }

    // text: welcome lines and the prompt

    private static void Text(Rect rect)
    {
        Listing_Standard list = new Listing_Standard();
        list.Begin(rect.LeftPart(0.6f));
        list.Label("JTM_WelcomeAbout".Translate());
        Rect area = list.GetRect(90f);
        Set.welcome = Widgets.TextArea(area, Set.welcome);
        if (list.ButtonText("JTM_DefaultWelcome".Translate()))
        {
            Set.welcome = JustTerminalSettings.DefaultWelcome;
        }
        list.GapLine();
        list.Label("JTM_StartupAbout".Translate());
        Set.startup = Widgets.TextField(list.GetRect(28f), Set.startup);
        list.GapLine();
        list.Label("JTM_PromptLike".Translate(Terminal.Shell.Prompt + ":~$"));
        Set.promptUser = list.TextEntryLabeled("JTM_BeforeAt".Translate(), Set.promptUser);
        Set.promptHost = list.TextEntryLabeled("JTM_AfterAt".Translate(), Set.promptHost);
        list.Label("JTM_EmptyHost".Translate());
        list.End();
    }

    // commands: a new name for each, the old one keeps working

    private static void CommandNames(Rect rect)
    {
        List<Command> all = Commands.All.Concat(EditCommands.All).ToList();
        Rect top = new Rect(rect.x, rect.y, rect.width, 30f);
        Widgets.Label(top.LeftPart(0.7f), "JTM_RenameAbout".Translate());
        if (Widgets.ButtonText(top.RightPart(0.25f), "JTM_AllNamesBack".Translate()))
        {
            Set.renames.Clear();
        }
        Rect outer = new Rect(rect.x, rect.y + 36f, rect.width, rect.height - 36f);
        Rect view = new Rect(0f, 0f, outer.width - 18f, all.Count * 28f);
        Widgets.BeginScrollView(outer, ref commandScroll, view);
        HashSet<string> taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < all.Count; i++)
        {
            Command c = all[i];
            Rect row = new Rect(0f, i * 28f, view.width, 26f);
            if (i % 2 == 0)
            {
                Widgets.DrawLightHighlight(row);
            }
            Widgets.Label(new Rect(row.x + 6f, row.y + 2f, 200f, 24f), (c.edit ? "JTM_EditModeName".Translate(c.name).ToString() : c.name));
            Set.renames.TryGetValue(c.Key, out string current);
            string typed = Widgets.TextField(new Rect(row.x + 220f, row.y + 1f, 180f, 24f), current ?? "", 24);
            typed = new string(typed.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
            if (typed.Length == 0)
            {
                Set.renames.Remove(c.Key);
            }
            else
            {
                Set.renames[c.Key] = typed;
            }
            // two commands with the same name in the same mode can't both work
            string shown = (c.edit ? "edit:" : "") + c.Shown;
            if (!taken.Add(shown))
            {
                GUI.color = ColorLibrary.RedReadable;
                Widgets.Label(new Rect(row.x + 410f, row.y + 2f, 300f, 24f), "JTM_NameTaken".Translate());
                GUI.color = Color.white;
            }
        }
        Widgets.EndScrollView();
    }
}

// a color wheel, brightness and hex, changes apply while you pick
public class Dialog_TermColor : Window
{
    private readonly string label;
    private readonly Action<Color> apply;
    private readonly Color start;
    private Color color;
    private bool dragging;
    private string hex;

    public override Vector2 InitialSize => new Vector2(320f, 420f);

    public Dialog_TermColor(string label, Color color, Action<Color> apply)
    {
        this.label = label;
        this.color = color;
        start = color;
        this.apply = apply;
        hex = "#" + ColorUtility.ToHtmlStringRGB(color);
        doCloseX = true;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Verse.Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), label);
        Verse.Text.Font = GameFont.Small;
        Rect wheel = new Rect(inRect.x + (inRect.width - 200f) / 2f, inRect.y + 40f, 200f, 200f);
        Color.RGBToHSV(color, out float h, out float s, out float v);
        Color before = color;
        Widgets.HSVColorWheel(wheel, ref color, ref dragging, v);
        Color.RGBToHSV(color, out h, out s, out _);
        float nv = Widgets.HorizontalSlider(new Rect(inRect.x, wheel.yMax + 14f, inRect.width, 24f), v, 0f, 1f, label: "JTM_Brightness".Translate());
        color = Color.HSVToRGB(h, s, nv);
        Rect swatch = new Rect(inRect.x, wheel.yMax + 50f, 60f, 30f);
        Widgets.DrawBoxSolid(swatch, color);
        Widgets.DrawBox(swatch);
        string typed = Widgets.TextField(new Rect(swatch.xMax + 10f, swatch.y + 3f, 100f, 24f), hex, 7);
        if (typed != hex)
        {
            hex = typed;
            if (typed.Length == 7 && ColorUtility.TryParseHtmlString(typed, out Color parsed))
            {
                color = parsed;
            }
        }
        else if (color != before)
        {
            hex = "#" + ColorUtility.ToHtmlStringRGB(color);
        }
        if (color != before)
        {
            apply(color);
        }
        Rect buttons = new Rect(inRect.x, inRect.yMax - 32f, inRect.width, 32f);
        if (Widgets.ButtonText(buttons.LeftHalf().ContractedBy(2f), "JTM_BackToBefore".Translate()))
        {
            color = start;
            hex = "#" + ColorUtility.ToHtmlStringRGB(color);
            apply(color);
        }
        if (Widgets.ButtonText(buttons.RightHalf().ContractedBy(2f), "OK"))
        {
            Close();
        }
    }
}
