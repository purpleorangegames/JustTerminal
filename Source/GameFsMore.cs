using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace JustTerminal;

// time, rooms, trade, caravans, growing, bills, storage, the DLCs, and more in each colonist's folder
public static partial class GameFs
{
    private static string F(float v, string format = "0") => v.ToString(format, CultureInfo.InvariantCulture);

    private static string Word(string label) => (label ?? "").Replace(' ', '_').ToLowerInvariant();

    private static bool YesNo(string value, out bool on)
    {
        string v = value.Trim().ToLowerInvariant();
        on = v is "yes" or "true" or "1" or "on";
        return on || v is "no" or "false" or "0" or "off";
    }

    // more folders under /, called from Top
    private static void More(FileNode top)
    {
        Dir("time", top, Time);
        Dir("rooms", top, Rooms);
        Dir("trade", top, Trade);
        Dir("caravans", top, Caravans);
        Dir("growing", top, Growing);
        Dir("bills", top, Bills);
        Dir("storage", top, Storage);
        if (ModsConfig.IdeologyActive)
        {
            Dir("ideology", top, Ideology);
        }
        if (ModsConfig.BiotechActive)
        {
            Dir("mechanitors", top, Mechanitors);
        }
        if (ModsConfig.AnomalyActive)
        {
            Dir("anomaly", top, Anomaly);
        }
    }

    // time

    private static readonly (string word, TimeSpeed speed)[] speeds =
    {
        ("paused", TimeSpeed.Paused), ("normal", TimeSpeed.Normal), ("fast", TimeSpeed.Fast), ("superfast", TimeSpeed.Superfast), ("ultrafast", TimeSpeed.Ultrafast)
    };

    private static void Time(FileNode d)
    {
        Writable(d, "speed", () => speeds.FirstOrDefault(s => s.speed == Find.TickManager.CurTimeSpeed).word ?? "normal", value =>
        {
            (string word, TimeSpeed speed) found = speeds.FirstOrDefault(s => s.word == value.Trim().ToLowerInvariant());
            if (found.word == null || found.speed == TimeSpeed.Ultrafast && !Prefs.DevMode)
            {
                return "speed is paused, normal, fast or superfast";
            }
            Find.TickManager.CurTimeSpeed = found.speed;
            return null;
        });
        File(d, "paused", () => Find.TickManager.Paused);
        File(d, "hour", () => GenLocalDate.HourOfDay(Home));
        File(d, "day", () => GenDate.DaysPassed + 1);
        File(d, "ticks", () => Find.TickManager.TicksGame.ToString());
    }

    // in each colonist's folder: drafted, schedule, thoughts, social, records, and the DLC ones

    private static readonly (char letter, Func<TimeAssignmentDef> def)[] hours =
    {
        ('s', () => TimeAssignmentDefOf.Sleep), ('w', () => TimeAssignmentDefOf.Work), ('j', () => TimeAssignmentDefOf.Joy), ('a', () => TimeAssignmentDefOf.Anything), ('m', () => TimeAssignmentDefOf.Meditate)
    };

