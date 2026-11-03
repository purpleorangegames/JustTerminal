using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace JustTerminal;

// btop for the colony: graphs on top, power and weather on the right, colonists like processes below
public class TopApp : App
{
    private enum Sort
    {
        Name,
        Mood,
        Health,
        Rest,
        Food
    }

    private Sort sort = Sort.Mood;
    private bool reverse;
    private int selected;
    private int listTop;
    private Pawn detail;
    // where each colonist row was drawn, for clicks
    private readonly List<(int y, int x0, int x1, int index)> rowHits = new List<(int, int, int, int)>();
    private int powerTop;
    // 0 shows the four boxes, 1 to 4 one of them on the whole screen
    private int view;
    private static readonly string[] viewNames = { "all", "colony", "power", "colonists", "stock" };

    // ticks per second, measured
    private int lastTicks = -1;
    private float lastTime;
    private float tps;
    private float frameMs;

    public override bool HandleKey(Event e)
    {
        List<Pawn> pawns = Pawns();
        if (detail != null && view != 2 && (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow))
        {
            page = (Page)(((int)page + (e.keyCode == KeyCode.RightArrow ? 1 : 4)) % 5);
            return true;
        }
        // on the power screen the arrows scroll the building list
        if (view == 2)
        {
            switch (e.keyCode)
            {
                case KeyCode.UpArrow:
                    powerTop = Math.Max(0, powerTop - 1);
                    return true;
                case KeyCode.DownArrow:
                    powerTop++;
                    return true;
                case KeyCode.PageUp:
                    powerTop = Math.Max(0, powerTop - 10);
                    return true;
                case KeyCode.PageDown:
                    powerTop += 10;
                    return true;
                case KeyCode.Home:
                    powerTop = 0;
                    return true;
            }
        }
        switch (e.keyCode)
        {
            case KeyCode.Escape:
            case KeyCode.Backspace:
                if (detail != null)
                {
                    detail = null;
                    return true;
                }
                Done = true;
                return true;
            case KeyCode.Q:
                Done = true;
                return true;
            case KeyCode.UpArrow:
                selected = Math.Max(0, selected - 1);
                return true;
            case KeyCode.DownArrow:
                selected = Math.Min(pawns.Count - 1, selected + 1);
                return true;
            case KeyCode.Home:
                selected = 0;
                return true;
            case KeyCode.End:
                selected = pawns.Count - 1;
                return true;
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                if (selected >= 0 && selected < pawns.Count)
                {
                    detail = detail == pawns[selected] ? null : pawns[selected];
                }
                return true;
            case KeyCode.S:
                sort = (Sort)(((int)sort + 1) % 5);
                return true;
            case KeyCode.R:
                reverse = !reverse;
                return true;
            case KeyCode.Tab:
                view = (view + (e.shift ? 4 : 1)) % 5;
                return true;
            case KeyCode.Alpha0:
            case KeyCode.Alpha1:
            case KeyCode.Alpha2:
            case KeyCode.Alpha3:
            case KeyCode.Alpha4:
                view = e.keyCode - KeyCode.Alpha0;
                return true;
        }
        return false;
    }

    // clicking a colonist opens their details, clicking the same one again closes them
    public override void Click(int cx, int cy)
    {
        foreach ((int y, int x0, int x1, Page pg) in pageHits)
        {
            if (cy == y && cx >= x0 && cx < x1)
            {
                page = pg;
                return;
            }
        }
        foreach ((int y, int x0, int x1, int index) in rowHits)
        {
            if (cy != y || cx < x0 || cx >= x1)
            {
                continue;
            }
            List<Pawn> pawns = Pawns();
            if (index >= pawns.Count)
            {
                return;
            }
            selected = index;
            detail = detail == pawns[index] ? null : pawns[index];
            return;
        }
    }

    public override void Scroll(int rows)
    {
        if (view == 2)
        {
            powerTop = Math.Max(0, powerTop - rows);
            return;
        }
        selected -= Math.Sign(rows);
    }

    private List<Pawn> Pawns()
    {
        IEnumerable<Pawn> list = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep;
        Func<Pawn, float> key = sort switch
        {
            Sort.Mood => p => p.needs?.mood?.CurLevelPercentage ?? 0f,
            Sort.Health => p => p.health.summaryHealth.SummaryHealthPercent,
            Sort.Rest => p => p.needs?.rest?.CurLevelPercentage ?? 0f,
            Sort.Food => p => p.needs?.food?.CurLevelPercentage ?? 0f,
            _ => null
        };
        // bars sort lowest first, those need looking at
        list = key == null ? list.OrderBy(p => p.LabelShort) : list.OrderBy(key).ThenBy(p => p.LabelShort);
        List<Pawn> result = list.ToList();
        if (reverse)
        {
            result.Reverse();
        }
        return result;
    }

