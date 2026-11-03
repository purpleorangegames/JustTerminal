using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace JustTerminal;

// area graph made of braille dots, 2 columns and 4 rows of dots per character
public class Graph
{
    public string title;
    // value at an hour of the game, NaN when unknown
    public Func<int, float> valueAt;
    public int fromHour;
    public int toHour;
    public Func<float, string> format = v => v.ToString("0.#");
    public Func<float, Ink> inkFor = _ => Ink.Normal;
    public float? fixedMin;
    public float? fixedMax;
    public int height = 10;
    public string note;

    private static readonly int[] leftBits = { 0x40, 0x04, 0x02, 0x01 };
    private static readonly int[] rightBits = { 0x80, 0x20, 0x10, 0x08 };

    // one value per dot column, the average of the hours it covers
    public float[] Sample(int dots, out float min, out float max, out float sum, out int count)
    {
        int hours = Math.Max(1, toHour - fromHour);
        float[] points = new float[dots];
        min = float.MaxValue;
        max = float.MinValue;
        sum = 0f;
        count = 0;
        for (int i = 0; i < dots; i++)
        {
            int a = fromHour + (int)((long)hours * i / dots);
            int b = Math.Max(a + 1, fromHour + (int)((long)hours * (i + 1) / dots));
            float total = 0f;
            int n = 0;
            for (int h = a; h < b; h++)
            {
                float v = valueAt(h);
                if (!float.IsNaN(v))
                {
                    total += v;
                    n++;
                }
            }
            points[i] = n > 0 ? total / n : float.NaN;
            if (n > 0)
            {
                min = Mathf.Min(min, points[i]);
                max = Mathf.Max(max, points[i]);
                sum += points[i];
                count++;
            }
        }
        return points;
    }

    // the range drawn, fixed ends or the data with a little room
    public void Range(float min, float max, out float low, out float high)
    {
        low = fixedMin ?? min;
        high = fixedMax ?? max;
        if (fixedMin == null && fixedMax == null || high - low < 0.0001f)
        {
            float pad = Mathf.Max((high - low) * 0.08f, 0.5f);
            low = fixedMin ?? low - pad;
            high = fixedMax ?? high + pad;
        }
    }

    // braille character for one cell, fill = dots filled from the bottom of the whole graph for each half
    public static char Braille(int leftFill, int rightFill, int rowFromBottom)
    {
        int bits = 0;
        int l = Mathf.Clamp(leftFill - rowFromBottom * 4, 0, 4);
        int r = Mathf.Clamp(rightFill - rowFromBottom * 4, 0, 4);
        for (int d = 0; d < l; d++)
        {
            bits |= leftBits[d];
        }
        for (int d = 0; d < r; d++)
        {
            bits |= rightBits[d];
        }
        return bits == 0 ? ' ' : (char)(0x2800 + bits);
    }

