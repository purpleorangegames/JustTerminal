using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace JustTerminal;

// shared by the bottom bar window and the pinned one
public static class Terminal
{
    // the terminal window that gets the keys, only after a click on it
    public static Window Focused;

    private static Shell shell;
    private static Game shellGame;

    public static Shell Shell
    {
        get
        {
            // a new shell for each game
            if (shell == null || shellGame != Current.Game)
            {
                shell = new Shell();
                shellGame = Current.Game;
            }
            return shell;
        }
    }

    public static bool TakesKeys => Focused != null && Focused.IsOpen;

    public static bool IsTerminal(Window w) => w is MainTabWindow_Terminal || w is Window_TerminalPinned;

    public static Window Open => Find.WindowStack?.Windows.FirstOrDefault(IsTerminal);

    // a click on a terminal gives it the keys, a click anywhere else takes them away
    public static void ClickAt(Vector2 pos)
    {
        // the click that opened it or the bottom bar button that brought it forward doesn't take the keys away
        if (Time.frameCount - giveFrame <= 1)
        {
            return;
        }
        Window w = Find.WindowStack?.GetWindowAt(pos);
        Focused = IsTerminal(w) ? w : null;
        if (TestHooks.Debug)
        {
            Log.Message($"[JustTerminal] click at {pos} window {w?.GetType().Name} focused {Focused?.GetType().Name}");
        }
    }

    // clicks inside windows go straight to the window and never reach the game's root GUI,
    // so the mouse buttons are checked every frame here too
    public static void CheckMouse(Window owner)
    {
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            ClickAt(UI.MousePositionOnUIInverted);
        }
        // the last place and size, so unpinning or opening again keeps them
        LastRect = owner.windowRect;
    }

    public static Rect? LastRect;
    private static int giveFrame = -10;

    public static void Give(Window w)
    {
        Focused = w;
        giveFrame = Time.frameCount;
    }

    // JustDesktop remembers where its windows were dragged, its own reset clears that
    private static readonly System.Reflection.MethodInfo desktopReset = HarmonyLib.AccessTools.Method("JustDesktop.WindowFrame:ResetPlace");

    // our part of a reset: forget the place, a pinned window goes back to the middle
    public static void ResetOwn()
    {
        LastRect = null;
        if (Find.WindowStack?.WindowOfType<Window_TerminalPinned>() is Window pinned)
        {
            pinned.windowRect = DefaultRect;
        }
    }

    // the open terminal goes back where it first opens, at its first size
    public static void ResetPlace()
    {
        ResetOwn();
        MainButtonDef def = DefDatabase<MainButtonDef>.GetNamed("JTM_Terminal");
        if (desktopReset != null)
        {
            // resizes and places the open tab window too
            desktopReset.Invoke(null, new object[] { def });
            return;
        }
        if (Open is MainTabWindow_Terminal tab)
        {
            tab.Close(false);
            LastRect = null;
            Find.MainTabsRoot.SetCurrentTab(def, false);
        }
    }

    public static Rect DefaultRect => new Rect((UI.screenWidth - 980f) / 2f, (UI.screenHeight - 620f) / 2f, 980f, 620f);
}

// draws the monitor and passes keys to the shell
public class TerminalView
{
    private readonly Screen screen = new Screen();
    private float openedAt;
    private float lastDraw = -1f;

    public void Opened()
    {
        openedAt = Time.realtimeSinceStartup;
        lastDraw = -1f;
    }

