using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace JustTerminal;

// one value per in game hour, NaN when there was nothing to record
public class Series : IExposable
{
    public string key;
    public string label;
    // hour index of values[0], hours counted from tick 0 of the game
    public int start;
    public List<float> values = new List<float>();

    public int End => start + values.Count;

    public float At(int hour) => hour >= start && hour < End ? values[hour - start] : float.NaN;

    public void Set(int hour, float value)
    {
        if (values.Count == 0)
        {
            start = hour;
        }
        if (hour < start)
        {
            return;
        }
        while (End <= hour)
        {
            values.Add(float.NaN);
        }
        values[hour - start] = value;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref key, "key");
        Scribe_Values.Look(ref label, "label");
        Scribe_Values.Look(ref start, "start");
        byte[] bytes = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            bytes = new byte[values.Count * 4];
            for (int i = 0; i < values.Count; i++)
            {
                BitConverter.TryWriteBytes(new Span<byte>(bytes, i * 4, 4), values[i]);
            }
        }
        DataExposeUtility.LookByteArray(ref bytes, "values");
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            values = new List<float>();
            if (bytes != null)
            {
                for (int i = 0; i + 4 <= bytes.Length; i += 4)
                {
                    values.Add(BitConverter.ToSingle(bytes, i));
                }
            }
        }
    }
}

// records what the game itself doesn't keep, saved with the game
public class Recorder : GameComponent
{
    public const int TicksPerHour = 2500;

    private List<Series> series = new List<Series>();
    private List<PawnGroup> groups = new List<PawnGroup>();
    private FileNode files = new FileNode { name = "~", folder = true };

    // the folders and text files made in the terminal
    public FileNode Files => files;

    public void ClearFiles() => files = new FileNode { name = "~", folder = true };

    public List<PawnGroup> Groups => groups;

    // spots saved with place save, for goto
    private List<SavedPlace> places = new List<SavedPlace>();
    public List<SavedPlace> Places => places;

    public PawnGroup FindGroup(string name) => groups.FirstOrDefault(g => g.name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public void SetGroup(string name, List<Pawn> pawns)
    {
        RemoveGroup(name);
        groups.Add(new PawnGroup { name = name, pawns = pawns });
    }

    public bool RemoveGroup(string name) => groups.RemoveAll(g => g.name.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0;
    private Dictionary<string, Series> byKey;

    public static Recorder Current => Verse.Current.Game?.GetComponent<Recorder>();

    public Recorder(Game game)
    {
    }

    public static int HourNow => Find.TickManager.TicksGame / TicksPerHour;

    public Series Get(string key)
    {
        if (byKey == null)
        {
            byKey = series.ToDictionary(s => s.key);
        }
        return byKey.TryGetValue(key, out Series s) ? s : null;
    }

    public IEnumerable<Series> All => series;

    public int HoursRecorded => series.Count == 0 ? 0 : series.Max(s => s.End) - series.Min(s => s.start);

    public void Clear()
    {
        series.Clear();
        byKey = null;
        Record();
    }

    private Series GetOrAdd(string key, string label)
    {
        Series s = Get(key);
        if (s == null)
        {
            s = new Series { key = key, label = label };
            series.Add(s);
            byKey[key] = s;
        }
        s.label = label;
        return s;
    }

    public override void GameComponentTick()
    {
        Events.Tick();
        if (Find.TickManager.TicksGame % TicksPerHour == 0)
        {
            Record();
            Scripts.Tick();
        }
    }

    public override void FinalizeInit()
    {
        // so a new game or a game that just got the mod has a first point right away
        Record();
    }

    public void Record()
    {
        try
        {
            int hour = HourNow;
            Map map = Commands.HomeMap;
            if (map != null)
            {
                GetOrAdd("temp", "temperature").Set(hour, map.mapTemperature.OutdoorTemp);
                int eaters = map.mapPawns.FreeColonistsSpawnedCount + map.mapPawns.PrisonersOfColonySpawnedCount;
                if (eaters > 0)
                {
                    // same days the game's low food alert counts
                    GetOrAdd("food", "food").Set(hour, map.resourceCounter.TotalHumanEdibleNutrition / eaters);
                }
            }
            List<Pawn> colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep;
            float sum = 0f;
            int count = 0;
            foreach (Pawn p in colonists)
            {
                if (p.needs?.mood == null)
                {
                    continue;
                }
                float mood = p.needs.mood.CurLevelPercentage * 100f;
                GetOrAdd("mood:" + p.GetUniqueLoadID(), p.LabelShort).Set(hour, mood);
                sum += mood;
                count++;
            }
            if (count > 0)
            {
                GetOrAdd("mood", "mood").Set(hour, sum / count);
            }
        }
        catch (Exception e)
        {
            Log.ErrorOnce("[JustTerminal] recording failed: " + e, 0x4a544d01);
        }
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref series, "series", LookMode.Deep);
        Scribe_Collections.Look(ref groups, "groups", LookMode.Deep);
        Scribe_Collections.Look(ref places, "places", LookMode.Deep);
        Scribe_Deep.Look(ref files, "files");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            series ??= new List<Series>();
            series.RemoveAll(s => s?.key == null);
            groups ??= new List<PawnGroup>();
            groups.RemoveAll(g => g?.name == null);
            places ??= new List<SavedPlace>();
            places.RemoveAll(p => p?.name == null);
            files ??= new FileNode { name = "~", folder = true };
            files.parent = null;
            byKey = null;
        }
    }
}