    // plots straight into the screen grid, no labels
    public void Plot(Screen s, int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0)
        {
            return;
        }
        int dots = w * 2;
        float[] points = Sample(dots, out float min, out float max, out float _, out int count);
        if (count == 0)
        {
            return;
        }
        Range(min, max, out float low, out float high);
        int levels = h * 4;
        int now = Recorder.HourNow;
        int hours = Math.Max(1, toHour - fromHour);
        for (int c = 0; c < w; c++)
        {
            float a = points[c * 2], b = points[c * 2 + 1];
            int fa = float.IsNaN(a) ? 0 : Mathf.Clamp(Mathf.RoundToInt((a - low) / (high - low) * levels), 1, levels);
            int fb = float.IsNaN(b) ? 0 : Mathf.Clamp(Mathf.RoundToInt((b - low) / (high - low) * levels), 1, levels);
            float value = float.IsNaN(a) ? b : float.IsNaN(b) ? a : (a + b) / 2f;
            int hour = fromHour + (int)((long)hours * (c * 2 + 1) / dots);
            Ink ink = hour > now ? Ink.Dim : float.IsNaN(value) ? Ink.Normal : inkFor(value);
            for (int r = 0; r < h; r++)
            {
                char ch = Braille(fa, fb, h - 1 - r);
                if (ch != ' ')
                {
                    s.Put(x + c, y + r, ch, ink);
                }
            }
        }
    }

    public void Draw(Shell sh)
    {
        int now = Recorder.HourNow;
        int labelWidth = 8;
        int cols = Math.Max(10, sh.Cols - labelWidth - 2);
        int dots = cols * 2;
        int hours = Math.Max(1, toHour - fromHour);

        float[] points = Sample(dots, out float min, out float max, out float sum, out int count);
        if (count == 0)
        {
            sh.Print(title + ": nothing recorded yet", Ink.Dim);
            return;
        }

        float nowValue = valueAt(Math.Min(now, toHour - 1));
        sh.Out(title, Ink.Bright);
        if (!float.IsNaN(nowValue) && now < toHour)
        {
            sh.Out("  now ", Ink.Dim);
            sh.Out(format(nowValue), inkFor(nowValue));
        }
        sh.Out("  min ", Ink.Dim);
        sh.Out(format(min));
        sh.Out("  max ", Ink.Dim);
        sh.Out(format(max));
        sh.Out("  avg ", Ink.Dim);
        sh.Print(format(sum / count));
        if (note != null)
        {
            sh.Print(note, Ink.Dim);
        }

        Range(min, max, out float low, out float high);
        int levels = height * 4;

        int[] filled = new int[dots];
        for (int i = 0; i < dots; i++)
        {
            filled[i] = float.IsNaN(points[i]) ? -1 : Mathf.Clamp(Mathf.RoundToInt((points[i] - low) / (high - low) * levels), 1, levels);
        }
        StringBuilder text = new StringBuilder();
        for (int r = 0; r < height; r++)
        {
            int fromBottom = height - 1 - r;
            string label = r == 0 ? format(high) : r == height - 1 ? format(low) : r == height / 2 ? format((high + low) / 2f) : "";
            sh.Out(label.PadLeft(labelWidth - 1) + " ", Ink.Dim);
            sh.Out(r == 0 || r == height - 1 || r == height / 2 ? "┤" : "│", Ink.Dim);
            Ink runInk = Ink.Normal;
            text.Clear();
            for (int c = 0; c < cols; c++)
            {
                char ch = Braille(filled[c * 2], filled[c * 2 + 1], fromBottom);
                // hours ahead of now are a forecast, drawn dim
                int hour = fromHour + (int)((long)hours * (c * 2 + 1) / dots);
                float a = points[c * 2], b = points[c * 2 + 1];
                float value = float.IsNaN(a) ? b : float.IsNaN(b) ? a : (a + b) / 2f;
                Ink ink = hour > now ? Ink.Dim : float.IsNaN(value) ? Ink.Normal : inkFor(value);
                if (ink != runInk && text.Length > 0)
                {
                    sh.Out(text.ToString(), runInk);
                    text.Clear();
                }
                runInk = ink;
                text.Append(ch);
            }
            sh.Out(text.ToString(), runInk);
            sh.NewLine();
        }
        Axis(sh, labelWidth, cols, hours, now);
    }

    // line under the graph with a mark and the day at each day start
    private void Axis(Shell sh, int labelWidth, int cols, int hours, int now)
    {
        char[] line = new string('─', cols).ToCharArray();
        char[] labels = new string(' ', cols).ToCharArray();
        int days = hours / 24 + 1;
        int every = Math.Max(1, (int)Math.Ceiling(days * 8f / cols));
        int firstDay = (fromHour + 23) / 24;
        for (int day = firstDay; day * 24 < toHour; day++)
        {
            int c = (int)((long)(day * 24 - fromHour) * cols / hours);
            if (c < 0 || c >= cols)
            {
                continue;
            }
            if ((day - firstDay) % every == 0)
            {
                line[c] = '┴';
                string text = "d" + (day + 1);
                // the last one moves left so it doesn't wrap
                int at = Math.Min(c, cols - text.Length);
                for (int i = 0; i < text.Length; i++)
                {
                    labels[at + i] = text[i];
                }
            }
        }
        if (now > fromHour && now < toHour)
        {
            int c = (int)((long)(now - fromHour) * cols / hours);
            line[Math.Min(c, cols - 1)] = '▲';
        }
        sh.Out(new string(' ', labelWidth) + "└", Ink.Dim);
        sh.Print(new string(line), Ink.Dim);
        sh.Print(new string(' ', labelWidth + 1) + new string(labels).TrimEnd(), Ink.Dim);
    }

    // value from one of the game's own history records, kept every half day or day
    public static Func<int, float> FromGameHistory(string defName)
    {
        HistoryAutoRecorder recorder = null;
        foreach (HistoryAutoRecorderGroup group in Find.History.Groups())
        {
            foreach (HistoryAutoRecorder r in group.recorders)
            {
                if (r.def.defName == defName)
                {
                    recorder = r;
                }
            }
        }
        if (recorder == null || recorder.records.Count == 0)
        {
            return _ => float.NaN;
        }
        List<float> records = recorder.records;
        float hoursPerRecord = recorder.def.recordTicksFrequency / (float)Recorder.TicksPerHour;
        int now = Recorder.HourNow;
        return h =>
        {
            if (h < 0 || h > now)
            {
                return float.NaN;
            }
            // straight line between two records
            float at = h / hoursPerRecord;
            int i = Mathf.Clamp((int)at, 0, records.Count - 1);
            int j = Mathf.Min(i + 1, records.Count - 1);
            return Mathf.Lerp(records[i], records[j], at - (int)at);
        };
    }
}