    public override void Draw(Screen s)
    {
        if (Current.ProgramState != ProgramState.Playing || Commands.HomeMap == null)
        {
            s.Write(1, 1, "no game running, q quits", Ink.Bad);
            return;
        }
        if (s.Cols < 70 || s.Rows < 20)
        {
            s.Write(1, 1, "make the window bigger, q quits", Ink.Warn);
            return;
        }
        Measure();
        rowHits.Clear();
        pageHits.Clear();
        int w = s.Cols, h = s.Rows;
        int rightW = Mathf.Clamp(w / 3, 32, 44);
        int leftW = w - rightW;
        int topH = Mathf.Clamp((h - 2) * 45 / 100, 9, 16);
        int bottomH = h - 2 - topH;

        Header(s, w);
        if (detail != null && (detail.Dead || detail.Destroyed))
        {
            detail = null;
        }
        int bodyH = h - 2;
        switch (view)
        {
            case 1:
                TopLeft(s, 0, 1, w, bodyH);
                break;
            case 2:
                PowerBox(s, 0, 1, w, bodyH);
                break;
            case 3:
                // a chosen colonist shows on the top half, the list below
                if (detail != null)
                {
                    int half = bodyH / 2;
                    PawnBox(s, 0, 1, w, half, detail);
                    PawnList(s, 0, 1 + half, w, bodyH - half);
                }
                else
                {
                    PawnList(s, 0, 1, w, bodyH);
                }
                break;
            case 4:
                StockBox(s, 0, 1, w, bodyH);
                break;
            default:
                TopLeft(s, 0, 1, leftW, topH);
                PowerBox(s, leftW, 1, rightW, topH);
                PawnList(s, 0, 1 + topH, leftW, bottomH);
                StockBox(s, leftW, 1 + topH, rightW, bottomH);
                break;
        }
        s.NoClip();
        Footer(s, w, h - 1);
    }

    // the colony graphs, or the chosen colonist's
    private void TopLeft(Screen s, int x, int y, int w, int h)
    {
        if (detail != null)
        {
            PawnBox(s, x, y, w, h, detail);
        }
        else
        {
            ColonyBox(s, x, y, w, h);
        }
    }

    private void Measure()
    {
        float t = Time.realtimeSinceStartup;
        int ticks = Find.TickManager.TicksGame;
        if (lastTicks < 0 || ticks < lastTicks)
        {
            lastTicks = ticks;
            lastTime = t;
        }
        else if (t - lastTime >= 1f)
        {
            tps = (ticks - lastTicks) / (t - lastTime);
            lastTicks = ticks;
            lastTime = t;
        }
        frameMs = Mathf.Lerp(frameMs, Time.unscaledDeltaTime * 1000f, frameMs == 0f ? 1f : 0.3f);
    }

    private static void Header(Screen s, int w)
    {
        Map map = Commands.HomeMap;
        Vector2 longLat = Find.WorldGrid.LongLatOf(map.Tile);
        long ticks = Find.TickManager.TicksAbs;
        int x = s.Write(1, 0, "rimtop", Ink.Accent);
        x = s.Write(x + 2, 0, GenDate.DateFullStringAt(ticks, longLat), Ink.Normal);
        x = s.Write(x + 1, 0, GenDate.HourOfDay(ticks, longLat.x) + "h", Ink.Bright);
        TimeSpeed speed = Find.TickManager.CurTimeSpeed;
        string speedText = speed == TimeSpeed.Paused ? "paused" : speed.ToString().ToLowerInvariant();
        x = s.Write(x + 2, 0, speedText, speed == TimeSpeed.Paused ? Ink.Warn : Ink.Dim);
        string colony = Faction.OfPlayer.Name;
        if (w - colony.Length - 1 > x + 2)
        {
            s.Write(w - colony.Length - 1, 0, colony, Ink.Bright);
        }
    }

