using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace JustTerminal;

// for testing without touching the game, all off unless the environment variable is set:
// JTM_TEST_LOAD=<save>    loads that save from the main menu and pauses it
// JTM_TEST_CMDS=<a;b;c>   then opens the terminal and runs each command, key:<KeyCode> presses a key, type:<text> types, link:N links the first N storage buildings, waitapp waits while a program runs, answer:<text> answers only when asked, break:<name> starts a sad wander, rclick:x,y rtype:<text> rkey:<Key> go through the game's real input, pin, tab:<def>, clickmap
// JTM_TEST_SHOTS=<dir>    with a screenshot after each in dir, and quits
// JTM_TEST_RUN=<frames>   lets time run that many frames at the fastest speed first
// JTM_TEST_SAVEAS=<name>  then saves the game with that name
// JTM_TEST_SIZE=<w>x<h>   terminal window size
public static class TestHooks
{
    private static readonly string loadName = Environment.GetEnvironmentVariable("JTM_TEST_LOAD");
    private static readonly string commands = Environment.GetEnvironmentVariable("JTM_TEST_CMDS");
    private static readonly string shotDir = Environment.GetEnvironmentVariable("JTM_TEST_SHOTS");
    private static readonly int runFrames = int.TryParse(Environment.GetEnvironmentVariable("JTM_TEST_RUN"), out int f) ? f : 0;
    private static readonly string saveAs = Environment.GetEnvironmentVariable("JTM_TEST_SAVEAS");
    public static readonly bool Debug = Environment.GetEnvironmentVariable("JTM_TEST_LOAD") != null;
    private static bool loadStarted;
    private static Action release;
    private static Action middle;
    // real input has to go in right after the frame starts, or the frame start clears it
    public static Action input;
    private static int playingFrames;
    private static int held;

