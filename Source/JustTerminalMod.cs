using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace JustTerminal;

public class JustTerminalSettings : ModSettings
{
    public string phosphor = "green";
    public int fontSize = 15;
    public bool saveRecordings = true;
    public bool editRed = true;
    public bool pinned;

    // look
    public CustomTheme custom = CustomTheme.From(Phosphor.All[0]);
    // how strong each effect is, 1 is how it comes, 0 is off
    public float glow = 1f;
    public float scanlines = 1f;
    public float vignette = 1f;
    public float band = 1f;
    public float reflection = 1f;
    public bool warmup = true;
    // commands run when a new terminal starts, separated by ;
    public string startup = "";
    // "" is the font that comes with the mod, os:<name> an installed font, file:<name> one from the fonts folder
    public string font = "";

    // text
    public const string DefaultWelcome = "JustTerminal 0.1\nType help to see the commands, manual to read the manual.";
    public string welcome = DefaultWelcome;
    public string promptUser = "rim";
    // empty uses the colony name
    public string promptHost = "";

    // command name -> the name the player gave it, edit commands keyed edit:<name>
    public Dictionary<string, string> renames = new Dictionary<string, string>();
    // short names for longer command lines, the same in every game
    public Dictionary<string, string> aliases = new Dictionary<string, string>();
    public bool keySounds;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref phosphor, "phosphor", "green");
        Scribe_Values.Look(ref fontSize, "fontSize", 15);
        Scribe_Values.Look(ref saveRecordings, "saveRecordings", true);
        Scribe_Values.Look(ref editRed, "editRed", true);
        Scribe_Values.Look(ref pinned, "pinned");
        Scribe_Deep.Look(ref custom, "custom");
        Scribe_Values.Look(ref glow, "glowStrength", 1f);
        Scribe_Values.Look(ref scanlines, "scanlineStrength", 1f);
        Scribe_Values.Look(ref vignette, "vignetteStrength", 1f);
        Scribe_Values.Look(ref band, "bandStrength", 1f);
        Scribe_Values.Look(ref reflection, "reflectionStrength", 1f);
        Scribe_Values.Look(ref startup, "startup", "");
        Scribe_Values.Look(ref warmup, "warmup", true);
        Scribe_Values.Look(ref font, "font", "");
        Scribe_Values.Look(ref welcome, "welcome", DefaultWelcome);
        Scribe_Values.Look(ref promptUser, "promptUser", "rim");
        Scribe_Values.Look(ref promptHost, "promptHost", "");
        Scribe_Collections.Look(ref renames, "renames", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref aliases, "aliases", LookMode.Value, LookMode.Value);
        Scribe_Values.Look(ref keySounds, "keySounds");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            custom ??= CustomTheme.From(Phosphor.All[0]);
            renames ??= new Dictionary<string, string>();
            aliases ??= new Dictionary<string, string>();
            font ??= "";
            welcome ??= DefaultWelcome;
            promptUser ??= "rim";
            promptHost ??= "";
            startup ??= "";
        }
    }
}

public class JustTerminalMod : Mod
{
    public static JustTerminalSettings Settings;
    public static JustTerminalMod Instance;

    public JustTerminalMod(ModContentPack content) : base(content)
    {
        Instance = this;
        Settings = GetSettings<JustTerminalSettings>();
        TermFont.Load(content);
        Programs.Load(content);
        Harmony harmony = new Harmony("tiagocc0.JustTerminal");
        harmony.PatchAll();
        // JustDesktop's right click on a taskbar button puts the window back in its first place and size, the terminal forgets its own place then too
        if (AccessTools.Method("JustDesktop.WindowFrame:ResetPlace") is System.Reflection.MethodInfo reset)
        {
            harmony.Patch(reset, prefix: new HarmonyMethod(typeof(JustTerminalMod), nameof(DesktopReset)));
        }
    }

    public static void Save() => Instance.WriteSettings();

    private static void DesktopReset(MainButtonDef def)
    {
        if (def?.defName == "JTM_Terminal")
        {
            Terminal.ResetOwn();
        }
    }

    public override string SettingsCategory() => "JustTerminal";

    public override void DoSettingsWindowContents(Rect inRect) => Options.Draw(inRect);

    public static void OpenOptions() => Find.WindowStack.Add(new Dialog_ModSettings(Instance));
}

// with the option off the recorder is left out of the save, so the save has nothing from this mod
[HarmonyPatch(typeof(Game), nameof(Game.ExposeData))]
public static class Game_ExposeData_Patch
{
    public static void Prefix(Game __instance, out Recorder __state)
    {
        __state = null;
        if (Scribe.mode == LoadSaveMode.Saving && !JustTerminalMod.Settings.saveRecordings)
        {
            __state = __instance.GetComponent<Recorder>();
            if (__state != null)
            {
                __instance.components.Remove(__state);
            }
        }
    }

    public static void Finalizer(Game __instance, Recorder __state)
    {
        if (__state != null && !__instance.components.Contains(__state))
        {
            __instance.components.Add(__state);
        }
    }
}