    private void Footer(Screen s, int w, int y)
    {
        string perf = $"{tps:0} ticks/s  {frameMs:0.0} ms";
        int perfX = w - perf.Length - 1;
        s.Write(perfX, y, perf, Ink.Dim);
        // quit always shows, the other hints only while they fit before it
        int quitX = perfX - 2 - "q quit".Length;
        int x = 1;
        foreach ((string key, string label) in new[] { ("tab", viewNames[(view + 1) % 5]), ("↑↓", view == 2 ? "scroll" : "select"), detail != null && view != 2 ? ("←→", "page") : ("enter", "details"), ("s", "sort"), ("r", "reverse") })
        {
            if (x + key.Length + 1 + label.Length > quitX)
            {
                break;
            }
            x = s.Write(x, y, key, Ink.Bright);
            x = s.Write(x + 1, y, label, Ink.Dim) + 2;
        }
        x = s.Write(x, y, "q", Ink.Bright);
        s.Write(x + 1, y, "quit", Ink.Dim);
    }

    // one labelled graph inside a box, label and value on the left
    private static void GraphRow(Screen s, int x, int y, int w, int h, string label, string value, Ink valueInk, Graph g)
    {
        s.Write(x, y, label, Ink.Dim);
        if (h > 1)
        {
            s.Write(x, y + 1, value, valueInk);
        }
        else
        {
            s.Write(x + label.Length + 1, y, value, valueInk);
        }
        g.Plot(s, x + 12, y, w - 12, h);
    }

    private static void ColonyBox(Screen s, int x, int y, int w, int h)
    {
        App.Box(s, x, y, w, h, "colony");
        int now = Recorder.HourNow;
        int inner = h - 2;
        int rows = Math.Max(1, inner / 3);
        int extra = inner - rows * 3;
        Recorder rec = Recorder.Current;

        Series moodRec = rec?.Get("mood");
        Func<int, float> moodGame = Graph.FromGameHistory("ColonistMood");
        Graph mood = new Graph
        {
            valueAt = hr => moodRec != null && !float.IsNaN(moodRec.At(hr)) ? moodRec.At(hr) : moodGame(hr),
            fromHour = Math.Max(0, now - 72),
            toHour = now + 1,
            fixedMin = 0f,
            fixedMax = 100f,
            inkFor = v => Commands.Level(v / 100f)
        };
        float moodNow = mood.valueAt(now);
        GraphRow(s, x + 2, y + 1, w - 4, rows, "mood 3d", float.IsNaN(moodNow) ? "-" : moodNow.ToString("0") + "%", Commands.Level(moodNow / 100f), mood);

        Map map = Commands.HomeMap;
        Series tempRec = rec?.Get("temp");
        int startAbs = Find.TickManager.gameStartAbsTick;
        Graph temp = new Graph
        {
            valueAt = hr =>
            {
                float v = tempRec?.At(hr) ?? float.NaN;
                return float.IsNaN(v) || hr > now ? Find.World.tileTemperatures.OutdoorTemperatureAt(map.Tile, hr * Recorder.TicksPerHour + startAbs) : v;
            },
            fromHour = Math.Max(0, now - 48),
            toHour = now + 25,
            inkFor = v => v >= 35f ? Ink.Bad : v >= 25f ? Ink.Warn : v <= 0f ? Ink.Accent : Ink.Normal
        };
        float tempNow = map.mapTemperature.OutdoorTemp;
        GraphRow(s, x + 2, y + 1 + rows, w - 4, rows, "temp 2d+1", tempNow.ToStringTemperature("F0"), temp.inkFor(tempNow), temp);

        Graph wealth = new Graph
        {
            valueAt = Graph.FromGameHistory("Wealth_Total"),
            fromHour = 0,
            toHour = now + 1,
            fixedMin = 0f,
            inkFor = _ => Ink.Normal
        };
        GraphRow(s, x + 2, y + 1 + rows * 2, w - 4, rows + extra, "wealth", Short(map.wealthWatcher.WealthTotal), Ink.Bright, wealth);
    }

    private enum Page
    {
        Mood,
        Health,
        Skills,
        Gear,
        Work
    }

    private Page page;
    private readonly List<(int y, int x0, int x1, Page page)> pageHits = new List<(int, int, int, Page)>();

