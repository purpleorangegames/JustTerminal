using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace JustTerminal;

// the folders under / are the game itself, read only, each file is one value read when it's opened
public static class GameFs
{
    // built again on each use, cheap since folders fill only when something looks inside and values are read when asked
    public static FileNode Top()
    {
        FileNode top = Dir("/", null, null);
        Dir("bin", top, Programs.Bin);
        Dir("manual", top, Programs.ManualDir);
        Dir("examples", top, Programs.ExamplesDir);
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null)
        {
            return top;
        }
        Dir("colony", top, Colony);
        Dir("pawns", top, Pawns);
        Dir("health", top, Health);
        Dir("danger", top, Danger);
        Dir("visitors", top, Visitors);
        Dir("factions", top, Factions);
        Dir("groups", top, Groups).make = MakeGroup;
        Dir("alerts", top, Alerts);
        Dir("letters", top, Letters);
        Dir("research", top, Research);
        Dir("prisoners", top, d => Numbered(d, PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_PrisonersOfColony.OrderBy(p => p.LabelShort), PrisonerFiles));
        Dir("animals", top, d => Numbered(d, MapCommands.Ours().Where(p => p.RaceProps.Animal && p.Faction == Faction.OfPlayer), AnimalFiles));
        Dir("mechs", top, d => Numbered(d, MapCommands.Ours().Where(p => p.RaceProps.IsMechanoid && p.Faction == Faction.OfPlayer), MechFiles));
        Dir("quests", top, Quests);
        Dir("world", top, w => Dir("towns", w, Towns));
        Dir("work", top, d =>
        {
            foreach (WorkTypeDef w in EditCommands.WorkTypes)
            {
                WorkTypeDef work = w;
                File(d, EditCommands.WorkWord(work), () => work.gerundLabel);
            }
        }).alias = WorkAlias;
        Dir("care", top, d =>
        {
            foreach ((string word, MedicalCareCategory care) in EditCommands.careNames)
            {
                MedicalCareCategory c = care;
                File(d, word, () => c.GetLabel());
            }
        });
        Dir("areas", top, d =>
        {
            foreach (Area a in EditCommands.Areas(Home))
            {
                Area area = a;
                File(d, EditCommands.AreaWord(area), () => area.TrueCount + " cells");
            }
        });
        Dir("policies", top, d =>
        {
            foreach (string kind in EditCommands.kinds)
            {
                string k = kind;
                Dir(k, d, pd =>
                {
                    foreach (Policy policy in EditCommands.Policies(k))
                    {
                        Policy pol = policy;
                        File(pd, EditCommands.PolicyWord(pol), () => pol.label);
                    }
                });
            }
        });
        Dir("idle", top, Idle);
        Dir("power", top, Power);
        Dir("stock", top, Stock);
        return top;
    }

    public static FileNode Dir(string name, FileNode parent, Action<FileNode> fill)
    {
        FileNode d = new FileNode { name = name, folder = true, game = true, parent = parent };
        if (fill != null)
        {
            d.fill = n =>
            {
                fill(n);
                return n.children;
            };
        }
        parent?.children.Add(d);
        return d;
    }

    public static void File(FileNode dir, string name, Func<string> read) =>
        dir.children.Add(new FileNode { name = name, game = true, parent = dir, read = read });

    // a file set can change, write gives null when it took the value or the reason it didn't
    private static void Writable(FileNode dir, string name, Func<string> read, Func<string, string> write) =>
        dir.children.Add(new FileNode { name = name, game = true, parent = dir, read = read, write = write });

    private static string WorkAlias(string word) => EditCommands.FindWork(word) is WorkTypeDef w ? EditCommands.WorkWord(w) : null;

    private static void File(FileNode dir, string name, Func<float> read, string format = "0") =>
        File(dir, name, () => read().ToString(format, CultureInfo.InvariantCulture));

    private static void File(FileNode dir, string name, Func<bool> read) =>
        File(dir, name, () => read() ? "yes" : "no");

    private static Map Home => Commands.HomeMap;

    private static Vector2 LongLat => Home != null ? Find.WorldGrid.LongLatOf(Home.Tile) : Vector2.zero;

    private static string Percent(float? v) => v == null ? "none" : (v.Value * 100f).ToString("0", CultureInfo.InvariantCulture);

    private static List<Pawn> Colonists() => PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep;

    private static string Names(IEnumerable<Pawn> pawns)
    {
        string names = string.Join(", ", pawns.Select(p => p.LabelShort));
        return names.Length == 0 ? "none" : names;
    }

    private static void Colony(FileNode d)
    {
        File(d, "name", () => Faction.OfPlayer.Name);
        File(d, "storyteller", () => Find.Storyteller.def.LabelCap);
        File(d, "difficulty", () => Find.Storyteller.difficultyDef.LabelCap);
        File(d, "biome", () => Home?.Biome.LabelCap ?? "none");
        File(d, "maps", () => Find.Maps.Count);
        File(d, "colonists", () => Colonists().Count);
        File(d, "prisoners", () => Home?.mapPawns.PrisonersOfColonyCount ?? 0);
        File(d, "slaves", () => Home?.mapPawns.SlavesOfColonySpawned.Count ?? 0);
        File(d, "animals", () => Home?.mapPawns.SpawnedColonyAnimals.Count ?? 0);
        File(d, "wealth", () => Home?.wealthWatcher.WealthTotal ?? 0f);
        File(d, "mood", () =>
        {
            List<Pawn> moods = Colonists().Where(p => p.needs?.mood != null).ToList();
            return moods.Count == 0 ? 0f : moods.Average(p => p.needs.mood.CurLevelPercentage) * 100f;
        });
        File(d, "day", () => GenDate.DaysPassed + 1);
        File(d, "date", () => GenDate.DateFullStringAt(Find.TickManager.TicksAbs, LongLat));
        File(d, "happiest", () => Colonists().OrderByDescending(p => p.needs?.mood?.CurLevelPercentage ?? 0f).FirstOrDefault()?.LabelShort ?? "none");
        File(d, "hour", () => GenLocalDate.HourOfDay(Home));
        File(d, "season", () => GenLocalDate.Season(Home).Label());
        File(d, "temp", () => Home?.mapTemperature.OutdoorTemp ?? 0f, "0.#");
        File(d, "weather", () => Home?.weatherManager.curWeather.label ?? "none");
        File(d, "manualwork", () => Find.PlaySettings.useWorkPriorities);
    }

    private static void Pawns(FileNode d)
    {
        HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Pawn p in Colonists().OrderBy(p => p.LabelShort))
        {
            string name = p.LabelShort.Replace(' ', '_');
            for (int i = 2; used.Contains(name); i++)
            {
                name = p.LabelShort.Replace(' ', '_') + "_" + i;
            }
            used.Add(name);
            Pawn pawn = p;
            Dir(name, d, f => PawnFiles(f, pawn)).tag = pawn;
        }
    }

    private static void PawnFiles(FileNode f, Pawn pawn)
    {
        File(f, "name", () => pawn.LabelShort);
        File(f, "fullname", () => pawn.Name?.ToStringFull ?? pawn.LabelCap);
        File(f, "gender", () => pawn.gender.GetLabel());
        File(f, "age", () => pawn.ageTracker.AgeBiologicalYears);
        File(f, "title", () => pawn.story?.TitleCap ?? pawn.KindLabel);
        // needs a pawn doesn't have read none
        File(f, "mood", () => Percent(pawn.needs?.mood?.CurLevelPercentage));
        File(f, "health", () => pawn.health.summaryHealth.SummaryHealthPercent * 100f);
        File(f, "pain", () => pawn.health.hediffSet.PainTotal * 100f);
        File(f, "food", () => Percent(pawn.needs?.food?.CurLevelPercentage));
        File(f, "rest", () => Percent(pawn.needs?.rest?.CurLevelPercentage));
        File(f, "joy", () => Percent(pawn.needs?.joy?.CurLevelPercentage));
        File(f, "bleeding", () => pawn.health.hediffSet.BleedRateTotal > 0f);
        File(f, "untended", () => pawn.health.HasHediffsNeedingTend());
        File(f, "sick", () => pawn.health.hediffSet.hediffs.Any(h => h.def.makesSickThought));
        File(f, "downed", () => pawn.Downed);
        File(f, "drafted", () => pawn.Drafted);
        File(f, "breaking", () => Breaking(pawn));
        File(f, "mental", () => pawn.InMentalState ? pawn.MentalStateDef.label : "none");
        File(f, "idle", () => IsIdle(pawn));
        File(f, "job", () => pawn.jobs?.curDriver?.GetReport() is string r && r.Length > 0 ? r : "idle");
        File(f, "doing", () => Commands.Doing(pawn));
        File(f, "where", () => Commands.Where(pawn));
        File(f, "traits", () => pawn.story?.traits == null || pawn.story.traits.allTraits.Count == 0 ? "none" : string.Join(", ", pawn.story.traits.allTraits.Select(t => t.LabelCap)));
        PawnSettings(f, pawn);
        if (pawn.skills != null)
        {
            Dir("skills", f, d =>
            {
                foreach (SkillRecord sk in pawn.skills.skills)
                {
                    SkillRecord skill = sk;
                    Dir(skill.def.defName.ToLowerInvariant(), d, k =>
                    {
                        File(k, "name", () => skill.def.skillLabel.CapitalizeFirst());
                        File(k, "level", () => skill.Level);
                        File(k, "passion", () => skill.passion == Passion.Major ? "major" : skill.passion == Passion.Minor ? "minor" : "none");
                        File(k, "disabled", () => skill.TotallyDisabled);
                    });
                }
            });
        }
    }

    // what edit mode changes: work, medicine, area and policies
    private static void PawnSettings(FileNode f, Pawn pawn)
    {
        if (pawn.workSettings != null && pawn.workSettings.EverWork)
        {
            Dir("work", f, d =>
            {
                foreach (WorkTypeDef w in EditCommands.WorkTypes)
                {
                    WorkTypeDef work = w;
                    Writable(d, EditCommands.WorkWord(work), () => pawn.WorkTypeIsDisabled(work) ? "disabled" : pawn.workSettings.GetPriority(work).ToString(), value =>
                    {
                        if (!int.TryParse(value.Trim(), out int priority) || priority < 0 || priority > 4)
                        {
                            return "work priorities go from 0 (off) to 4";
                        }
                        if (priority > 0 && pawn.WorkTypeIsDisabled(work))
                        {
                            return pawn.LabelShort + " can't do " + work.labelShort;
                        }
                        // the game uses 3 for on when manual priorities are off
                        pawn.workSettings.SetPriority(work, priority > 0 && !Find.PlaySettings.useWorkPriorities ? 3 : priority);
                        return null;
                    });
                }
            }).alias = WorkAlias;
        }
        if (pawn.playerSettings == null)
        {
            return;
        }
        Writable(f, "medicine", () => EditCommands.careNames.FirstOrDefault(c => c.care == pawn.playerSettings.medCare).word ?? "none", value =>
        {
            (string word, MedicalCareCategory care) found = EditCommands.careNames.FirstOrDefault(c => c.word.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
            if (found.word == null)
            {
                return "medicine is one of " + string.Join(", ", EditCommands.careNames.Select(c => c.word));
            }
            pawn.playerSettings.medCare = found.care;
            return null;
        });
        if (pawn.playerSettings.SupportsAllowedAreas)
        {
            Writable(f, "area", () => pawn.playerSettings.AreaRestrictionInPawnCurrentMap is Area a ? EditCommands.AreaWord(a) : "unrestricted", value =>
            {
                string wanted = value.Trim().Replace('_', ' ');
                if (pawn.MapHeld == null)
                {
                    return pawn.LabelShort + " isn't on a map";
                }
                if (wanted.Equals("unrestricted", StringComparison.OrdinalIgnoreCase))
                {
                    pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null;
                    return null;
                }
                Area area = EditCommands.Areas(pawn.MapHeld).FirstOrDefault(a => a.Label.Equals(wanted, StringComparison.OrdinalIgnoreCase));
                if (area == null)
                {
                    return "no area called " + wanted + " where " + pawn.LabelShort + " is, the areas are " + string.Join(", ", EditCommands.Areas(pawn.MapHeld).Select(EditCommands.AreaWord)) + " and unrestricted";
                }
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
                return null;
            });
        }
        Dir("policy", f, d =>
        {
            foreach (string kind in EditCommands.kinds)
            {
                string k = kind;
                if (EditCommands.CurrentPolicy(pawn, k) == null)
                {
                    continue;
                }
                Writable(d, k, () => EditCommands.CurrentPolicy(pawn, k) is Policy p ? EditCommands.PolicyWord(p) : "none", value =>
                {
                    string wanted = value.Trim().Replace('_', ' ');
                    Policy policy = EditCommands.Policies(k).FirstOrDefault(x => x.label.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                        ?? EditCommands.Policies(k).FirstOrDefault(x => x.label.StartsWith(wanted, StringComparison.OrdinalIgnoreCase));
                    if (policy == null)
                    {
                        return "no " + k + " policy called " + wanted + ", they are " + string.Join(", ", EditCommands.Policies(k).Select(EditCommands.PolicyWord));
                    }
                    return EditCommands.SetPolicy(pawn, k, policy) ? null : pawn.LabelShort + " has no " + k + " policy";
                });
            }
        });
    }

    // each group a file with the names in it, set changes who is in it, none removes it
    private static void Groups(FileNode d)
    {
        Recorder rec = Recorder.Current;
        if (rec == null)
        {
            return;
        }
        foreach (PawnGroup g in rec.Groups)
        {
            PawnGroup group = g;
            Writable(d, group.name, () => Names(group.pawns.Where(p => p != null && !p.Dead)), value => SetGroup(group.name, value));
        }
    }

    private static string MakeGroup(string name, string value)
    {
        string bad = Files.BadName(name);
        return bad ?? SetGroup(name, value);
    }

    private static string SetGroup(string name, string value)
    {
        Recorder rec = Recorder.Current;
        string[] words = value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words.Length == 1 && words[0].Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            rec.RemoveGroup(name);
            return null;
        }
        List<Pawn> pawns = new List<Pawn>();
        foreach (string word in words)
        {
            List<Pawn> found = EditCommands.Who(word, out string error);
            if (found == null)
            {
                return error;
            }
            pawns.AddRange(found.Where(p => !pawns.Contains(p)));
        }
        rec.SetGroup(name, pawns);
        return null;
    }

    // numbered folders, one for each, in the order given
    private static void Numbered<T>(FileNode d, IEnumerable<T> things, Action<FileNode, T> files)
    {
        int i = 0;
        foreach (T thing in things.ToList())
        {
            T t = thing;
            Dir((++i).ToString("00"), d, f => files(f, t));
        }
    }

    private static string Period(int ticks) => ticks <= 0 ? "none" : ticks.ToStringTicksToPeriod();

    private static readonly AccessTools.FieldRef<AlertsReadout, List<Alert>> activeAlerts = AccessTools.FieldRefAccess<AlertsReadout, List<Alert>>("activeAlerts");

    // the alerts on the right of the screen, most urgent first
    private static void Alerts(FileNode d)
    {
        List<Alert> alerts = Find.UIRoot is UIRoot_Play ? activeAlerts(Find.Alerts) : new List<Alert>();
        File(d, "count", () => alerts.Count);
        Dir("list", d, l => Numbered(l, alerts.OrderByDescending(a => a.Priority), (f, a) =>
        {
            File(f, "name", () => a.GetLabel());
            File(f, "priority", () => a.Priority.ToString().ToLowerInvariant());
            File(f, "about", () => a.GetExplanation().ToString().StripTags());
        }));
    }

    // the letters waiting on the right, newest first
    private static void Letters(FileNode d)
    {
        List<Letter> letters = Find.LetterStack.LettersListForReading.ToList();
        File(d, "count", () => letters.Count);
        Dir("list", d, l => Numbered(l, Enumerable.Reverse(letters), (f, let) =>
        {
            File(f, "name", () => let.Label.ToString().StripTags());
            File(f, "kind", () => let.def.defName.ToLowerInvariant());
            File(f, "when", () => Scripts.When(GenDate.TickGameToAbs(let.arrivalTick)));
        }));
    }

    private static void Research(FileNode d)
    {
        File(d, "current", () => Find.ResearchManager.GetProject()?.LabelCap.ToString() ?? "none");
        File(d, "progress", () => (Find.ResearchManager.GetProject()?.ProgressPercent ?? 0f) * 100f);
        File(d, "done", () => DefDatabase<ResearchProjectDef>.AllDefs.Count(r => r.IsFinished));
        File(d, "total", () => DefDatabase<ResearchProjectDef>.AllDefs.Count());
        File(d, "available", () => string.Join(", ", DefDatabase<ResearchProjectDef>.AllDefs.Where(r => r.CanStartNow).Select(r => r.LabelCap.ToString()).OrderBy(n => n)) is string a && a.Length > 0 ? a : "none");
    }

    private static void PrisonerFiles(FileNode f, Pawn p)
    {
        File(f, "name", () => p.LabelShort);
        File(f, "faction", () => p.Faction?.Name ?? "no faction");
        File(f, "resistance", () => p.guest?.resistance ?? 0f, "0.#");
        File(f, "will", () => p.guest?.will ?? 0f, "0.#");
        File(f, "recruitable", () => p.guest?.Recruitable ?? false);
        File(f, "mode", () => p.guest?.ExclusiveInteractionMode?.label ?? "none");
        File(f, "health", () => p.health.summaryHealth.SummaryHealthPercent * 100f);
        File(f, "downed", () => p.Downed);
    }

    private static void AnimalFiles(FileNode f, Pawn p)
    {
        File(f, "name", () => p.LabelShort);
        File(f, "kind", () => p.KindLabel);
        File(f, "health", () => p.health.summaryHealth.SummaryHealthPercent * 100f);
        File(f, "food", () => Percent(p.needs?.food?.CurLevelPercentage));
        File(f, "master", () => p.playerSettings?.Master?.LabelShort ?? "none");
        File(f, "bonded", () => p.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond)?.LabelShort ?? "none");
        File(f, "downed", () => p.Downed);
    }

    private static void MechFiles(FileNode f, Pawn p)
    {
        File(f, "name", () => p.LabelShort);
        File(f, "kind", () => p.KindLabel);
        File(f, "health", () => p.health.summaryHealth.SummaryHealthPercent * 100f);
        File(f, "energy", () => Percent(p.needs?.energy?.CurLevelPercentage));
        File(f, "overseer", () => p.GetOverseer()?.LabelShort ?? "none");
    }

    // quests waiting for an answer or going on
    private static void Quests(FileNode d)
    {
        List<Quest> quests = Find.QuestManager.QuestsListForReading.Where(q => !q.hidden && !q.hiddenInUI && (q.State == QuestState.NotYetAccepted || q.State == QuestState.Ongoing)).ToList();
        File(d, "count", () => quests.Count);
        Dir("list", d, l => Numbered(l, quests, (f, q) =>
        {
            File(f, "name", () => q.name.StripTags());
            File(f, "state", () => q.State == QuestState.Ongoing ? "ongoing" : "waiting");
            File(f, "expires", () => q.State == QuestState.NotYetAccepted ? Period(q.TicksUntilExpiry) : "none");
        }));
    }

    // other factions' towns, closest first
    private static void Towns(FileNode d)
    {
        Numbered(d, EditCommands.NearTowns(), (FileNode f, (Settlement town, float tiles) t) =>
        {
            File(f, "name", () => t.town.Label);
            File(f, "faction", () => t.town.Faction?.Name ?? "none");
            File(f, "relation", () => t.town.Faction == null ? "none" : t.town.Faction.PlayerRelationKind.GetLabel());
            File(f, "distance", () => t.tiles, "0");
        });
    }

    private static bool Breaking(Pawn p) =>
        p.mindState?.mentalBreaker != null && p.needs?.mood != null && p.needs.mood.CurLevel < p.mindState.mentalBreaker.BreakThresholdMinor;

    private static bool IsIdle(Pawn p) =>
        p.Spawned && !p.Downed && !p.Drafted && (p.mindState.IsIdle || p.CurJobDef == JobDefOf.Wait_Wander || p.CurJobDef == JobDefOf.GotoWander || p.CurJobDef == JobDefOf.Wait);

    private static void Health(FileNode d)
    {
        File(d, "bleeding", () => Colonists().Count(p => p.health.hediffSet.BleedRateTotal > 0f));
        File(d, "untended", () => Colonists().Count(p => p.health.HasHediffsNeedingTend()));
        File(d, "sick", () => Colonists().Count(p => p.health.hediffSet.hediffs.Any(h => h.def.makesSickThought)));
        File(d, "downed", () => Colonists().Count(p => p.Downed));
        File(d, "breaking", () => Colonists().Count(Breaking));
        File(d, "mental", () => Colonists().Count(p => p.InMentalState));
        File(d, "names", () => Names(Colonists().Where(p => p.health.HasHediffsNeedingTend() || p.Downed)));
        Dir("list", d, HealthList);
    }

    // everyone of ours with their wounds and diseases: colonists, slaves, animals and prisoners
    private static void HealthList(FileNode d)
    {
        List<Pawn> ours = MapCommands.Ours();
        for (int i = 0; i < ours.Count; i++)
        {
            Pawn p = ours[i];
            Dir((i + 1).ToString("00"), d, f =>
            {
                List<Hediff> all = p.health.hediffSet.hediffs.Where(h => h.Visible).ToList();
                List<Hediff> open = all.Where(h => !h.IsPermanent() && !h.IsTended() && (h.TendableNow() || h.Bleeding)).OrderByDescending(h => h.BleedRate).ToList();
                List<Hediff> sick = all.Where(h => h.TryGetComp<HediffComp_Immunizable>() != null).ToList();
                List<Hediff> chronic = all.Where(h => h.def.chronic && h.TryGetComp<HediffComp_Immunizable>() == null).ToList();
                // old wounds and lost parts stay tended forever, those aren't news
                List<Hediff> tended = all.Where(h => h.IsTended() && !h.IsPermanent() && !(h is Hediff_MissingPart) && !sick.Contains(h) && !chronic.Contains(h)).ToList();
                File(f, "name", () => MapCommands.Who(p));
                File(f, "bleeding", () => p.health.hediffSet.BleedRateTotal * 100f);
                File(f, "dies", () =>
                {
                    if (p.health.hediffSet.BleedRateTotal <= 0f)
                    {
                        return "none";
                    }
                    int ticks = HealthUtility.TicksUntilDeathDueToBloodLoss(p);
                    return ticks < GenDate.TicksPerDay ? ticks.ToStringTicksToPeriod() : "none";
                });
                File(f, "downed", () => p.Downed);
                Dir("open", f, o => Hediffs(o, open, false, (h, k) => File(k, "bleed", () => h.BleedRate * 100f)));
                Dir("sick", f, o => Hediffs(o, sick, true, (h, k) =>
                {
                    HediffComp_Immunizable imm = h.TryGetComp<HediffComp_Immunizable>();
                    File(k, "severity", () => h.Severity * 100f);
                    File(k, "max", () => h.def.maxSeverity * 100f);
                    File(k, "immunity", () => imm.Props.immunityPerDaySick <= 0f ? "none" : (imm.Immunity * 100f).ToString("0", CultureInfo.InvariantCulture));
                }));
                Dir("chronic", f, o => Hediffs(o, chronic, false, null));
                Dir("tended", f, o => Hediffs(o, tended, false, null));
            });
        }
    }

    // a folder for each one: name, part, tend, and what more is asked, diseases go by their name without the part
    private static void Hediffs(FileNode d, List<Hediff> list, bool baseName, Action<Hediff, FileNode> more)
    {
        for (int i = 0; i < list.Count; i++)
        {
            Hediff h = list[i];
            Dir((i + 1).ToString("00"), d, k =>
            {
                File(k, "name", () => baseName ? h.LabelBaseCap : h.LabelCap);
                File(k, "part", () => h.Part?.Label ?? "none");
                File(k, "bleeding", () => h.Bleeding);
                File(k, "tend", () => MapCommands.Tend(h));
                more?.Invoke(h, k);
            });
        }
    }

    private static List<Pawn> Enemies() => MapCommands.OnMaps(p => !p.Dead && p.HostileTo(Faction.OfPlayer) && !p.IsPrisonerOfColony).ToList();

    private static List<Pawn> Predators() => MapCommands.OnMaps(p => !p.Dead && p.Faction == null && p.RaceProps.predator && !p.HostileTo(Faction.OfPlayer)).ToList();

    private static void Danger(FileNode d)
    {
        File(d, "count", () => Enemies().Count + Predators().Count);
        File(d, "enemies", () => Enemies().Count);
        File(d, "predators", () => Predators().Count);
        // cells between the closest enemy or predator and a colonist, none when there's nothing
        File(d, "closest", () =>
        {
            List<Pawn> colonists = MapCommands.OnMaps(p => p.IsColonist && !p.Dead).ToList();
            float best = float.MaxValue;
            foreach (Pawn e in Enemies().Concat(Predators()))
            {
                foreach (Pawn c in colonists)
                {
                    if (c.Map == e.Map)
                    {
                        best = Mathf.Min(best, c.Position.DistanceTo(e.Position));
                    }
                }
            }
            return best == float.MaxValue ? "none" : best.ToString("0", CultureInfo.InvariantCulture);
        });
        Dir("list", d, DangerList);
    }

    // each enemy or predator, animals of the same kind and state as one with how many, closest first
    private static void DangerList(FileNode d)
    {
        List<Pawn> colonists = MapCommands.OnMaps(p => p.IsColonist && !p.Dead).ToList();
        List<Pawn> enemies = Enemies();
        var groups = enemies.Concat(Predators())
            .GroupBy(p => p.RaceProps.Humanlike ? p.thingIDNumber.ToString() : p.def.defName + "|" + p.Faction?.loadID + "|" + MapCommands.State(p))
            .Select(g => (pawns: g.ToList(), distance: g.Min(p => Closest(p, colonists))))
            .OrderBy(x => enemies.Contains(x.pawns[0]) ? 0 : 1)
            .ThenBy(x => x.distance)
            .ToList();
        for (int i = 0; i < groups.Count; i++)
        {
            List<Pawn> group = groups[i].pawns;
            float distance = groups[i].distance;
            Pawn p = group[0];
            Dir((i + 1).ToString("00"), d, f =>
            {
                File(f, "name", () => group.Count > 1 ? p.KindLabel + " x" + group.Count : p.LabelShort);
                File(f, "count", () => group.Count);
                File(f, "kind", () => p.KindLabel);
                File(f, "faction", () => p.Faction?.Name ?? "wild");
                File(f, "enemy", () => enemies.Contains(p));
                File(f, "health", () => group.Average(x => x.health.summaryHealth.SummaryHealthPercent) * 100f);
                File(f, "distance", () => distance < 9999f ? distance.ToString("0", CultureInfo.InvariantCulture) : "none");
                File(f, "state", () => MapCommands.State(p) ?? "none");
            });
        }
    }

    private static float Closest(Pawn p, List<Pawn> colonists)
    {
        float best = 99999f;
        foreach (Pawn c in colonists)
        {
            if (c.Map == p.Map)
            {
                best = Mathf.Min(best, c.Position.DistanceTo(p.Position));
            }
        }
        return best;
    }

    private static void Visitors(FileNode d)
    {
        IEnumerable<Pawn> Visiting() => MapCommands.OnMaps(p => p.RaceProps.Humanlike && p.Faction != Faction.OfPlayer && !p.IsPrisonerOfColony && !p.IsSlaveOfColony && !p.HostileTo(Faction.OfPlayer));
        File(d, "count", () => Visiting().Count());
        File(d, "traders", () => Visiting().Count(p => p.trader?.traderKind != null));
        Dir("list", d, l =>
        {
            List<Pawn> pawns = Visiting().OrderBy(p => p.Faction?.Name).ThenBy(p => p.LabelShort).ToList();
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                Dir((i + 1).ToString("00"), l, f =>
                {
                    File(f, "name", () => p.LabelShort);
                    File(f, "kind", () => p.KindLabel);
                    File(f, "faction", () => p.Faction?.Name ?? "no faction");
                    File(f, "relation", () => p.Faction == null || p.Faction.IsPlayer ? "none" : p.Faction.PlayerRelationKind.GetLabel());
                    File(f, "why", () => MapCommands.Why(p) ?? "none");
                    File(f, "downed", () => p.Downed);
                });
            }
        });
    }

    private static void Factions(FileNode d)
    {
        List<Faction> factions = Find.FactionManager.AllFactionsVisibleInViewOrder.Where(f => !f.IsPlayer && !f.defeated && !f.temporary).ToList();
        for (int i = 0; i < factions.Count; i++)
        {
            Faction fa = factions[i];
            Dir((i + 1).ToString("00"), d, f =>
            {
                File(f, "name", () => fa.Name);
                File(f, "kind", () => fa.def.LabelCap);
                File(f, "relation", () => fa.PlayerRelationKind.GetLabel());
                File(f, "goodwill", () => fa.HasGoodwill ? fa.PlayerGoodwill.ToString() : "none");
                File(f, "leader", () => fa.leader?.LabelShort ?? "none");
            });
        }
    }

    private static void Idle(FileNode d)
    {
        IEnumerable<Pawn> Idlers() => Colonists().Where(IsIdle);
        File(d, "count", () => Idlers().Count());
        File(d, "names", () => Names(Idlers()));
    }

    private static void Power(FileNode d)
    {
        File(d, "made", () => PowerOf().made);
        File(d, "used", () => PowerOf().used);
        File(d, "stored", () => PowerOf().stored);
        File(d, "max", () => PowerOf().max);
    }

    private static (float made, float used, float stored, float max) PowerOf()
    {
        float made = 0f, used = 0f, stored = 0f, max = 0f;
        Map map = Home;
        if (map == null)
        {
            return (0f, 0f, 0f, 0f);
        }
        foreach (PowerNet net in map.powerNetManager.AllNetsListForReading)
        {
            foreach (CompPowerTrader comp in net.powerComps)
            {
                if (!comp.PowerOn)
                {
                    continue;
                }
                if (comp.PowerOutput > 0f)
                {
                    made += comp.PowerOutput;
                }
                else
                {
                    used -= comp.PowerOutput;
                }
            }
            foreach (CompPowerBattery battery in net.batteryComps)
            {
                stored += battery.StoredEnergy;
                max += battery.Props.storedEnergyMax;
            }
        }
        return (made, used, stored, max);
    }

    private static void Stock(FileNode d)
    {
        // days of food, the same the game's low food alert counts
        File(d, "food", () =>
        {
            Map map = Home;
            int eaters = map == null ? 0 : map.mapPawns.FreeColonistsSpawnedCount + map.mapPawns.PrisonersOfColonySpawnedCount;
            return eaters == 0 ? 0f : map.resourceCounter.TotalHumanEdibleNutrition / eaters;
        }, "0.#");
        File(d, "meals", () => Counted(t => t.IsIngestible && t.ingestible.IsMeal));
        File(d, "medicine", () => Counted(t => t.IsMedicine));
        File(d, "silver", () => Count(ThingDefOf.Silver));
        File(d, "steel", () => Count(ThingDefOf.Steel));
        File(d, "wood", () => Count(ThingDefOf.WoodLog));
        File(d, "components", () => Count(ThingDefOf.ComponentIndustrial));
        File(d, "plasteel", () => Count(ThingDefOf.Plasteel));
    }

    private static int Count(ThingDef def) => Home?.resourceCounter.GetCount(def) ?? 0;

    private static int Counted(Func<ThingDef, bool> which) =>
        Home?.resourceCounter.AllCountedAmounts.Where(kv => which(kv.Key)).Sum(kv => kv.Value) ?? 0;

    // the paths a path with * in it stands for, with the word the * took
    public static List<(FileNode node, string star)> Expand(string path, FileNode top = null)
    {
        List<(FileNode node, string star)> found = new List<(FileNode, string)>();
        FileNode start = path.StartsWith("~") ? Files.Root : top ?? Top();
        if (start == null)
        {
            return found;
        }
        string[] parts = path.TrimStart('~').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        Walk(start, parts, 0, null, found);
        return found;
    }

    private static void Walk(FileNode n, string[] parts, int i, string star, List<(FileNode, string)> found)
    {
        if (i == parts.Length)
        {
            found.Add((n, star));
            return;
        }
        if (!n.folder)
        {
            return;
        }
        if (parts[i] == "*")
        {
            foreach (FileNode c in n.Kids)
            {
                Walk(c, parts, i + 1, star ?? c.name, found);
            }
            return;
        }
        FileNode next = n.Child(parts[i]);
        if (next != null)
        {
            Walk(next, parts, i + 1, star, found);
        }
    }
}