    public void Draw(Window owner, Rect inRect, bool pinned, Action togglePin)
    {
        int fontSize = JustTerminalMod.Settings.fontSize;
        Shell sh = Terminal.Shell;
        Phosphor p = (sh.EditMode || sh.Mode?.red == true) && JustTerminalMod.Settings.editRed ? Phosphor.Red : Phosphor.Get(JustTerminalMod.Settings.phosphor);
        bool focused = Terminal.Focused == owner;
        Rect glass = Crt.DrawFrame(inRect, p);
        Rect area = glass.ContractedBy(16f, 14f);
        Vector2 cell = TermFont.Cell(fontSize);
        if (cell.x > 0f && cell.y > 0f)
        {
            if (screen.Resize((int)(area.width / cell.x), (int)(area.height / cell.y)))
            {
                lastDraw = -1f;
            }
            sh.Cols = screen.Cols;
            sh.RunStartup();
        }

        PinButton(inRect, pinned, togglePin);
        if (!focused)
        {
            Crt.FrameLabel(new Rect(inRect.x + Crt.Frame + 6f, inRect.yMax - Crt.Frame, 200f, Crt.Frame), "click to type", TextAnchor.MiddleLeft, false);
        }

        Event e = Event.current;
        if (e.type == EventType.ScrollWheel && Mouse.IsOver(glass))
        {
            sh.ScrollBy(e.delta.y < 0f ? 3 : -3);
            lastDraw = -1f;
            e.Use();
        }
        if (e.type == EventType.MouseDown && e.button == 0 && sh.App != null && Mouse.IsOver(area) && cell.x > 0f && cell.y > 0f)
        {
            Vector2 at = e.mousePosition - area.position;
            sh.App.Click((int)(at.x / cell.x), (int)(at.y / cell.y));
            sh.Redraw = true;
            e.Use();
        }
        if (TestHooks.Debug && e.type == EventType.KeyDown)
        {
            Log.Message($"[JustTerminal] key {e.keyCode} char {(int)e.character} focused {focused}");
        }
        if (focused && e.type == EventType.KeyDown)
        {
            lastDraw = -1f;
            if (e.keyCode == KeyCode.Escape && sh.App == null)
            {
                owner.Close();
                e.Use();
                return;
            }
            if (sh.HandleKey(e))
            {
                e.Use();
            }
            if (sh.WantsClose)
            {
                sh.WantsClose = false;
                owner.Close();
                return;
            }
        }

        if (e.type == EventType.Repaint)
        {
            bool cursor = focused && (Time.realtimeSinceStartup - openedAt) % 1.06f < 0.6f;
            // programs redraw a few times a second, they read a lot of the game
            float t = Time.realtimeSinceStartup;
            if (sh.App == null || sh.Redraw || lastDraw < 0f || t - lastDraw > sh.App.RedrawEvery)
            {
                sh.Redraw = false;
                sh.Render(screen, cursor);
                lastDraw = t;
            }
            // tube warming up when it opens
            float alpha = JustTerminalMod.Settings.warmup ? Mathf.Clamp01((Time.realtimeSinceStartup - openedAt) / 0.25f) : 1f;
            Crt.DrawText(area, screen, p, fontSize, alpha);
            Crt.DrawGlass(glass, p);
            Crt.DrawCorners(glass);
        }
    }

    private static void FontButton(Rect r, string label, int change, string tip)
    {
        Crt.FrameLabel(r, label, TextAnchor.MiddleCenter, Mouse.IsOver(r));
        TooltipHandler.TipRegion(r, tip);
        if (Widgets.ButtonInvisible(r))
        {
            Shell.FontSize(change);
        }
    }

    // a little light with "pin" on the top of the frame, like a monitor's power light
    private static void PinButton(Rect inRect, bool pinned, Action togglePin)
    {
        Rect hit = new Rect(inRect.xMax - Crt.Frame - 60f, inRect.y, 60f, Crt.Frame);
        FontButton(new Rect(hit.x - 26f, hit.y, 24f, hit.height), "A+", 1, "Bigger text (ctrl +)");
        FontButton(new Rect(hit.x - 52f, hit.y, 24f, hit.height), "A-", -1, "Smaller text (ctrl -)");
        bool over = Mouse.IsOver(hit);
        Crt.FrameLabel(new Rect(hit.x, hit.y, 44f, hit.height), "pin", TextAnchor.MiddleRight, over);
        Crt.Led(new Rect(hit.xMax - 10f, hit.y + (hit.height - 8f) / 2f, 8f, 8f), pinned);
        TooltipHandler.TipRegion(hit, pinned
            ? "Pinned, stays open when you click elsewhere or open other tabs. Click to unpin."
            : "Pin it so it stays open when you click elsewhere or open other tabs.");
        if (Widgets.ButtonInvisible(hit))
        {
            togglePin();
        }
    }
}