    // the chosen colonist, page names along the top border, left and right or a click changes the page
    private void PawnBox(Screen s, int x, int y, int w, int h, Pawn p)
    {
        App.Box(s, x, y, w, h, p.LabelShort);
        s.NoClip();
        int tx = x + 2 + Screen.Plain(p.LabelShort).Length + 3;
        Page[] pages = (Page[])Enum.GetValues(typeof(Page));
        int need = pages.Sum(pg => pg.ToString().Length + 2);
        if (tx + need < x + w - 2)
        {
            foreach (Page pg in pages)
            {
                string name = " " + pg.ToString().ToLowerInvariant() + " ";
                bool on = pg == page;
                pageHits.Add((y, tx, tx + name.Length, pg));
                tx = s.Write(tx, y, name, on ? Ink.Screen : Ink.Dim, on ? Ink.Normal : Ink.None);
            }
        }
        else
        {
            s.Write(tx, y, " ‹ " + page.ToString().ToLowerInvariant() + " › ", Ink.Bright);
        }
        s.Clip(x + 1, y + 1, w - 2, h - 2);
        switch (page)
        {
            case Page.Health:
                HealthPage(s, x + 2, y + 1, w - 4, h - 2, p);
                break;
            case Page.Skills:
                SkillsPage(s, x + 2, y + 1, w - 4, h - 2, p);
                break;
            case Page.Gear:
                GearPage(s, x + 2, y + 1, w - 4, h - 2, p);
                break;
            case Page.Work:
                WorkPage(s, x + 2, y + 1, w - 4, h - 2, p);
                break;
            default:
                MoodPage(s, x, y, w, h, p);
                break;
        }
    }

    // label, bar and percent in a grid of columns, returns the row after it
    private static int Grid(Screen s, int x, int y, int w, IList<(string label, float value, Ink ink, string text)> items, int labelW, int barW)
    {
        int colW = labelW + barW + 7;
        int perRow = Math.Max(1, w / colW);
        for (int i = 0; i < items.Count; i++)
        {
            int cx = x + i % perRow * colW;
            int ry = y + i / perRow;
            (string label, float value, Ink ink, string text) = items[i];
            s.Write(cx, ry, App.Clip(label, labelW - 1), Ink.Dim);
            int bx = cx + labelW;
            if (value >= 0f)
            {
                bx = App.Bar(s, bx, ry, value, barW, ink);
            }
            s.Write(bx + 1, ry, text, value >= 0f ? Ink.Normal : ink);
        }
        return y + (items.Count + perRow - 1) / perRow;
    }

    private static void HealthPage(Screen s, int x, int y, int w, int h, Pawn p)
    {
        HediffSet set = p.health.hediffSet;
        int cx = s.Write(x, y, "health ", Ink.Dim);
        float health = p.health.summaryHealth.SummaryHealthPercent;
        cx = s.Write(cx, y, health.ToStringPercent(), Commands.Level(health)) + 2;
        cx = s.Write(cx, y, "pain ", Ink.Dim);
        cx = s.Write(cx, y, set.PainTotal.ToStringPercent(), set.PainTotal > 0.4f ? Ink.Bad : set.PainTotal > 0.15f ? Ink.Warn : Ink.Normal) + 2;
        float bleed = set.BleedRateTotal;
        if (bleed > 0f)
        {
            cx = s.Write(cx, y, "bleeding ", Ink.Dim);
            cx = s.Write(cx, y, bleed.ToStringPercent() + "/day", Ink.Bad) + 2;
        }
        if (p.health.HasHediffsNeedingTend())
        {
            s.Write(cx, y, "needs tending", Ink.Warn);
        }
        List<(string, float, Ink, string)> caps = new List<(string, float, Ink, string)>();
        foreach (PawnCapacityDef def in DefDatabase<PawnCapacityDef>.AllDefs.Where(d => d.showOnHumanlikes).OrderBy(d => d.listOrder))
        {
            float level = p.health.capacities.GetLevel(def);
            caps.Add((def.label, Mathf.Clamp01(level), Commands.Level(level), level.ToStringPercent()));
        }
        int row = Grid(s, x, y + 2, w, caps, 16, 6) + 1;
        foreach (Hediff hediff in set.hediffs.Where(hd => hd.Visible))
        {
            if (row >= y + h)
            {
                break;
            }
            string part = hediff.Part != null ? " · " + hediff.Part.Label : "";
            s.Write(x, row, App.Clip("• " + hediff.LabelCap + part, w), hediff.Bleeding ? Ink.Bad : hediff.def.isBad ? Ink.Warn : Ink.Normal);
            row++;
        }
    }

    private static void SkillsPage(Screen s, int x, int y, int w, int h, Pawn p)
    {
        if (p.skills == null)
        {
            s.Write(x, y, "no skills", Ink.Dim);
            return;
        }
        List<(string, float, Ink, string)> items = new List<(string, float, Ink, string)>();
        foreach (SkillRecord sk in p.skills.skills)
        {
            if (sk.TotallyDisabled)
            {
                items.Add((sk.def.skillLabel.CapitalizeFirst(), -1f, Ink.Dim, "-"));
                continue;
            }
            string passion = sk.passion == Passion.Major ? " ♥♥" : sk.passion == Passion.Minor ? " ♥" : "";
            items.Add((sk.def.skillLabel.CapitalizeFirst(), sk.Level / 20f, sk.passion != Passion.None ? Ink.Warn : Ink.Normal, sk.Level + passion));
        }
        Grid(s, x, y + 1, w, items, 14, 10);
    }

