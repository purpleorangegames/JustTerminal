using System;
using System.Collections.Generic;
using UnityEngine;

namespace JustTerminal;

// snake, arrows turn, eat the food, don't hit yourself or the walls
public class SnakeApp : App
{
    private readonly List<Vector2Int> body = new List<Vector2Int>();
    private Vector2Int dir = new Vector2Int(1, 0);
    private Vector2Int nextDir = new Vector2Int(1, 0);
    private Vector2Int food;
    private int w;
    private int h;
    private float nextStep;
    private bool dead;
    private int score;
    private static int best;
    private readonly System.Random rand = new System.Random();

    public override float RedrawEvery => 0.03f;

    private void Start(int cols, int rows)
    {
        w = cols;
        h = rows;
        body.Clear();
        for (int i = 0; i < 4; i++)
        {
            body.Add(new Vector2Int(w / 2 - i, h / 2));
        }
        dir = nextDir = new Vector2Int(1, 0);
        dead = false;
        score = 0;
        Food();
    }

    private void Food()
    {
        for (int tries = 0; tries < 500; tries++)
        {
            food = new Vector2Int(rand.Next(0, w), rand.Next(0, h));
            if (!body.Contains(food))
            {
                return;
            }
        }
    }

    public override bool HandleKey(Event e)
    {
        Vector2Int want = e.keyCode switch
        {
            KeyCode.UpArrow or KeyCode.W => new Vector2Int(0, -1),
            KeyCode.DownArrow or KeyCode.S => new Vector2Int(0, 1),
            KeyCode.LeftArrow or KeyCode.A => new Vector2Int(-1, 0),
            KeyCode.RightArrow or KeyCode.D => new Vector2Int(1, 0),
            _ => Vector2Int.zero
        };
        // no turning back into yourself
        if (want != Vector2Int.zero && want != -dir)
        {
            nextDir = want;
            return true;
        }
        if (dead && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return))
        {
            w = 0;
            return true;
        }
        return base.HandleKey(e);
    }

    public override void Draw(Screen s)
    {
        int cols = s.Cols, rows = s.Rows - 1;
        if (cols < 20 || rows < 8)
        {
            s.Write(1, 1, "make the window bigger, q quits", Ink.Warn);
            return;
        }
        if (w != cols || h != rows)
        {
            Start(cols, rows);
        }
        // a bit slower going up and down, the cells are taller than wide
        float step = dir.y != 0 ? 0.12f : 0.07f;
        if (!dead && Time.realtimeSinceStartup >= nextStep)
        {
            nextStep = Time.realtimeSinceStartup + step;
            dir = nextDir;
            Vector2Int head = body[0] + dir;
            if (head.x < 0 || head.y < 0 || head.x >= w || head.y >= h || body.Contains(head))
            {
                dead = true;
                best = Math.Max(best, score);
            }
            else
            {
                body.Insert(0, head);
                if (head == food)
                {
                    score++;
                    Food();
                }
                else
                {
                    body.RemoveAt(body.Count - 1);
                }
            }
        }
        for (int i = 0; i < s.Cols; i++)
        {
            s.Put(i, 0, ' ', Ink.Screen, Ink.Normal);
        }
        s.Write(1, 0, $"snake  score {score}  best {Math.Max(best, score)}", Ink.Screen, Ink.Normal);
        string keys = dead ? "space again · q quit" : "arrows turn · q quit";
        s.Write(s.Cols - keys.Length - 1, 0, keys, Ink.Screen, Ink.Normal);
        s.Put(food.x, food.y + 1, '●', Ink.Warn);
        for (int i = 0; i < body.Count; i++)
        {
            s.Put(body[i].x, body[i].y + 1, '█', i == 0 ? Ink.Bright : dead ? Ink.Bad : Ink.Good);
        }
        if (dead)
        {
            string over = $" game over, {score} eaten ";
            s.Write(Math.Max(0, (cols - over.Length) / 2), rows / 2, over, Ink.Screen, Ink.Bad);
        }
    }
}

// a train goes by, like sl when ls is typed wrong
public class TrainApp : App
{
    private static readonly string[] train =
    {
        @"                    (  )  (@@)   ( )    (@)     ()",
        @"                 (@@@)",
        @"         ____   |                                       ",
        @"   _____|____|__|_      _______________   _______________",
        @"  |  ___  RIM     |    |   COLONISTS   | |    COMPONENTS |",
        @"  | |___|  EXPRESS|-oo-|               |-|               |",
        @" _|_______________|    |_______________| |_______________|",
        @" \_(@)==(@)==(@)_/       (@)       (@)     (@)       (@)  "
    };

    private float start = -1f;

    public override float RedrawEvery => 0.03f;

    // only esc stops it, the rest of the keys do nothing, like sl
    public override bool HandleKey(Event e)
    {
        if (e.keyCode == KeyCode.Escape)
        {
            Done = true;
        }
        return true;
    }

    public override void Draw(Screen s)
    {
        if (start < 0f)
        {
            start = Time.realtimeSinceStartup;
        }
        int width = 0;
        foreach (string line in train)
        {
            width = Math.Max(width, line.Length);
        }
        int x = s.Cols - (int)((Time.realtimeSinceStartup - start) * 40f);
        if (x < -width)
        {
            Done = true;
            return;
        }
        int y0 = Math.Max(0, (s.Rows - train.Length) / 2);
        for (int j = 0; j < train.Length; j++)
        {
            string line = train[j];
            // the smoke drifts a little
            int shift = j < 2 ? (int)((Time.realtimeSinceStartup - start) * 3f) % 3 : 0;
            for (int i = 0; i < line.Length; i++)
            {
                int cx = x + i + shift;
                if (cx >= 0 && cx < s.Cols && line[i] != ' ')
                {
                    s.Put(cx, y0 + j, line[i], j < 2 ? Ink.Dim : Ink.Bright);
                }
            }
        }
    }
}
