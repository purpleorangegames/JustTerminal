using System;
using UnityEngine;

namespace JustTerminal;

// runs a command again every few seconds, like watch
public class WatchApp : App
{
    private readonly string command;
    private readonly float seconds;
    private readonly Shell inner = new Shell { CanAsk = false };
    private float next;
    private int runs;

    public WatchApp(string command, float seconds)
    {
        this.command = command;
        this.seconds = seconds;
        inner.Clear();
    }

    public override bool HandleKey(Event e)
    {
        if (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return)
        {
            // now, without waiting
            next = 0f;
            return true;
        }
        return base.HandleKey(e);
    }

    public override void Draw(Screen s)
    {
        if (Time.realtimeSinceStartup >= next)
        {
            next = Time.realtimeSinceStartup + seconds;
            runs++;
            inner.Cols = s.Cols;
            inner.Clear();
            inner.Execute(command);
        }
        for (int i = 0; i < s.Cols; i++)
        {
            s.Put(i, 0, ' ', Ink.Screen, Ink.Normal);
        }
        int x = s.Write(1, 0, $"every {seconds:0.#}s: {command}", Ink.Screen, Ink.Normal);
        string keys = "space now · q quit";
        if (x + keys.Length + 2 < s.Cols)
        {
            s.Write(s.Cols - keys.Length - 1, 0, keys, Ink.Screen, Ink.Normal);
        }
        inner.RenderLines(s, 1);
    }

    public static void Run(Shell sh, string[] args)
    {
        if (args.Length == 0)
        {
            sh.Print("usage: watch [seconds] <command>", Ink.Dim);
            return;
        }
        float seconds = 2f;
        int start = 0;
        if (float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float n))
        {
            seconds = Mathf.Clamp(n, 0.5f, 600f);
            start = 1;
        }
        if (args.Length <= start)
        {
            sh.Print("watch needs a command, like watch 5 pawns", Ink.Bad);
            return;
        }
        string command = string.Join(" ", args, start, args.Length - start);
        string first = command.Split(' ')[0];
        if (first is "watch" or "rimtop" or "top" or "nano" or "less" or "cron_edit" or "manual" or "snake" or "sl")
        {
            sh.Print(first + " has its own screen, watch can't run it", Ink.Bad);
            return;
        }
        sh.App = new WatchApp(command, seconds);
    }
}