    private static void GearPage(Screen s, int x, int y, int w, int h, Pawn p)
    {
        int row = y;
        ThingWithComps weapon = p.equipment?.Primary;
        s.Write(x, row, "weapon  ", Ink.Dim);
        s.Write(x + 8, row, weapon != null ? App.Clip(weapon.LabelCap, w - 8) : "none", Ink.Bright);
        row += 2;
        int half = w >= 70 ? w / 2 : w;
        int top = row;
        s.Write(x, row++, "APPAREL", Ink.Dim);
        foreach (Apparel ap in p.apparel?.WornApparel ?? new List<Apparel>())
        {
            float hp = ap.MaxHitPoints > 0 ? ap.HitPoints / (float)ap.MaxHitPoints : 1f;
            string label = ap.LabelNoParenthesisCap + (ap.TryGetQuality(out QualityCategory q) ? " · " + q.GetLabel() : "");
            s.Write(x, row, App.Clip(label, half - 7), Ink.Normal);
            s.Write(x + half - 6, row, hp.ToStringPercent().PadLeft(4), Commands.Level(hp));
            row++;
        }
        // inventory beside the apparel when there's room, below it when not
        int ix = half < w ? x + half + 1 : x;
        int iy = half < w ? top : row + 1;
        s.Write(ix, iy++, "CARRYING", Ink.Dim);
        List<Thing> things = p.inventory?.innerContainer.ToList() ?? new List<Thing>();
        if (p.carryTracker?.CarriedThing != null)
        {
            things.Insert(0, p.carryTracker.CarriedThing);
        }
        if (things.Count == 0)
        {
            s.Write(ix, iy, "nothing", Ink.Dim);
        }
        foreach (Thing t in things)
        {
            s.Write(ix, iy++, App.Clip(t.LabelCap, (half < w ? w - half - 1 : w)), Ink.Normal);
        }
    }

    private static void WorkPage(Screen s, int x, int y, int w, int h, Pawn p)
    {
        if (p.workSettings == null || !p.workSettings.EverWork)
        {
            s.Write(x, y, "doesn't work", Ink.Dim);
            return;
        }
        bool manual = Find.PlaySettings.useWorkPriorities;
        List<WorkTypeDef> works = DefDatabase<WorkTypeDef>.AllDefs.Where(wt => wt.visible).OrderByDescending(wt => wt.naturalPriority).ToList();
        int colW = 18;
        int perRow = Math.Max(1, w / colW);
        for (int i = 0; i < works.Count; i++)
        {
            WorkTypeDef wt = works[i];
            int cx = x + i % perRow * colW;
            int ry = y + 1 + i / perRow;
            bool disabled = p.WorkTypeIsDisabled(wt);
            int pr = disabled ? 0 : p.workSettings.GetPriority(wt);
            string value = disabled ? "-" : pr == 0 ? "·" : manual ? pr.ToString() : "✓";
            s.Write(cx, ry, App.Clip(wt.labelShort, 13), disabled || pr == 0 ? Ink.Dim : Ink.Normal);
            s.Write(cx + 14, ry, value, disabled || pr == 0 ? Ink.Dim : pr == 1 ? Ink.Bright : Ink.Normal);
        }
        s.Write(x, y + 2 + (works.Count + perRow - 1) / perRow, "change these in edit mode: work <name> <work> <0-4>", Ink.Dim);
    }

