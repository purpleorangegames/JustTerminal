using UnityEngine;

namespace JustTerminal;

// a program that takes the whole screen until it quits, like top
public abstract class App
{
    public bool Done;
    // another program to open on top of this one
    public App Push;
    // the program to go back to when this one quits
    public App Then;

    // seconds between draws when nothing is pressed, games go faster
    public virtual float RedrawEvery => 0.5f;

    // back on screen after the program it opened quit
    public virtual void Back()
    {
    }

    // draws everything into the screen, called a few times a second and after each key
    public abstract void Draw(Screen screen);

    // true when the key was used, q and esc quit by default
    public virtual bool HandleKey(Event e)
    {
        if (e.keyCode == KeyCode.Q || e.keyCode == KeyCode.Escape)
        {
            Done = true;
            return true;
        }
        return false;
    }

    public virtual void Scroll(int rows)
    {
    }

    // a left click on a character cell
    public virtual void Click(int x, int y)
    {
    }

    // box drawing helpers

    public static void Box(Screen s, int x, int y, int w, int h, string title, Ink border = Ink.Dim)
    {
        s.NoClip();
        if (w < 2 || h < 2)
        {
            return;
        }
        for (int i = 1; i < w - 1; i++)
        {
            s.Put(x + i, y, '─', border);
            s.Put(x + i, y + h - 1, '─', border);
        }
        for (int j = 1; j < h - 1; j++)
        {
            s.Put(x, y + j, '│', border);
            s.Put(x + w - 1, y + j, '│', border);
        }
        s.Put(x, y, '╭', border);
        s.Put(x + w - 1, y, '╮', border);
        s.Put(x, y + h - 1, '╰', border);
        s.Put(x + w - 1, y + h - 1, '╯', border);
        if (!string.IsNullOrEmpty(title) && w > 6)
        {
            s.Put(x + 1, y, '┤', border);
            int end = s.Write(x + 2, y, Clip(title, w - 6), Ink.Bright);
            s.Put(end, y, '├', border);
        }
        // what comes next stays inside the box
        s.Clip(x + 1, y + 1, w - 2, h - 2);
    }

    public static string Clip(string text, int width)
    {
        if (width <= 0)
        {
            return "";
        }
        text = Screen.Plain(text);
        return text.Length <= width ? text : text.Substring(0, width - 1) + "…";
    }

    // btop style meter, filled squares on a dim track, returns the x after it
    public static int Bar(Screen s, int x, int y, float fraction, int width, Ink ink, Ink bg = Ink.None)
    {
        int filled = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(fraction) * width), fraction > 0f ? 1 : 0, width);
        for (int i = 0; i < width; i++)
        {
            s.Put(x + i, y, '■', i < filled ? ink : bg == Ink.None ? Ink.Dim : Ink.Screen, bg);
        }
        return x + width;
    }
}
