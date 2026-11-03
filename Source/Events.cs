using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace JustTerminal;

// things that happen, for cron scripts with when instead of every
public static class Events
{
    public static readonly string[] Names = { "raid", "downed", "break", "sick", "death", "letter", "trader" };

    // set while a script sends a letter, so it doesn't count
    public static bool Quiet;

    private static readonly List<(string name, string who, string what)> queue = new List<(string, string, string)>();

    // what the last look found, to see what's new
    private static Game seenGame;
    private static int enemies;
    private static int traders;
    private static HashSet<Pawn> downed = new HashSet<Pawn>();
    private static HashSet<Pawn> breaking = new HashSet<Pawn>();
    private static HashSet<Hediff> diseases = new HashSet<Hediff>();
    private static HashSet<Pawn> alive = new HashSet<Pawn>();

    public static void Add(string name, string who, string what) => queue.Add((name, who, what));

    private static string Who(Pawn p) => p.LabelShort.Replace(' ', '_');

    // once a second of game time, and the events found run their scripts
    public static void Tick()
    {
        if (Find.TickManager.TicksGame % 60 == 0)
        {
            try
            {
                Look();
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[JustTerminal] looking for events failed: " + e, 0x4a544d02);
            }
        }
        if (queue.Count == 0)
        {
            return;
        }
        List<(string name, string who, string what)> now = queue.ToList();
        queue.Clear();
        foreach ((string name, string who, string what) in now)
        {
            Scripts.RunEvent(name, who, what);
        }
    }

    internal static void Look()
    {
        List<Pawn> colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep.ToList();
        int enemyCount = MapCommands.OnMaps(p => !p.Dead && !p.Downed && p.HostileTo(Faction.OfPlayer) && !p.IsPrisonerOfColony).Count();
        int traderCount = MapCommands.OnMaps(p => p.trader?.traderKind != null && !p.HostileTo(Faction.OfPlayer)).Count();
        HashSet<Pawn> nowDowned = new HashSet<Pawn>(colonists.Where(p => p.Downed));
        HashSet<Pawn> nowBreaking = new HashSet<Pawn>(colonists.Where(p => p.InMentalState));
        HashSet<Hediff> nowDiseases = new HashSet<Hediff>(colonists.SelectMany(p => p.health.hediffSet.hediffs).Where(h => h.def.makesSickThought));
        // the first look after loading only learns how things are
        bool first = seenGame != Current.Game;
        if (!first)
        {
            if (enemies == 0 && enemyCount > 0)
            {
                Add("raid", "", enemyCount.ToString());
            }
            if (traders == 0 && traderCount > 0)
            {
                Add("trader", "", traderCount.ToString());
            }
            foreach (Pawn p in nowDowned.Where(p => !downed.Contains(p)))
            {
                Add("downed", Who(p), "downed");
            }
            foreach (Pawn p in nowBreaking.Where(p => !breaking.Contains(p)))
            {
                Add("break", Who(p), p.MentalStateDef?.label ?? "mental break");
            }
            foreach (Hediff h in nowDiseases.Where(h => !diseases.Contains(h)))
            {
                Add("sick", Who(h.pawn), h.LabelBase);
            }
            foreach (Pawn p in alive.Where(p => p.Dead))
            {
                Add("death", Who(p), "died");
            }
        }
        seenGame = Current.Game;
        enemies = enemyCount;
        traders = traderCount;
        downed = nowDowned;
        breaking = nowBreaking;
        diseases = nowDiseases;
        alive = new HashSet<Pawn>(colonists);
    }
}

[HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
public static class LetterStack_ReceiveLetter_Patch
{
    public static void Postfix(Letter let)
    {
        if (!Events.Quiet && Current.ProgramState == ProgramState.Playing && let != null)
        {
            Events.Add("letter", "", let.Label.ToString());
        }
    }
}