    private static void MoodPage(Screen s, int x, int y, int w, int h, Pawn p)
    {
        int now = Recorder.HourNow;
        Series rec = Recorder.Current?.Get("mood:" + p.GetUniqueLoadID());
        List<(string label, float value)> needs = new List<(string, float)> { ("health", p.health.summaryHealth.SummaryHealthPercent) };
        foreach ((string label, Need need) in new (string, Need)[] { ("food", p.needs?.food), ("rest", p.needs?.rest), ("joy", p.needs?.joy) })
        {
            if (need != null)
            {
                needs.Add((label, need.CurLevelPercentage));
            }
        }
        // the needs wrap to more lines when the box is narrow
        const int barW = 6;
        int itemW = 6 + 1 + barW + 6;
        int perLine = Math.Max(1, (w - 4) / itemW);
        int needLines = (needs.Count + perLine - 1) / perLine;
        int graphH = Math.Max(2, h - 2 - needLines - 1);
        float mood = (p.needs?.mood?.CurLevelPercentage ?? 0f) * 100f;
        Graph g = new Graph
        {
            valueAt = hr => rec?.At(hr) ?? float.NaN,
            fromHour = Math.Max(0, now - 72),
            toHour = now + 1,
            fixedMin = 0f,
            fixedMax = 100f,
            inkFor = v => Commands.Level(v / 100f)
        };
        GraphRow(s, x + 2, y + 1, w - 4, graphH, "mood 3d", mood.ToString("0") + "%", Commands.Level(mood / 100f), g);
        if (rec == null || rec.start > now - 72)
        {
            s.Write(x + 2, y + 3, "recorded", Ink.Dim);
            s.Write(x + 2, y + 4, "hourly", Ink.Dim);
        }
        int row = y + 1 + graphH;
        for (int i = 0; i < needs.Count; i++)
        {
            int cx = x + 2 + i % perLine * itemW;
            int ry = row + i / perLine;
            (string label, float value) = needs[i];
            s.Write(cx, ry, label, Ink.Dim);
            int bx = App.Bar(s, cx + 7, ry, value, barW, Commands.Level(value));
            s.Write(bx + 1, ry, value.ToStringPercent(), Ink.Normal);
        }
        row += needLines - 1;
        string doing = Doing(p);
        s.Write(x + 2, row + 1, "doing ", Ink.Dim);
        s.Write(x + 8, row + 1, App.Clip(doing, w - 10), p.InMentalState ? Ink.Bad : Ink.Normal);
    }