    private static void PawnMore(FileNode f, Pawn p)
    {
        if (p.drafter != null)
        {
            Writable(f, "drafted", () => p.Drafted ? "yes" : "no", value =>
            {
                if (!YesNo(value, out bool on))
                {
                    return "drafted is yes or no";
                }
                if (on && (!p.Spawned || p.Downed || p.InMentalState))
                {
                    return p.LabelShort + " can't be drafted now";
                }
                p.drafter.Drafted = on;
                return null;
            });
        }
        if (p.timetable != null)
        {
            // one letter an hour from midnight: s sleep, w work, j joy, a anything, m meditate
            Writable(f, "schedule", () => new string(Enumerable.Range(0, 24).Select(h => hours.FirstOrDefault(x => x.def() == p.timetable.GetAssignment(h)).letter is char c && c != '\0' ? c : 'a').ToArray()), value =>
            {
                string v = value.Trim().ToLowerInvariant().Replace(" ", "");
                if (v.Length != 24 || v.Any(c => hours.All(x => x.letter != c || x.def() == null)))
                {
                    return "schedule is 24 letters, one an hour from midnight: s sleep, w work, j joy, a anything" + (TimeAssignmentDefOf.Meditate != null ? ", m meditate" : "");
                }
                for (int h = 0; h < 24; h++)
                {
                    p.timetable.SetAssignment(h, hours.First(x => x.letter == v[h]).def());
                }
                return null;
            });
            File(f, "now", () => p.timetable.CurrentAssignment?.label ?? "none");
        }
        if (p.needs?.mood != null)
        {
            Dir("thoughts", f, d =>
            {
                List<Thought> groups = new List<Thought>();
                p.needs.mood.thoughts.GetDistinctMoodThoughtGroups(groups);
                Numbered(d, groups.Select(g => (g, p.needs.mood.thoughts.MoodOffsetOfGroup(g))).OrderBy(x => x.Item2), (k, t) =>
                {
                    File(k, "name", () => t.g.LabelCap.ToString().StripTags());
                    File(k, "mood", () => t.Item2, "0.#");
                });
            });
        }
        if (p.relations != null)
        {
            Dir("social", f, d => Numbered(d, Colonists().Where(o => o != p).OrderByDescending(o => p.relations.OpinionOf(o)), (k, o) =>
            {
                File(k, "name", () => o.LabelShort);
                File(k, "opinion", () => p.relations.OpinionOf(o));
                File(k, "relation", () => p.GetMostImportantRelation(o)?.GetGenderSpecificLabel(o) ?? "none");
            }));
        }
        if (p.records != null)
        {
            Dir("records", f, d =>
            {
                foreach (RecordDef r in DefDatabase<RecordDef>.AllDefs.OrderBy(r => r.defName))
                {
                    RecordDef rec = r;
                    // time records are in ticks, days read better
                    File(d, rec.defName.ToLowerInvariant(), () => rec.type == RecordType.Time ? F(p.records.GetValue(rec) / GenDate.TicksPerDay, "0.#") : F(p.records.GetValue(rec)));
                }
            });
        }
        if (ModsConfig.IdeologyActive && p.ideo?.Ideo != null)
        {
            File(f, "certainty", () => p.ideo.Certainty * 100f);
            File(f, "ideoligion", () => p.ideo.Ideo.name);
            File(f, "role", () => p.ideo.Ideo.GetRole(p)?.LabelForPawn(p) ?? "none");
        }
        if (ModsConfig.RoyaltyActive && p.royalty != null)
        {
            File(f, "royaltitle", () => p.royalty.MostSeniorTitle?.Label ?? "none");
            File(f, "psylink", () => p.GetPsylinkLevel());
        }
    }

    // rooms on the home map, bedrooms say whose

    // the game's label already says whose bedroom it is
    private static string RoomName(Room r) => r.GetRoomRoleLabel().CapitalizeFirst();

    // the kind of room alone, like bedroom or kitchen
    private static string RoomRole(Room r) => Word(r.Role?.label ?? r.GetRoomRoleLabel());

    private static List<Room> HomeRooms() =>
        Home == null ? new List<Room>() : Home.regionGrid.AllRooms.Where(r => r.ProperRoom && !r.PsychologicallyOutdoors && !r.IsDoorway).OrderBy(r => r.GetRoomRoleLabel()).ThenBy(r => r.ID).ToList();

    private static void Rooms(FileNode d)
    {
        Numbered(d, HomeRooms(), (f, r) =>
        {
            File(f, "name", () => RoomName(r));
            File(f, "role", () => RoomRole(r));
            File(f, "owners", () => Names(r.Owners));
            File(f, "temp", () => r.Temperature, "0.#");
            File(f, "cells", () => r.CellCount);
            File(f, "cleanliness", () => r.GetStat(RoomStatDefOf.Cleanliness), "0.##");
            File(f, "impressiveness", () => r.GetStat(RoomStatDefOf.Impressiveness), "0.#");
            File(f, "beauty", () => r.GetStat(RoomStatDefOf.Beauty), "0.##");
            File(f, "space", () => r.GetStat(RoomStatDefOf.Space), "0.#");
        });
    }

    // traders on our maps and what they carry

    private static void Trade(FileNode d)
    {
        Numbered(d, MapCommands.OnMaps(p => p.trader?.traderKind != null && !p.Dead && !p.HostileTo(Faction.OfPlayer)).OrderBy(p => p.LabelShort), (f, p) =>
        {
            File(f, "name", () => p.LabelShort);
            File(f, "kind", () => p.trader.traderKind.label);
            File(f, "faction", () => p.Faction?.Name ?? "none");
            File(f, "silver", () => p.trader.Goods.Where(t => t.def == ThingDefOf.Silver).Sum(t => t.stackCount));
            Dir("goods", f, g => Numbered(g, p.trader.Goods.Where(t => t.def != ThingDefOf.Silver).OrderBy(t => t.LabelNoCount), (k, t) =>
            {
                File(k, "name", () => t.LabelCapNoCount.StripTags());
                File(k, "count", () => t.stackCount);
                // what one is worth, the trader asks a bit more and pays a bit less
                File(k, "value", () => t.MarketValue, "0.#");
            }));
        });
    }

    // caravans on the world map

