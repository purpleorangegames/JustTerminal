using HarmonyLib;
using UnityEngine;
using Verse;

namespace JustTerminal;

// a click decides if the terminal has the keys, before anything else sees the click
[HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootOnGUI))]
public static class UIRoot_UIRootOnGUI_Patch
{
    public static void Prefix()
    {
        if (Event.current.type == EventType.MouseDown)
        {
            Terminal.ClickAt(UI.MousePositionOnUIInverted);
        }
    }
}

// while typing, game hotkeys (pause, speed, tabs, camera) don't fire
[HarmonyPatch(typeof(KeyBindingDef), nameof(KeyBindingDef.KeyDownEvent), MethodType.Getter)]
public static class KeyBindingDef_KeyDownEvent_Patch
{
    public static bool Prefix(ref bool __result)
    {
        if (Terminal.TakesKeys)
        {
            __result = false;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(KeyBindingDef), nameof(KeyBindingDef.IsDownEvent), MethodType.Getter)]
public static class KeyBindingDef_IsDownEvent_Patch
{
    public static bool Prefix(ref bool __result) => KeyBindingDef_KeyDownEvent_Patch.Prefix(ref __result);
}

[HarmonyPatch(typeof(KeyBindingDef), nameof(KeyBindingDef.IsDown), MethodType.Getter)]
public static class KeyBindingDef_IsDown_Patch
{
    public static bool Prefix(ref bool __result) => KeyBindingDef_KeyDownEvent_Patch.Prefix(ref __result);
}

[HarmonyPatch(typeof(KeyBindingDef), nameof(KeyBindingDef.JustPressed), MethodType.Getter)]
public static class KeyBindingDef_JustPressed_Patch
{
    public static bool Prefix(ref bool __result) => KeyBindingDef_KeyDownEvent_Patch.Prefix(ref __result);
}

// any other window opening (a dialog, a menu, a letter) takes the keys back from the terminal
[HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
public static class WindowStack_Add_Patch
{
    public static void Postfix(Window window)
    {
        if (!Terminal.IsTerminal(window) && !(window is ImmediateWindow))
        {
            Terminal.Focused = null;
        }
    }
}
