using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace JustTerminal;

// spots on the map: pick one with a click, save it with a name, send colonists there
[StaticConstructorOnStartup]
public static class Places
{
    private static readonly Texture2D cursor = ContentFinder<Texture2D>.Get("UI/JustTerminal/Terminal", false);

    // the terminal hides and the game pauses, a click on the map gives the cell, then the terminal comes back
    public static void Pick(Shell sh, Action<IntVec3> picked)
    {
        Map map = Find.CurrentMap;
        if (map == null)
        {
            sh.Print("pick needs a map on screen", Ink.Bad);
            return;
        }
        TimeSpeed was = Find.TickManager.CurTimeSpeed;
        Find.TickManager.Pause();
        Window open = Terminal.Open;
        bool pinned = open is Window_TerminalPinned;
        Rect rect = open?.windowRect ?? Terminal.DefaultRect;
        if (pinned)
        {
            Find.WindowStack.TryRemove(open, false);
        }
        else if (open != null)
        {
            Find.MainTabsRoot.EscapeCurrentTab(false);
        }
        Messages.Message("JTM_PickHint".Translate(), MessageTypeDefOf.SilentInput, false);
        bool done = false;
        Find.Targeter.BeginTargeting(new TargetingParameters { canTargetLocations = true, canTargetPawns = true, canTargetBuildings = true, canTargetItems = true },
            target =>
            {
                done = true;
                Back(pinned, rect, was);
                picked(target.Cell);
            },
            target => GenDraw.DrawTargetHighlight(target),
            target => target.Cell.InBounds(map),
            actionWhenFinished: () =>
            {
                if (!done)
                {
                    Back(pinned, rect, was);
                    sh.Print("nothing picked", Ink.Dim);
                }
            },
            mouseAttachment: cursor,
            onGuiAction: target =>
            {
                // the cell under the mouse, next to the cursor
                IntVec3 c = UI.MouseCell();
                if (c.InBounds(map))
                {
                    Widgets.MouseAttachedLabel(GameFs.Pos(c), 8f, -18f);
                }
            });
    }