    private static void Caravans(FileNode d)
    {
        Numbered(d, Find.WorldObjects.Caravans.Where(c => c.IsPlayerControlled), (f, c) =>
        {
            File(f, "name", () => c.Label);
            File(f, "pawns", () => c.PawnsListForReading.Count);
            File(f, "colonists", () => Names(c.PawnsListForReading.Where(p => p.IsColonist)));
            File(f, "moving", () => c.pather.Moving);
            File(f, "destination", () =>
            {
                if (!c.pather.Moving)
                {
                    return "none";
                }
                Settlement s = Find.WorldObjects.SettlementAt(c.pather.Destination);
                return s != null ? s.Label : "tile " + c.pather.Destination;
            });
            File(f, "arrives", () => c.pather.Moving ? Period(CaravanArrivalTimeEstimator.EstimatedTicksToArrive(c, true)) : "none");
            File(f, "food", () => c.DaysWorthOfFood.days >= 600f ? "plenty" : F(c.DaysWorthOfFood.days, "0.#"));
        });
    }

    // growing zones, sow can be turned off and on

    private static void Growing(FileNode d)
    {
        if (Home == null)
        {
            return;
        }
        Numbered(d, Home.zoneManager.AllZones.OfType<Zone_Growing>().OrderBy(z => z.label), (f, z) =>
        {
            File(f, "name", () => z.label);
            File(f, "crop", () => z.GetPlantDefToGrow()?.label ?? "none");
            File(f, "cells", () => z.Cells.Count);
            File(f, "grown", () =>
            {
                List<Plant> plants = z.Cells.Select(c => c.GetPlant(Home)).Where(pl => pl != null && pl.def == z.GetPlantDefToGrow()).ToList();
                return plants.Count == 0 ? 0f : plants.Average(pl => pl.Growth) * 100f;
            });
            File(f, "plants", () => z.Cells.Count(c => c.GetPlant(Home)?.def == z.GetPlantDefToGrow()));
            Writable(f, "sow", () => z.allowSow ? "yes" : "no", value =>
            {
                if (!YesNo(value, out bool on))
                {
                    return "sow is yes or no";
                }
                z.allowSow = on;
                return null;
            });
        });
    }

    // workbenches with their bills, count and suspended can change

    private static void Bills(FileNode d)
    {
        if (Home == null)
        {
            return;
        }
        Numbered(d, Home.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Where(b => b.BillStack.Count > 0).OrderBy(b => b.LabelCap.ToString()), (f, b) =>
        {
            File(f, "name", () => b.LabelCap);
            Dir("list", f, l => Numbered(l, b.BillStack.Bills.ToList(), (k, bill) =>
            {
                File(k, "name", () => bill.LabelCap.ToString());
                Writable(k, "suspended", () => bill.suspended ? "yes" : "no", value =>
                {
                    if (!YesNo(value, out bool on))
                    {
                        return "suspended is yes or no";
                    }
                    bill.suspended = on;
                    return null;
                });
                if (bill is Bill_Production prod)
                {
                    File(k, "mode", () => prod.repeatMode == BillRepeatModeDefOf.Forever ? "forever" : prod.repeatMode == BillRepeatModeDefOf.TargetCount ? "until" : "count");
                    Writable(k, "count", () => (prod.repeatMode == BillRepeatModeDefOf.TargetCount ? prod.targetCount : prod.repeatCount).ToString(), value =>
                    {
                        if (!int.TryParse(value.Trim(), out int n) || n < 0 || n > 99999)
                        {
                            return "count is a number from 0";
                        }
                        if (prod.repeatMode == BillRepeatModeDefOf.TargetCount)
                        {
                            prod.targetCount = n;
                        }
                        else
                        {
                            prod.repeatCount = n;
                        }
                        return null;
                    });
                    File(k, "have", () => prod.repeatMode == BillRepeatModeDefOf.TargetCount ? prod.recipe.WorkerCounter.CountProducts(prod).ToString() : "none");
                }
            }));
        });
    }

    // stockpiles and shelves, the priority and what they take can change

    private static readonly string[] priorities = { "low", "normal", "preferred", "important", "critical" };

    private static string StorageRoom(SlotGroup g, Map map)
    {
        IntVec3 cell = g.CellsList.FirstOrDefault();
        Room room = cell.IsValid ? cell.GetRoom(map) : null;
        return room != null && room.ProperRoom && !room.PsychologicallyOutdoors ? RoomRole(room) : "outdoors";
    }

