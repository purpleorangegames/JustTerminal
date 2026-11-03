using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace JustTerminal;

// health and who's around, all read only
public static class MapCommands
{
    // ours: colonists, slaves, animals and mechs, and our prisoners
    public static List<Pawn> Ours() =>
        PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction
            .Concat(PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_PrisonersOfColony)
            .Distinct()
            .OrderBy(p => p.RaceProps.Humanlike ? 0 : 1)
            .ThenBy(p => p.LabelShort)
            .ToList();

    private static bool NeedGame(Shell sh)
    {
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null)
        {
            sh.Print("no game running", Ink.Bad);
            return false;
        }
        return true;
    }

    public static string Who(Pawn p)
    {
        if (p.IsPrisonerOfColony)
        {
            return p.LabelShort + " (prisoner)";
        }
        if (p.IsSlaveOfColony)
        {
            return p.LabelShort + " (slave)";
        }
        return p.RaceProps.Humanlike ? p.LabelShort : p.LabelShort + " (" + p.KindLabel + ")";
    }

    public static string Part(Hediff h) => h.Part != null ? " · " + h.Part.Label : "";



    // tended 40% for 2 days, needs tending, or none
    public static string Tend(Hediff h)
    {
        HediffComp_TendDuration tend = h.TryGetComp<HediffComp_TendDuration>();
        if (tend == null)
        {
            return "none";
        }
        if (tend.IsTended)
        {
            return "tended " + tend.tendQuality.ToStringPercent() + (tend.tendTicksLeft > 0 ? " for " + tend.tendTicksLeft.ToStringTicksToPeriod() : "");
        }
        return h.TendableNow() ? "needs tending" : "none";
    }


    public static IEnumerable<Pawn> OnMaps(System.Func<Pawn, bool> which) =>
        Verse.Find.Maps.Where(m => m.IsPlayerHome || m.mapPawns.AnyColonistSpawned).SelectMany(m => m.mapPawns.AllPawnsSpawned).Where(which);


    public static string Why(Pawn p)
    {
        if (p.trader?.traderKind != null)
        {
            return p.trader.traderKind.label;
        }
        if (p.guest != null && p.GuestStatus == GuestStatus.Guest)
        {
            return "guest";
        }
        string lord = p.GetLord()?.LordJob?.GetType().Name;
        return lord switch
        {
            "LordJob_TradeWithColony" => "trader",
            "LordJob_VisitColony" => "visiting",
            "LordJob_ExitMapBest" => "leaving",
            "LordJob_TravelAndExit" => "passing by",
            _ => null
        };
    }

    // what an enemy or animal is up to, null when nothing special
    public static string State(Pawn p) =>
        p.Downed ? "downed" : p.InMentalState ? p.MentalStateDef.label : p.GetComp<CompCanBeDormant>() is { Awake: false } ? "dormant" : p.CurJobDef == JobDefOf.LayDown ? "asleep" : null;
}