    private static void Back(bool pinned, Rect rect, TimeSpeed was)
    {
        Find.TickManager.CurTimeSpeed = was;
        if (pinned)
        {
            Find.WindowStack.Add(new Window_TerminalPinned(rect));
        }
        else
        {
            Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed("JTM_Terminal"), false);
        }
        if (Terminal.Open is Window w)
        {
            Terminal.Give(w);
        }
        Terminal.Shell.Redraw = true;
    }

    // what's at a cell, for pick and place
    private static string What(IntVec3 c, Map map)
    {
        Thing t = c.GetFirstPawn(map) as Thing ?? c.GetFirstBuilding(map) ?? c.GetFirstItem(map);
        return t != null ? t.LabelShort : c.GetTerrain(map)?.label ?? "";
    }

    // pick: the cell clicked on the map
    public static void PickCmd(Shell sh, string[] args)
    {
        if (!sh.CanAsk)
        {
            sh.Print("pick needs the player to click", Ink.Bad);
            return;
        }
        Pick(sh, c => sh.Print(GameFs.Pos(c) + "  " + What(c, Find.CurrentMap), Ink.Bright));
    }

    private static List<SavedPlace> Saved => Recorder.Current?.Places;

    public static SavedPlace Named(string name) => Saved?.FirstOrDefault(p => p.name.Equals(name, StringComparison.OrdinalIgnoreCase));

    // a place name, x,z or a colonist's name, the cell it means
    public static bool Where(string word, out IntVec3 cell, out string error)
    {
        error = null;
        if (GameFs.ParsePos(word, out cell))
        {
            return true;
        }
        if (Named(word) is SavedPlace p)
        {
            cell = p.cell;
            return true;
        }
        Pawn who = PawnsFinder.AllMaps_FreeColonistsSpawned.FirstOrDefault(c => GameFs.Word(c.LabelShort) == GameFs.Word(word));
        if (who != null)
        {
            cell = who.Position;
            return true;
        }
        error = "no place called " + word + ", place lists them, or use x,z";
        return false;
    }

    // place, place save <name> [x,z|who|pick], place rm <name>
    public static void PlaceCmd(Shell sh, string[] args)
    {
        if (Saved == null)
        {
            sh.Print("places need a game running", Ink.Bad);
            return;
        }
        if (args.Length == 0)
        {
            if (Saved.Count == 0)
            {
                sh.Print("no places, place save <name> saves one", Ink.Dim);
            }
            foreach (SavedPlace p in Saved)
            {
                sh.Out(p.name.PadRight(16), Ink.Bright);
                sh.Out(GameFs.Pos(p.cell).PadRight(10));
                sh.Print(Find.CurrentMap != null && p.cell.InBounds(Find.CurrentMap) ? What(p.cell, Find.CurrentMap) : "", Ink.Dim);
            }
            return;
        }
        string what = args[0].ToLowerInvariant();
        if (what == "rm" && args.Length == 2)
        {
            sh.Print(Saved.RemoveAll(p => p.name.Equals(args[1], StringComparison.OrdinalIgnoreCase)) > 0 ? args[1] + " removed" : "no place called " + args[1], Ink.Dim);
            return;
        }
        if (what != "save" || args.Length < 2)
        {
            sh.Print("usage: place, place save <name> [x,z|who|pick], place rm <name>", Ink.Dim);
            return;
        }
        string name = args[1];
        if (name.Contains(',') || name.Contains('/') || name.Length > 24)
        {
            sh.Print("a place name is a word, 24 letters at most", Ink.Bad);
            return;
        }
        void Save(IntVec3 c)
        {
            Saved.RemoveAll(p => p.name.Equals(name, StringComparison.OrdinalIgnoreCase));
            Saved.Add(new SavedPlace { name = name, cell = c });
            sh.Print(name + " is " + GameFs.Pos(c), Ink.Good);
        }
        if (args.Length < 3 || args[2].Equals("pick", StringComparison.OrdinalIgnoreCase))
        {
            if (!sh.CanAsk)
            {
                sh.Print("pick needs the player to click, give x,z or a colonist instead", Ink.Bad);
                return;
            }
            Pick(sh, Save);
            return;
        }
        if (!Where(args[2], out IntVec3 cell, out string error))
        {
            sh.Print(error, Ink.Bad);
            return;
        }
        Save(cell);
    }

    // goto <who> <place|x,z|who|pick>: drafts them and sends them there, side by side
    public static void GotoCmd(Shell sh, string[] args)
    {
        if (args.Length < 2)
        {
            sh.Print("usage: goto <who> <place|x,z|colonist|pick>", Ink.Dim);
            return;
        }
        List<Pawn> pawns = EditCommands.Who(args[0], out string bad);
        if (bad != null)
        {
            sh.Print(bad, Ink.Bad);
            return;
        }
        if (args[1].Equals("pick", StringComparison.OrdinalIgnoreCase))
        {
            if (!sh.CanAsk)
            {
                sh.Print("pick needs the player to click, give a place or x,z instead", Ink.Bad);
                return;
            }
            Pick(sh, c => Send(sh, pawns, c));
            return;
        }
        if (!Where(args[1], out IntVec3 cell, out string error))
        {
            sh.Print(error, Ink.Bad);
            return;
        }
        Send(sh, pawns, cell);
    }

    private static void Send(Shell sh, List<Pawn> pawns, IntVec3 cell)
    {
        // spots others are already walking to count as taken, so goto one by one spreads out too
        HashSet<IntVec3> taken = new HashSet<IntVec3>();
        foreach (Pawn other in PawnsFinder.AllMaps_FreeColonistsSpawned.Where(o => !pawns.Contains(o)))
        {
            if (other.CurJobDef == JobDefOf.Goto && other.CurJob.targetA.IsValid)
            {
                taken.Add(other.CurJob.targetA.Cell);
            }
            if (other.Drafted)
            {
                taken.Add(other.Position);
            }
        }
        foreach (Pawn p in pawns)
        {
            if (!p.Spawned || p.Map == null || !cell.InBounds(p.Map))
            {
                sh.Print(p.LabelShort + " isn't on that map", Ink.Bad);
                continue;
            }
            if (p.Downed || p.InMentalState || p.drafter == null)
            {
                sh.Print(p.LabelShort + " can't be drafted now", Ink.Bad);
                continue;
            }
            // the closest free cell they can stand on and reach, so a group spreads out around the spot
            IntVec3 spot = IntVec3.Invalid;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(cell, 6f, true))
            {
                if (c.InBounds(p.Map) && c.Standable(p.Map) && !taken.Contains(c) && p.CanReach(c, PathEndMode.OnCell, Danger.Deadly))
                {
                    spot = c;
                    break;
                }
            }
            if (!spot.IsValid)
            {
                sh.Print(p.LabelShort + " can't get to " + GameFs.Pos(cell), Ink.Bad);
                continue;
            }
            taken.Add(spot);
            p.drafter.Drafted = true;
            p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, spot));
            sh.Print(p.LabelShort + " goes to " + GameFs.Pos(spot), Ink.Good);
        }
    }
}