    private static void Storage(FileNode d)
    {
        Map map = Home;
        if (map == null)
        {
            return;
        }
        Numbered(d, map.haulDestinationManager.AllGroupsListForReading.ToList(), (f, g) =>
        {
            StorageSettings settings = g.Settings;
            File(f, "name", () => g.parent is Zone z ? z.label : g.parent is Thing t ? t.LabelCap.ToString() : g.parent.SlotYielderLabel());
            File(f, "kind", () => g.parent is Zone ? "zone" : "building");
            File(f, "room", () => StorageRoom(g, map));
            File(f, "cells", () => g.CellsList.Count);
            Writable(f, "priority", () => settings.Priority.ToString().ToLowerInvariant(), value =>
            {
                int i = Array.IndexOf(priorities, value.Trim().ToLowerInvariant());
                if (i < 0)
                {
                    return "priority is " + string.Join(", ", priorities);
                }
                settings.Priority = (StoragePriority)(i + 1);
                return null;
            });
            Writable(f, "filter", () => FilterText(settings.filter), value => SetFilter(g.parent as IStoreSettingsParent, settings, value));
        });
    }

    // a filter as lines: the hit points and quality ranges, then each thing allowed
    public static string FilterText(ThingFilter filter)
    {
        List<string> lines = new List<string>
        {
            $"hitpoints {F(filter.AllowedHitPointsPercents.min * 100f)}-{F(filter.AllowedHitPointsPercents.max * 100f)}",
            $"quality {filter.AllowedQualityLevels.min.ToString().ToLowerInvariant()}-{filter.AllowedQualityLevels.max.ToString().ToLowerInvariant()}"
        };
        lines.AddRange(filter.AllowedThingDefs.Select(t => t.defName).OrderBy(n => n));
        return string.Join("\n", lines);
    }

    private static string SetFilter(IStoreSettingsParent owner, StorageSettings settings, string text)
    {
        ThingFilter parent = owner?.GetParentStoreSettings()?.filter;
        ThingFilter filter = settings.filter;
        List<ThingDef> allow = new List<ThingDef>();
        FloatRange hp = filter.AllowedHitPointsPercents;
        QualityRange quality = filter.AllowedQualityLevels;
        foreach (string raw in text.Replace("\r", "").Split('\n', ','))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#"))
            {
                continue;
            }
            string[] words = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (words[0] == "hitpoints" && words.Length > 1)
            {
                string[] r = words[1].Split('-');
                if (r.Length != 2 || !float.TryParse(r[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float a) || !float.TryParse(r[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
                {
                    return "hitpoints is like hitpoints 50-100";
                }
                hp = new FloatRange(Mathf.Clamp01(a / 100f), Mathf.Clamp01(b / 100f));
                continue;
            }
            if (words[0] == "quality" && words.Length > 1)
            {
                string[] r = words[1].Split('-');
                if (r.Length != 2 || !Enum.TryParse(r[0], true, out QualityCategory a) || !Enum.TryParse(r[1], true, out QualityCategory b))
                {
                    return "quality is like quality normal-legendary";
                }
                quality = new QualityRange(a, b);
                continue;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(line);
            // things this storage can't hold are left out, like a shelf with meals
            if (def != null && (parent == null || parent.Allows(def)))
            {
                allow.Add(def);
            }
        }
        filter.SetDisallowAll();
        foreach (ThingDef def in allow)
        {
            filter.SetAllow(def, true);
        }
        filter.AllowedHitPointsPercents = hp;
        filter.AllowedQualityLevels = quality;
        return null;
    }

    // the DLCs

    private static void Ideology(FileNode d)
    {
        Ideo ideo = Faction.OfPlayer.ideos?.PrimaryIdeo;
        if (ideo == null)
        {
            return;
        }
        File(d, "name", () => ideo.name);
        File(d, "memes", () => string.Join(", ", ideo.memes.Select(m => m.LabelCap.ToString())));
        Dir("roles", d, r => Numbered(r, ideo.RolesListForReading, (f, role) =>
        {
            File(f, "name", () => role.LabelCap.ToString());
            File(f, "holder", () => Names(role.ChosenPawns()));
        }));
    }

    private static void Mechanitors(FileNode d)
    {
        Numbered(d, Colonists().Where(p => p.mechanitor != null), (f, p) =>
        {
            File(f, "name", () => p.LabelShort);
            File(f, "bandwidth", () => p.mechanitor.TotalBandwidth);
            File(f, "used", () => p.mechanitor.UsedBandwidth);
            File(f, "mechs", () => p.mechanitor.OverseenPawns.Count());
            File(f, "groups", () => p.mechanitor.controlGroups.Count);
        });
    }

    private static void Anomaly(FileNode d)
    {
        File(d, "level", () => Find.Anomaly.Level);
        File(d, "entities", () => Find.Maps.SelectMany(m => m.mapPawns.AllPawns).Count(p => p.IsOnHoldingPlatform));
    }
}