    public static void Tick()
    {
        if (loadName == null)
        {
            return;
        }
        // new means the game was started with -quicktest, it makes its own colony
        if (!loadStarted && loadName != "new" && Current.ProgramState == ProgramState.Entry && !LongEventHandler.AnyEventNowOrWaiting)
        {
            loadStarted = true;
            GameDataSaveLoader.LoadGame(loadName);
            return;
        }
        bool playing = Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null && !LongEventHandler.AnyEventNowOrWaiting;
        if (!playing)
        {
            return;
        }
        playingFrames++;
        if (playingFrames == 5)
        {
            Find.TickManager.CurTimeSpeed = runFrames > 0 ? TimeSpeed.Ultrafast : TimeSpeed.Paused;
        }
        if (runFrames > 0)
        {
            if (playingFrames == 5 + runFrames)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                if (saveAs != null)
                {
                    GameDataSaveLoader.SaveGame(saveAs);
                }
                Log.Message($"[JustTerminal] test ran to hour {Recorder.HourNow}, food points {Recorder.Current?.Get("food")?.values.Count}");
            }
            if (playingFrames <= 5 + runFrames)
            {
                return;
            }
        }
        int frames = playingFrames - runFrames - held;
        if (frames == 25)
        {
            // the test save can have errors from mods that aren't loaded
            Find.WindowStack.TryRemove(typeof(LudeonTK.EditWindow_Log), false);
        }
        if (frames == 30)
        {
            Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed("JTM_Terminal"));
        }
        if (frames == 32 && Environment.GetEnvironmentVariable("JTM_TEST_SIZE") is string size && Terminal.Open is Window open)
        {
            string[] wh = size.Split('x');
            Rect r = open.windowRect;
            open.windowRect = new Rect(r.x, r.yMax - float.Parse(wh[1]), float.Parse(wh[0]), float.Parse(wh[1]));
        }
        if (commands == null || frames < 60)
        {
            return;
        }
        string[] list = commands.Split(';');
        int step = (frames - 60) / 40;
        int frame = (frames - 60) % 40;
        if (step < list.Length)
        {
            // waitapp holds the steps while a program runs, like the ai thinking, 5 minutes at most
            if (list[step] == "waitapp" && frame == 1 && Terminal.Shell.App != null && held < 18000)
            {
                held++;
                return;
            }
            if (frame == 0)
            {
                if (list[step].StartsWith("rclick:") || list[step].StartsWith("rrclick:"))
                {
                    // a real click through the game's input, x,y from the top left, rrclick is the right button
                    int button = list[step].StartsWith("rr") ? 1 : 0;
                    string[] xy = list[step].Substring(list[step].IndexOf(':') + 1).Split(',');
                    Vector2 pos = new Vector2(float.Parse(xy[0]), UI.screenHeight - float.Parse(xy[1]));
                    input = () =>
                    {
                        UnityEngine.InputSystem.InputSystem.QueueMouseMove(pos);
                        UnityEngine.InputSystem.InputSystem.QueueMouseButton(button, true, pos);
                    };
                    release = () => UnityEngine.InputSystem.InputSystem.QueueMouseButton(button, false, pos);
                }
                else if (list[step].StartsWith("rdrag:"))
                {
                    // press at the first point, move to the second, let go there
                    float[] v = list[step].Substring(6).Split(',').Select(float.Parse).ToArray();
                    Vector2 from = new Vector2(v[0], UI.screenHeight - v[1]);
                    Vector2 to = new Vector2(v[2], UI.screenHeight - v[3]);
                    input = () =>
                    {
                        UnityEngine.InputSystem.InputSystem.QueueMouseMove(from);
                        UnityEngine.InputSystem.InputSystem.QueueMouseButton(0, true, from);
                    };
                    middle = () => UnityEngine.InputSystem.InputSystem.QueueMouseMove(to);
                    release = () => UnityEngine.InputSystem.InputSystem.QueueMouseButton(0, false, to);
                }
                else if (list[step].StartsWith("rchord:"))
                {
                    // keys pressed together, like LeftCtrl,S
                    UnityEngine.InputSystem.Key[] keys = list[step].Substring(7).Split(',').Select(k => (UnityEngine.InputSystem.Key)Enum.Parse(typeof(UnityEngine.InputSystem.Key), k)).ToArray();
                    input = () =>
                    {
                        foreach (UnityEngine.InputSystem.Key k in keys)
                        {
                            UnityEngine.InputSystem.InputSystem.QueueKeyChange(k, true);
                        }
                    };
                    release = () =>
                    {
                        foreach (UnityEngine.InputSystem.Key k in keys.Reverse())
                        {
                            UnityEngine.InputSystem.InputSystem.QueueKeyChange(k, false);
                        }
                    };
                }
                else if (list[step].StartsWith("save:"))
                {
                    GameDataSaveLoader.SaveGame(list[step].Substring(5));
                }
                else if (list[step].StartsWith("rtype:"))
                {
                    foreach (char c in list[step].Substring(6))
                    {
                        UnityEngine.InputSystem.InputSystem.QueueTextInput(c);
                    }
                }
                else if (list[step].StartsWith("rkeytext:"))
                {
                    // a key and its text in the same frame, like the numpad with num lock on
                    string[] kt = list[step].Substring(9).Split(',');
                    UnityEngine.InputSystem.Key key = (UnityEngine.InputSystem.Key)Enum.Parse(typeof(UnityEngine.InputSystem.Key), kt[0]);
                    input = () =>
                    {
                        UnityEngine.InputSystem.InputSystem.QueueKeyChange(key, true);
                        UnityEngine.InputSystem.InputSystem.QueueTextInput(kt[1][0]);
                    };
                    release = () => UnityEngine.InputSystem.InputSystem.QueueKeyChange(key, false);
                }
                else if (list[step].StartsWith("rkey:"))
                {
                    UnityEngine.InputSystem.Key key = (UnityEngine.InputSystem.Key)Enum.Parse(typeof(UnityEngine.InputSystem.Key), list[step].Substring(5));
                    input = () => UnityEngine.InputSystem.InputSystem.QueueKeyChange(key, true);
                    release = () => UnityEngine.InputSystem.InputSystem.QueueKeyChange(key, false);
                }
                else if (list[step] == "unpin")
                {
                    Find.WindowStack.WindowOfType<Window_TerminalPinned>()?.Unpin();
                }
                else if (list[step].StartsWith("incident:"))
                {
                    IncidentDef def = DefDatabase<IncidentDef>.GetNamed(list[step].Substring(9));
                    IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, Find.CurrentMap);
                    parms.forced = true;
                    Log.Message($"[JustTerminal] incident {def.defName}: {def.Worker.TryExecute(parms)}");
                }
                else if (list[step].StartsWith("write:"))
                {
                    // write:name|text, \n is a new line and § stands for ;
                    string[] parts = list[step].Substring(6).Split('|');
                    FileNode root = Files.ParentOf(null, "~/" + parts[0], out string fileName);
                    FileNode f = root.Child(fileName);
                    if (f == null)
                    {
                        f = new FileNode { name = fileName, parent = root };
                        root.children.Add(f);
                    }
                    f.text = parts[1].Replace("\\n", "\n").Replace('§', ';');
                }
                else if (list[step] == "export")
                {
                    Log.Message("[JustTerminal] exported " + Transfer.Export(Files.Root));
                }
                else if (list[step] == "wipe")
                {
                    Recorder.Current.ClearFiles();
                    Terminal.Shell.Cwd = null;
                }
                else if (list[step] == "import")
                {
                    Log.Message("[JustTerminal] import " + Transfer.Import(Transfer.Exports()[0], Files.Root));
                }
                else if (list[step].StartsWith("spawn:"))
                {
                    // extra colonists, to see how fast things are with a big colony
                    Map map = Find.CurrentMap;
                    for (int i = 0; i < int.Parse(list[step].Substring(6)); i++)
                    {
                        Pawn p = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                        GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(map.Center, map, 20), map);
                    }
                }
                else if (list[step].StartsWith("time:"))
                {
                    string what = list[step].Substring(5);
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    if (what == "events")
                    {
                        Events.Look();
                    }
                    else
                    {
                        Shell quiet = new Shell { CanAsk = false };
                        quiet.Cols = 120;
                        quiet.Execute(what);
                    }
                    Log.Message($"[JustTerminal] time {what}: {sw.Elapsed.TotalMilliseconds:0.0} ms");
                }
                else if (list[step].StartsWith("ticks:"))
                {
                    for (int i = 0; i < int.Parse(list[step].Substring(6)); i++)
                    {
                        Find.TickManager.DoSingleTick();
                    }
                }
                else if (list[step].StartsWith("setsave:"))
                {
                    JustTerminalMod.Settings.saveRecordings = list[step].EndsWith("on");
                }
                else if (list[step].StartsWith("link:"))
                {
                    // links the first N storage buildings, like the link button does
                    var members = Find.CurrentMap.listerBuildings.allBuildingsColonist.OfType<IStorageGroupMember>().Take(int.Parse(list[step].Substring(5))).ToList();
                    StorageGroup group = Find.CurrentMap.storageGroups.NewGroup();
                    group.InitFrom(members[0]);
                    foreach (IStorageGroupMember m in members)
                    {
                        m.SetStorageGroup(group);
                    }
                }
                else if (list[step] == "waitapp")
                {
                }
                else if (list[step].StartsWith("answer:"))
                {
                    // only when something is asking, so a test can have more than it needs
                    if (Terminal.Shell.Waiting)
                    {
                        Terminal.Shell.Run(list[step].Substring(7));
                    }
                }
                else if (list[step].StartsWith("break:"))
                {
                    Pawn p = PawnsFinder.AllMaps_FreeColonistsSpawned.FirstOrDefault(x => x.LabelShort == list[step].Substring(6));
                    p?.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Wander_Sad, forced: true);
                }
                else if (list[step] == "scripts")
                {
                    Scripts.Tick();
                }
                else if (list[step] == "pin")
                {
                    (Terminal.Open as MainTabWindow_Terminal)?.Pin();
                }
                else if (list[step].StartsWith("tab:"))
                {
                    Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed(list[step].Substring(4)));
                }
                else if (list[step] == "clickmap")
                {
                    // the middle of the screen, where the map is
                    Terminal.ClickAt(new Vector2(UI.screenWidth * 0.75f, UI.screenHeight * 0.3f));
                }
                else if (list[step].StartsWith("type:"))
                {
                    foreach (char c in list[step].Substring(5))
                    {
                        Terminal.Shell.HandleKey(new Event { type = EventType.KeyDown, character = c });
                    }
                }
                else if (list[step].StartsWith("key:"))
                {
                    KeyCode key = (KeyCode)Enum.Parse(typeof(KeyCode), list[step].Substring(4));
                    Terminal.Shell.HandleKey(new Event { type = EventType.KeyDown, keyCode = key });
                }
                else
                {
                    Terminal.Shell.Run(list[step]);
                }
            }
            else if (frame == 1 && middle != null)
            {
                input = middle;
                middle = null;
            }
            else if (frame == 2 && release != null)
            {
                input = release;
                release = null;
            }
            else if (frame == 30 && shotDir != null)
            {
                Directory.CreateDirectory(shotDir);
                ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, $"{step}-{list[step].Replace(' ', '_').Replace('/', '-')}.png"));
            }
        }
        else if (step == list.Length && frame == 0 && shotDir != null)
        {
            Root.Shutdown();
        }
    }
}

[HarmonyPatch(typeof(UnityEngine.InputSystem.InputSystem), nameof(UnityEngine.InputSystem.InputSystem.BeginFrame))]
public static class InputSystem_BeginFrame_Patch
{
    public static void Postfix()
    {
        Action a = TestHooks.input;
        TestHooks.input = null;
        a?.Invoke();
    }
}

[HarmonyPatch(typeof(Root_Entry), nameof(Root_Entry.Update))]
public static class Root_Entry_Update_Patch
{
    public static void Postfix() => TestHooks.Tick();
}

[HarmonyPatch(typeof(Root_Play), nameof(Root_Play.Update))]
public static class Root_Play_Update_Patch
{
    public static void Postfix() => TestHooks.Tick();
}
