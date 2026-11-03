using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace JustTerminal;

// research, prisoners, animals, wild animals, beds, switches, forbidding and places: the files set changes
public static partial class GameFs
{
    // words the player types for things with spaces in their names
    public static string Word(string label) => (label ?? "").Trim().ToLowerInvariant().Replace(' ', '_');

    private static bool Same(string typed, string label) => Word(typed) == Word(label);

    // research

    private static ResearchProjectDef FindResearch(string typed) =>
        DefDatabase<ResearchProjectDef>.AllDefs.FirstOrDefault(r => Same(typed, r.LabelCap.ToString()) || r.defName.Equals(typed.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string SetResearch(string value)
    {
        ResearchProjectDef r = FindResearch(value);
        if (r == null)
        {
            return "no research called " + value + ", cat /research/available lists them";
        }
        if (r.IsFinished)
        {
            return r.LabelCap + " is done already";
        }
        if (!r.CanStartNow)
        {
            string need = string.Join(", ", (r.prerequisites ?? new List<ResearchProjectDef>()).Where(p => !p.IsFinished).Select(p => Word(p.LabelCap.ToString())));
            return r.LabelCap + " can't start yet, it needs " + (need.Length > 0 ? need : "something else first");
        }
        Find.ResearchManager.SetCurrentProject(r);
        return null;
    }

    // prisoners

    private static bool Bloodfeeders(Map map) => map != null && ModsConfig.BiotechActive && map.mapPawns.FreeColonists.Any(c => c.genes?.HasActiveGene(GeneDefOf.Bloodfeeder) == true);

    // the modes the prisoner tab would show for them
    public static List<PrisonerInteractionModeDef> PrisonerModes(Pawn p) =>
        DefDatabase<PrisonerInteractionModeDef>.AllDefs.Where(m => !m.isNonExclusiveInteraction
            && (p.guest == null || p.guest.Recruitable || !m.hideIfNotRecruitable)
            && (!p.IsWildMan() || m.allowOnWildMan)
            && (!m.hideIfNoBloodfeeders || Bloodfeeders(p.MapHeld))
            && (!m.hideOnHemogenicPawns || !(ModsConfig.BiotechActive && p.genes?.HasActiveGene(GeneDefOf.Hemogenic) == true))
            && (m.allowInClassicIdeoMode || !Find.IdeoManager.classicMode)
            && (!m.hideIfNotStudiableAsPrisoner || ModsConfig.AnomalyActive && p.kindDef.studiableAsPrisoner && !p.everLostEgo)
            && (!m.hideIfGrayFleshNotAppeared || ModsConfig.AnomalyActive && Find.Anomaly.hasSeenGrayFlesh))
        .OrderBy(m => m.listOrder).ToList();

    private static void PrisonerMode(FileNode f, Pawn p)
    {
        Writable(f, "mode", () => p.guest?.ExclusiveInteractionMode != null ? Word(p.guest.ExclusiveInteractionMode.label) : "none", value =>
        {
            List<PrisonerInteractionModeDef> modes = PrisonerModes(p);
            PrisonerInteractionModeDef m = modes.FirstOrDefault(x => Same(value, x.label) || x.defName.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
            if (m == null)
            {
                return "mode is one of " + string.Join(", ", modes.Select(x => Word(x.label)));
            }
            p.guest.SetExclusiveInteraction(m);
            return null;
        });
        File(f, "modes", () => string.Join(", ", PrisonerModes(p).Select(x => Word(x.label))));
    }

    // our animals

    private static void AnimalSettings(FileNode f, Pawn p)
    {
        Writable(f, "master", () => p.playerSettings?.Master?.LabelShort ?? "none", value =>
        {
            if (p.playerSettings == null)
            {
                return p.LabelShort + " can't have a master";
            }
            if (Word(value) == "none")
            {
                p.playerSettings.Master = null;
                return null;
            }
            Pawn m = PawnsFinder.AllMaps_FreeColonistsSpawned.FirstOrDefault(c => Same(value, c.LabelShort));
            if (m == null)
            {
                return "no colonist called " + value;
            }
            if (!TrainableUtility.CanBeMaster(m, p))
            {
                return m.LabelShort + " can't handle " + p.LabelShort + ", not enough animals skill";
            }
            p.playerSettings.Master = m;
            return null;
        });
        Writable(f, "slaughter", () => Marked(p, DesignationDefOf.Slaughter), value => Mark(p, DesignationDefOf.Slaughter, value, () => p.Faction == Faction.OfPlayer ? null : "only your animals"));
        if (p.training != null)
        {
            Dir("training", f, d =>
            {
                foreach (TrainableDef td in DefDatabase<TrainableDef>.AllDefs.OrderBy(t => t.listPriority))
                {
                    TrainableDef t = td;
                    if (!p.training.CanAssignToTrain(t, out bool visible).Accepted && !visible)
                    {
                        continue;
                    }
                    Writable(d, Word(t.label), () => p.training.HasLearned(t) ? "learned" : p.training.GetWanted(t) ? "yes" : "no", value =>
                    {
                        bool? on = YesNo(value);
                        if (on == null)
                        {
                            return "training is yes or no";
                        }
                        AcceptanceReport can = p.training.CanAssignToTrain(t);
                        if (on.Value && !can.Accepted)
                        {
                            return p.LabelShort + " can't learn " + t.label + (can.Reason.NullOrEmpty() ? "" : ", " + can.Reason);
                        }
                        p.training.SetWantedRecursive(t, on.Value);
                        return null;
                    });
                }
            });
        }
    }

    private static bool? YesNo(string value) => Word(value) switch
    {
        "yes" or "on" or "true" or "1" => true,
        "no" or "off" or "false" or "0" => false,
        _ => null
    };

    private static string Marked(Thing t, DesignationDef def) => t.MapHeld?.designationManager.DesignationOn(t, def) != null ? "yes" : "no";

    // hunt, tame and slaughter are marks on the map, like the game's orders
    private static string Mark(Thing t, DesignationDef def, string value, Func<string> allowed)
    {
        bool? on = YesNo(value);
        if (on == null)
        {
            return def.defName.ToLowerInvariant() + " is yes or no";
        }
        if (t.MapHeld == null)
        {
            return t.LabelShort + " isn't on a map";
        }
        DesignationManager dm = t.MapHeld.designationManager;
        Designation there = dm.DesignationOn(t, def);
        if (!on.Value)
        {
            there?.Delete();
            return null;
        }
        if (allowed() is string bad)
        {
            return bad;
        }
        if (there == null)
        {
            // hunting and taming the same animal don't go together
            dm.RemoveAllDesignationsOn(t);
            dm.AddDesignation(new Designation(t, def));
        }
        return null;
    }

    // wild animals on the home map, closest to the colony first
    private static void Wild(FileNode d)
    {
        Map map = Home;
        if (map == null)
        {
            return;
        }
        IntVec3 center = map.Center;
        Numbered(d, map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Animal && p.Faction == null && !p.Dead).OrderBy(p => p.Position.DistanceToSquared(center)).ToList(), (f, p) =>
        {
            File(f, "name", () => p.LabelShort);
            File(f, "kind", () => p.KindLabel);
            File(f, "pos", () => Pos(p.Position));
            File(f, "manhunter", () => p.InAggroMentalState);
            Writable(f, "hunt", () => Marked(p, DesignationDefOf.Hunt), value => Mark(p, DesignationDefOf.Hunt, value, () => null));
            Writable(f, "tame", () => Marked(p, DesignationDefOf.Tame), value => Mark(p, DesignationDefOf.Tame, value, () => TameUtility.CanTame(p) ? null : p.LabelShort + " can't be tamed"));
        });
    }

    // beds

    private static void Beds(FileNode d)
    {
        Map map = Home;
        if (map == null)
        {
            return;
        }
        Numbered(d, map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().Where(b => b.def.building.bed_humanlike && !b.ForPrisoners && !b.Medical).ToList(), (f, b) =>
        {
            File(f, "name", () => b.LabelCap.ToString());
            File(f, "room", () => b.GetRoom() is Room r && r.ProperRoom && !r.PsychologicallyOutdoors ? RoomRole(r) : "outdoors");
            File(f, "pos", () => Pos(b.Position));
            File(f, "slots", () => b.SleepingSlotsCount);
            Writable(f, "owner", () => b.OwnersForReading.Count == 0 ? "none" : string.Join(", ", b.OwnersForReading.Select(o => o.LabelShort)), value =>
            {
                CompAssignableToPawn comp = b.CompAssignableToPawn;
                if (Word(value) == "none")
                {
                    foreach (Pawn o in b.OwnersForReading.ToList())
                    {
                        comp.TryUnassignPawn(o);
                    }
                    return null;
                }
                Pawn p = PawnsFinder.AllMaps_FreeColonistsSpawned.FirstOrDefault(c => Same(value, c.LabelShort));
                if (p == null)
                {
                    return "no colonist called " + value;
                }
                AcceptanceReport can = comp.CanAssignTo(p);
                if (!can.Accepted)
                {
                    return can.Reason.NullOrEmpty() ? p.LabelShort + " can't use this bed" : can.Reason;
                }
                // a full bed gives its place up, like the game's assign button
                if (b.OwnersForReading.Count >= b.SleepingSlotsCount)
                {
                    comp.TryUnassignPawn(b.OwnersForReading[0]);
                }
                comp.TryAssignPawn(p);
                return null;
            });
        });
    }

    // things with a power switch, a colonist goes and flicks it like with the game's button

    private static readonly AccessTools.FieldRef<CompFlickable, bool> wantOn = AccessTools.FieldRefAccess<CompFlickable, bool>("wantSwitchOn");

    private static void Switches(FileNode d)
    {
        Map map = Home;
        if (map == null)
        {
            return;
        }
        Numbered(d, map.listerBuildings.allBuildingsColonist.Where(b => b.GetComp<CompFlickable>() != null).OrderBy(b => b.LabelCap.ToString()).ToList(), (f, b) =>
        {
            CompFlickable flick = b.GetComp<CompFlickable>();
            File(f, "name", () => b.LabelCap.ToString());
            File(f, "room", () => b.GetRoom() is Room r && r.ProperRoom && !r.PsychologicallyOutdoors ? RoomRole(r) : "outdoors");
            Writable(f, "on", () => flick.SwitchIsOn ? (wantOn(flick) ? "yes" : "yes, to turn off") : (wantOn(flick) ? "no, to turn on" : "no"), value =>
            {
                bool? on = YesNo(value);
                if (on == null)
                {
                    return "on is yes or no";
                }
                wantOn(flick) = on.Value;
                FlickUtility.UpdateFlickDesignation(b);
                return null;
            });
        });
    }

    // what's lying in a stockpile or on shelves, forbidden or not
    private static string SetForbidden(SlotGroup g, string value)
    {
        bool? on = YesNo(value);
        if (on == null)
        {
            return "forbid is yes or no";
        }
        foreach (Thing t in g.HeldThings.ToList())
        {
            t.SetForbidden(on.Value, false);
        }
        return null;
    }

    private static string Forbidden(SlotGroup g)
    {
        List<Thing> held = g.HeldThings.ToList();
        int n = held.Count(t => t.IsForbidden(Faction.OfPlayer));
        return held.Count == 0 ? "empty" : n == 0 ? "no" : n == held.Count ? "yes" : $"{n} of {held.Count}";
    }

    // positions are x,z like the game's cells, x goes right and z goes up
    public static string Pos(IntVec3 c) => c.x + "," + c.z;

    public static bool ParsePos(string text, out IntVec3 cell)
    {
        cell = IntVec3.Invalid;
        string[] parts = text.Trim().Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out int x) || !int.TryParse(parts[1].Trim(), out int z))
        {
            return false;
        }
        cell = new IntVec3(x, 0, z);
        return true;
    }

    // the places saved with place save, in the save
    private static void Places(FileNode d)
    {
        foreach (SavedPlace p in Recorder.Current?.Places ?? new List<SavedPlace>())
        {
            SavedPlace place = p;
            File(d, place.name, () => Pos(place.cell));
        }
    }
}

public class SavedPlace : IExposable
{
    public string name;
    public IntVec3 cell;

    public void ExposeData()
    {
        Scribe_Values.Look(ref name, "name");
        Scribe_Values.Look(ref cell, "cell");
    }
}