    // weather first, then the power numbers, then every building using power, the hungriest on top
    private void PowerBox(Screen s, int x, int y, int w, int h)
    {
        App.Box(s, x, y, w, h, "weather · power");
        Map map = Commands.HomeMap;
        int bottom = y + h - 1;
        int row = y + 1;
        s.Write(x + 2, row, App.Clip(map.weatherManager.curWeather.LabelCap + ", " + map.mapTemperature.OutdoorTemp.ToStringTemperature("F0"), w - 4), Ink.Bright);
        row++;
        foreach (GameCondition condition in map.gameConditionManager.ActiveConditions)
        {
            s.Write(x + 2, row, App.Clip(condition.LabelCap, w - 4), Ink.Warn);
            row++;
        }
        row++;

        float made = 0f, used = 0f, stored = 0f, max = 0f;
        List<(CompPowerTrader comp, float watts)> users = new List<(CompPowerTrader, float)>();
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
                else if (comp.PowerOutput < 0f)
                {
                    used -= comp.PowerOutput;
                    users.Add((comp, -comp.PowerOutput));
                }
            }
            foreach (CompPowerBattery battery in net.batteryComps)
            {
                stored += battery.StoredEnergy;
                max += battery.Props.storedEnergyMax;
            }
        }
        int bw = Math.Max(4, w - 4 - 16);
        s.Write(x + 2, row, "made", Ink.Dim);
        s.Write(x + 9, row, $"{made:0} W".PadLeft(8), Ink.Bright);
        row++;
        s.Write(x + 2, row, "used", Ink.Dim);
        s.Write(x + 9, row, $"{used:0} W".PadLeft(8), used > made && stored <= 0f ? Ink.Bad : Ink.Normal);
        App.Bar(s, x + 18, row, made > 0f ? used / made : 1f, bw, used > made ? Ink.Bad : used > made * 0.8f ? Ink.Warn : Ink.Good);
        row++;
        if (max > 0f)
        {
            s.Write(x + 2, row, "battery", Ink.Dim);
            s.Write(x + 9, row, $"{stored:0} Wd".PadLeft(8), Ink.Normal);
            App.Bar(s, x + 18, row, stored / max, bw, Commands.Level(stored / max));
            row++;
        }
        row++;
        if (users.Count == 0 || row >= bottom - 1)
        {
            return;
        }

        users.Sort((a, b) => b.watts.CompareTo(a.watts));
        float most = users[0].watts;
        int right = x + w - 2;
        int barW = w >= 60 ? 12 : 0;
        int wattsX = right - 8;
        int barX = wattsX - barW - 1;
        int nameEnd = (barW > 0 ? barX : wattsX) - 1;
        s.Write(x + 2, row, "USING POWER", Ink.Dim);
        s.Write(wattsX, row, "W".PadLeft(8), Ink.Dim);
        row++;
        int rows = bottom - row;
        powerTop = Mathf.Clamp(powerTop, 0, Math.Max(0, users.Count - rows));
        for (int i = powerTop; i < users.Count && row < bottom; i++, row++)
        {
            (CompPowerTrader comp, float watts) = users[i];
            Thing t = comp.parent;
            int nx = s.Write(x + 2, row, App.Clip(t.LabelCapNoCount, nameEnd - x - 2), Ink.Normal);
            // the room it's in, when there's room for it
            Room room = t.GetRoom();
            string where = room != null && !room.PsychologicallyOutdoors && room.Role != null ? " · " + room.GetRoomRoleLabel() : null;
            if (where != null && nx + where.Length <= nameEnd)
            {
                s.Write(nx, row, where, Ink.Dim);
            }
            if (barW > 0)
            {
                App.Bar(s, barX, row, watts / most, barW, watts >= most * 0.5f ? Ink.Warn : Ink.Normal);
            }
            s.Write(wattsX, row, watts.ToString("0").PadLeft(8), Ink.Bright);
        }
        if (users.Count > rows)
        {
            s.NoClip();
            string more = $" {powerTop + 1}-{Math.Min(users.Count, powerTop + rows)} of {users.Count} ";
            s.Write(x + w - more.Length - 2, bottom, more, Ink.Dim);
        }
    }

    private static readonly (string label, Func<ThingDef> def, float good)[] stock =
    {
        ("silver", () => ThingDefOf.Silver, 2000f),
        ("steel", () => ThingDefOf.Steel, 1000f),
        ("wood", () => ThingDefOf.WoodLog, 600f),
        ("comps", () => ThingDefOf.ComponentIndustrial, 30f),
        ("plasteel", () => ThingDefOf.Plasteel, 200f),
        ("herbal", () => ThingDefOf.MedicineHerbal, 20f),
        ("meds", () => ThingDefOf.MedicineIndustrial, 20f)
    };

    private static void StockBox(Screen s, int x, int y, int w, int h)
    {
        App.Box(s, x, y, w, h, "stock");
        Map map = Commands.HomeMap;
        int bw = w - 4 - 18;
        int row = y + 1;
        int eaters = map.mapPawns.FreeColonistsSpawnedCount + map.mapPawns.PrisonersOfColonySpawnedCount;
        float days = eaters > 0 ? map.resourceCounter.TotalHumanEdibleNutrition / eaters : 0f;
        s.Write(x + 2, row, "food", Ink.Dim);
        s.Write(x + 10, row, (days.ToString("0.#") + "d").PadLeft(7), Ink.Bright);
        App.Bar(s, x + 18, row, days / 15f, bw, days < 2f ? Ink.Bad : days < 5f ? Ink.Warn : Ink.Good);
        row++;
        foreach ((string label, Func<ThingDef> def, float good) in stock)
        {
            if (row >= y + h - 1)
            {
                break;
            }
            int count = map.resourceCounter.GetCount(def());
            s.Write(x + 2, row, label, Ink.Dim);
            s.Write(x + 10, row, Short(count).PadLeft(7), Ink.Normal);
            App.Bar(s, x + 18, row, count / good, bw, Ink.Normal);
            row++;
        }
        row++;
        if (row < y + h - 1)
        {
            s.Write(x + 2, row, "prisoners", Ink.Dim);
            s.Write(x + 13, row, map.mapPawns.PrisonersOfColonyCount.ToString(), Ink.Normal);
            row++;
        }
        if (row < y + h - 1)
        {
            s.Write(x + 2, row, "animals", Ink.Dim);
            s.Write(x + 13, row, map.mapPawns.SpawnedColonyAnimals.Count.ToString(), Ink.Normal);
        }
    }

    private void PawnList(Screen s, int x, int y, int w, int h)
    {
        List<Pawn> pawns = Pawns();
        App.Box(s, x, y, w, h, $"colonists {pawns.Count} · sort {sort.ToString().ToLowerInvariant()}{(reverse ? " ↑" : "")}");
        selected = Mathf.Clamp(selected, 0, Math.Max(0, pawns.Count - 1));
        int rows = h - 3;
        if (selected < listTop)
        {
            listTop = selected;
        }
        if (selected >= listTop + rows)
        {
            listTop = selected - rows + 1;
        }
        listTop = Mathf.Clamp(listTop, 0, Math.Max(0, pawns.Count - rows));

        // columns that don't fit are left out, doing keeps at least 12
        int bar = 6;
        int right = x + w - 1;
        int cName = x + 2, cMood = cName + 13;
        int next = cMood + bar + 6;
        int cHealth = -1, cRest = -1, cFood = -1;
        if (next + bar + 2 <= right)
        {
            cHealth = next;
            next += bar + 2;
        }
        if (next + bar + 2 + 12 <= right)
        {
            cRest = next;
            next += bar + 2;
        }
        if (next + bar + 2 + 12 <= right)
        {
            cFood = next;
            next += bar + 2;
        }
        int cDoing = next + 12 <= right ? next : -1;
        int y0 = y + 1;
        Ink head = Ink.Dim;
        s.Write(cName, y0, "NAME", sort == Sort.Name ? Ink.Bright : head);
        s.Write(cMood, y0, "MOOD", sort == Sort.Mood ? Ink.Bright : head);
        if (cHealth >= 0)
        {
            s.Write(cHealth, y0, "HEALTH", sort == Sort.Health ? Ink.Bright : head);
        }
        if (cRest >= 0)
        {
            s.Write(cRest, y0, "REST", sort == Sort.Rest ? Ink.Bright : head);
        }
        if (cFood >= 0)
        {
            s.Write(cFood, y0, "FOOD", sort == Sort.Food ? Ink.Bright : head);
        }
        if (cDoing >= 0)
        {
            s.Write(cDoing, y0, "DOING", head);
        }
        for (int i = 0; i < rows && listTop + i < pawns.Count; i++)
        {
            Pawn p = pawns[listTop + i];
            int ry = y0 + 1 + i;
            rowHits.Add((ry, x + 1, x + w - 1, listTop + i));
            bool sel = listTop + i == selected;
            if (sel)
            {
                for (int cx = x + 1; cx < x + w - 1; cx++)
                {
                    s.Put(cx, ry, ' ', Ink.Normal, Ink.Dim);
                }
            }
            Ink bg = sel ? Ink.Dim : Ink.None;
            Ink text = sel ? Ink.Bright : Ink.Normal;
            Put(s, cName, ry, App.Clip(p.LabelShort, 12), text, bg);
            float mood = p.needs?.mood?.CurLevelPercentage ?? 0f;
            BarAt(s, cMood, ry, mood, bar, bg);
            bool breaking = p.mindState?.mentalBreaker != null && p.needs?.mood != null && p.needs.mood.CurLevel < p.mindState.mentalBreaker.BreakThresholdMinor;
            Put(s, cMood + bar + 1, ry, (mood * 100f).ToString("0").PadLeft(3), breaking ? Ink.Bad : text, bg);
            if (breaking)
            {
                Put(s, cMood + bar + 4, ry, "!", Ink.Bad, bg);
            }
            if (cHealth >= 0)
            {
                BarAt(s, cHealth, ry, p.health.summaryHealth.SummaryHealthPercent, bar, bg);
            }
            if (cRest >= 0)
            {
                BarAt(s, cRest, ry, p.needs?.rest?.CurLevelPercentage ?? 0f, bar, bg);
            }
            if (cFood >= 0)
            {
                BarAt(s, cFood, ry, p.needs?.food?.CurLevelPercentage ?? 0f, bar, bg);
            }
            if (cDoing >= 0)
            {
                Put(s, cDoing, ry, App.Clip(Doing(p), right - 1 - cDoing), p.InMentalState ? Ink.Bad : text, bg);
            }
        }
        if (pawns.Count > rows)
        {
            s.NoClip();
            s.Write(x + w - 10, y + h - 1, $" {selected + 1}/{pawns.Count} ", Ink.Dim);
        }
    }

    private static void Put(Screen s, int x, int y, string text, Ink ink, Ink bg)
    {
        s.Write(x, y, text, ink, bg);
    }

    private static void BarAt(Screen s, int x, int y, float value, int width, Ink bg)
    {
        App.Bar(s, x, y, value, width, Commands.Level(value), bg);
    }

    private static string Doing(Pawn p)
    {
        if (p.InMentalState)
        {
            return p.MentalStateDef.label;
        }
        if (p.Map == null)
        {
            return "away";
        }
        string report = p.jobs?.curDriver?.GetReport();
        return string.IsNullOrEmpty(report) ? "idle" : report.CapitalizeFirst();
    }

    private static string Short(float v)
    {
        float a = Mathf.Abs(v);
        return a >= 1000000f ? (v / 1000000f).ToString("0.#") + "M" : a >= 10000f ? (v / 1000f).ToString("0") + "k" : a >= 1000f ? (v / 1000f).ToString("0.#") + "k" : v.ToString("0");
    }
}