public class MainTabWindow_Terminal : MainTabWindow
{
    private readonly TerminalView view = new TerminalView();

    public override Vector2 RequestedTabSize => new Vector2(980f, 620f);

    protected override float Margin => 0f;

    protected override void SetInitialSizeAndPosition()
    {
        base.SetInitialSizeAndPosition();
        if (LastRectFits(out Rect r))
        {
            windowRect = r;
        }
    }

    private static bool LastRectFits(out Rect r)
    {
        r = Terminal.LastRect ?? default;
        return Terminal.LastRect != null && r.width >= 300f && r.height >= 200f && r.xMax <= UI.screenWidth + 1f && r.yMax <= UI.screenHeight + 1f && r.x >= -1f && r.y >= -1f;
    }

    public MainTabWindow_Terminal()
    {
        doWindowBackground = false;
        closeOnAccept = false;
        closeOnCancel = false;
        resizeable = true;
    }

    public override void PostOpen()
    {
        base.PostOpen();
        Terminal.Give(this);
        view.Opened();
    }

    public override void PostClose()
    {
        base.PostClose();
        if (Terminal.Focused == this)
        {
            Terminal.Focused = null;
        }
    }

    public override void WindowUpdate()
    {
        base.WindowUpdate();
        Terminal.CheckMouse(this);
        preventCameraMotion = Terminal.Focused == this;
    }

    public override void DoWindowContents(Rect inRect)
    {
        view.Draw(this, inRect, false, Pin);
    }

    public void Pin()
    {
        JustTerminalMod.Settings.pinned = true;
        JustTerminalMod.Save();
        Rect rect = windowRect;
        Close(false);
        Find.WindowStack.Add(new Window_TerminalPinned(rect));
    }
}

// the terminal as a normal window, it doesn't close when the bottom bar tabs change or the map is clicked
public class Window_TerminalPinned : Window
{
    private readonly TerminalView view = new TerminalView();
    private readonly Rect startRect;

    protected override float Margin => 0f;

    public override Vector2 InitialSize => startRect.size;

    public Window_TerminalPinned(Rect rect)
    {
        startRect = rect;
        layer = WindowLayer.GameUI;
        doWindowBackground = false;
        closeOnAccept = false;
        closeOnCancel = false;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = false;
        preventCameraMotion = false;
        onlyOneOfTypeAllowed = true;
        draggable = true;
        resizeable = true;
        soundAppear = null;
    }

    protected override void SetInitialSizeAndPosition()
    {
        windowRect = startRect;
    }

    public override void PostOpen()
    {
        base.PostOpen();
        Terminal.Give(this);
        view.Opened();
    }

    public override void PostClose()
    {
        base.PostClose();
        if (Terminal.Focused == this)
        {
            Terminal.Focused = null;
        }
    }

    public override void WindowUpdate()
    {
        base.WindowUpdate();
        Terminal.CheckMouse(this);
        preventCameraMotion = Terminal.Focused == this;
    }

    public override void DoWindowContents(Rect inRect)
    {
        view.Draw(this, inRect, true, Unpin);
    }

    public void Unpin()
    {
        JustTerminalMod.Settings.pinned = false;
        JustTerminalMod.Save();
        Close(false);
        Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed("JTM_Terminal"), false);
    }
}

// the bottom bar button brings a pinned terminal forward instead of opening a second one,
// and opens it pinned again if it was pinned when it closed
public class MainButtonWorker_Terminal : MainButtonWorker_ToggleTab
{
    public override void Activate()
    {
        Window pinned = Find.WindowStack.WindowOfType<Window_TerminalPinned>();
        if (pinned != null)
        {
            Find.WindowStack.Notify_ManuallySetFocus(pinned);
            Terminal.Give(pinned);
            return;
        }
        if (JustTerminalMod.Settings.pinned && !(Find.MainTabsRoot.OpenTab == def))
        {
            Find.WindowStack.Add(new Window_TerminalPinned(Terminal.LastRect ?? Terminal.DefaultRect));
            return;
        }
        base.Activate();
    }
}
