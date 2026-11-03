using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace JustTerminal;

// graph <what> [pawn] [days back] [days ahead]
public static class GraphCommand
{
    private static readonly string[] kinds = { "temp", "mood", "food", "wealth", "colonists", "threat" };

    // graph types first, colonist names only after graph mood
    public static List<string> Complete(string[] done)
    {
        if (done.Length == 0)
        {
            return kinds.ToList();
        }
        Recorder rec = Recorder.Current;
        if (done.Length == 1 && done[0].Equals("mood", StringComparison.OrdinalIgnoreCase) && rec != null)
        {
            return rec.All.Where(s => s.key.StartsWith("mood:")).Select(s => s.label.Replace(' ', '_')).ToList();
        }
        return new List<string>();
    }

    public static void Run(Shell sh, string[] args)
    {
        if (Current.ProgramState != ProgramState.Playing || Current.Game == null)
        {
            sh.Print("no game running", Ink.Bad);
            return;
        }
        if (args.Length == 0)
        {
            Usage(sh);
            return;
        }
        string kind = args[0].ToLowerInvariant();
        List<int> numbers = new List<int>();
        List<string> words = new List<string>();
        foreach (string a in args.Skip(1))
        {
            if (int.TryParse(a, out int n))
            {
                numbers.Add(Math.Max(0, n));
            }
            else
            {
                words.Add(a);
            }
        }
        int now = Recorder.HourNow;
        int back = numbers.Count > 0 ? numbers[0] : -1;
        int ahead = numbers.Count > 1 ? numbers[1] : -1;
        Recorder rec = Recorder.Current;
        Graph g;
        switch (kind)
        {
            case "temp":
            case "temperature":
                g = Temperature(back < 0 ? 3 : back, ahead < 0 ? 1 : ahead);
                break;
            case "mood":
                if (words.Count > 0)
                {
                    string name = string.Join(" ", words).Replace('_', ' ');
                    Series s = rec?.All.FirstOrDefault(x => x.key.StartsWith("mood:") && x.label.Equals(name, StringComparison.OrdinalIgnoreCase))
                        ?? rec?.All.FirstOrDefault(x => x.key.StartsWith("mood:") && x.label.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (s == null)
                    {
                        sh.Print("no mood recorded for " + name, Ink.Bad);
                        return;
                    }
                    g = Mood(s.label + " mood", s.At, Since(s, back < 0 ? 7 : back));
                    g.note = Note(s, g.fromHour);
                }
                else
                {
                    // ours each hour, before it the game's own every half day
                    Series s = rec?.Get("mood");
                    Func<int, float> game = Graph.FromGameHistory("ColonistMood");
                    g = Mood("colony mood", h => s != null && !float.IsNaN(s.At(h)) ? s.At(h) : game(h), back < 0 ? now - 7 * 24 : now - back * 24);
                }
                break;
            case "food":
            {
                Series s = rec?.Get("food");
                if (s == null)
                {
                    sh.Print("food: nothing recorded yet", Ink.Dim);
                    return;
                }
                g = new Graph
                {
                    title = "food (days for each colonist and prisoner)",
                    valueAt = s.At,
                    fromHour = Since(s, back < 0 ? 7 : back),
                    fixedMin = 0f,
                    format = v => v.ToString("0.#") + "d",
                    inkFor = v => v < 2f ? Ink.Bad : v < 5f ? Ink.Warn : Ink.Good
                };
                g.note = Note(s, g.fromHour);
                break;
            }
            case "wealth":
                g = new Graph
                {
                    title = "wealth",
                    fixedMin = 0f,
                    valueAt = Graph.FromGameHistory("Wealth_Total"),
                    fromHour = back < 0 ? 0 : now - back * 24,
                    format = Short,
                    inkFor = _ => Ink.Normal
                };
                break;
            case "colonists":
                g = new Graph
                {
                    title = "colonists",
                    valueAt = Graph.FromGameHistory("FreeColonists"),
                    fromHour = back < 0 ? 0 : now - back * 24,
                    fixedMin = 0f,
                    format = v => v.ToString("0")
                };
                break;
            case "threat":
                Func<int, float> threat = Graph.FromGameHistory("ThreatPoints");
                g = new Graph
                {
                    title = "threat points",
                    // the game keeps it divided by 10
                    valueAt = h => threat(h) * 10f,
                    fromHour = back < 0 ? 0 : now - back * 24,
                    fixedMin = 0f,
                    format = Short,
                    inkFor = _ => Ink.Warn
                };
                break;
            default:
                sh.Print("unknown graph " + args[0], Ink.Bad);
                Usage(sh);
                return;
        }
        g.fromHour = Mathf.Max(0, g.fromHour);
        if (g.toHour == 0)
        {
            g.toHour = now + 1;
        }
        g.Draw(sh);
    }

    private static void Usage(Shell sh)
    {
        sh.Print("usage: graph <what> [days back] [days ahead]", Ink.Dim);
        sh.Print("  temp       outside temperature, 3 days back and 1 ahead");
        sh.Print("  mood       colony mood, or the mood of one colonist: graph mood <name>");
        sh.Print("  food       days of food");
        sh.Print("  wealth     colony wealth, whole game");
        sh.Print("  colonists  how many colonists, whole game");
        sh.Print("  threat     raid points, whole game");
        sh.Print("examples: graph temp 7 2, graph mood Tasca 3, graph wealth 30", Ink.Dim);
    }

    // the hours before the mod started recording stay empty
    private static int Since(Series s, int days) => Recorder.HourNow - days * 24;

    private static string Note(Series s, int from)
    {
        if (s.start <= from)
        {
            return null;
        }
        Map map = Commands.HomeMap;
        float longitude = map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;
        int hour = GenDate.HourOfDay(s.start * Recorder.TicksPerHour + Find.TickManager.gameStartAbsTick, longitude);
        return $"recording since d{s.start / 24 + 1} {hour}h";
    }

    private static string Short(float v)
    {
        float a = Mathf.Abs(v);
        return a >= 1000000f ? (v / 1000000f).ToString("0.#") + "M" : a >= 10000f ? (v / 1000f).ToString("0") + "k" : a >= 1000f ? (v / 1000f).ToString("0.#") + "k" : v.ToString("0");
    }

    private static Graph Mood(string title, Func<int, float> values, int from) => new Graph
    {
        title = title,
        valueAt = values,
        fromHour = from,
        fixedMin = 0f,
        fixedMax = 100f,
        format = v => v.ToString("0") + "%",
        inkFor = v => Commands.Level(v / 100f)
    };

    private static Graph Temperature(int back, int ahead)
    {
        Map map = Commands.HomeMap;
        Recorder rec = Recorder.Current;
        Series recorded = rec?.Get("temp");
        int now = Recorder.HourNow;
        int startAbs = Find.TickManager.gameStartAbsTick;
        PlanetTile tile = map.Tile;
        return new Graph
        {
            title = "temperature outside",
            // recorded when we have it, it has heat waves and cold snaps, the game's own formula for the rest
            valueAt = h =>
            {
                float v = recorded?.At(h) ?? float.NaN;
                return float.IsNaN(v) || h > now ? Find.World.tileTemperatures.OutdoorTemperatureAt(tile, h * Recorder.TicksPerHour + startAbs) : v;
            },
            fromHour = now - back * 24,
            toHour = now + 1 + ahead * 24,
            format = v => v.ToStringTemperature("F0"),
            inkFor = v => v >= 35f ? Ink.Bad : v >= 25f ? Ink.Warn : v <= 0f ? Ink.Accent : Ink.Normal
        };
    }
}
