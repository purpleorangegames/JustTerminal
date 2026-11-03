using System.Text;
using UnityEngine;
using Verse;

namespace JustTerminal;

// the monitor look: plastic frame, dark glass, glowing text, scanlines, darker edges
[StaticConstructorOnStartup]
public static class Crt
{
    public const float Frame = 16f;
    private const float GlassRadius = 22f;
    private const float FrameRadius = 14f;

    private static Texture2D disc;
    private static Texture2D scanlines;
    private const int ScanHeight = 300;
    private static Texture2D vignette;
    private static Texture2D band;
    private static Texture2D[] corners;
    private static Texture2D sheen;
    // the frame color, from the theme, the corner pieces are made again when it changes
    private static Color plastic = new Color(0.12f, 0.12f, 0.115f);
    private static Color cornersFor = new Color(-1f, 0f, 0f);
    private static readonly StringBuilder run = new StringBuilder();

    private static void MakeTextures()
    {
        if (disc != null)
        {
            return;
        }
        // white disc, its quarters are the rounded corners
        const int d = 64;
        disc = new Texture2D(d, d, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < d; y++)
        {
            for (int x = 0; x < d; x++)
            {
                float dist = new Vector2(x + 0.5f - d / 2f, y + 0.5f - d / 2f).magnitude;
                disc.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(d / 2f - dist)));
            }
        }
        disc.Apply();

        // dark line every third pixel, tall enough to stack a few strips instead of relying on texture repeat
        scanlines = new Texture2D(1, ScanHeight, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
        for (int y = 0; y < ScanHeight; y++)
        {
            int row = y % 3;
            scanlines.SetPixel(0, y, new Color(0f, 0f, 0f, row == 0 ? 1f : row == 1 ? 0.31f : 0f));
        }
        scanlines.Apply();

        const int v = 64;
        vignette = new Texture2D(v, v, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < v; y++)
        {
            for (int x = 0; x < v; x++)
            {
                float dx = (x + 0.5f) / v * 2f - 1f;
                float dy = (y + 0.5f) / v * 2f - 1f;
                // squarish falloff like a tube
                float r = Mathf.Pow(Mathf.Pow(Mathf.Abs(dx), 4f) + Mathf.Pow(Mathf.Abs(dy), 4f), 0.25f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1.1f, r));
                vignette.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }
        vignette.Apply();

        // soft bright band that rolls down the glass
        band = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++)
        {
            float t = Mathf.Sin((y + 0.5f) / 32f * Mathf.PI);
            band.SetPixel(0, y, new Color(1f, 1f, 1f, t * t));
        }
        band.Apply();

        // glass reflection, fades out going down
        sheen = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
        {
            float t = (y + 0.5f) / 64f;
            sheen.SetPixel(0, y, new Color(1f, 1f, 1f, t * t));
        }
        sheen.Apply();

    }

    private static void FrameColor(Color c)
    {
        plastic = c;
        if (corners != null && c == cornersFor)
        {
            return;
        }
        if (corners != null)
        {
            foreach (Texture2D t in corners)
            {
                UnityEngine.Object.Destroy(t);
            }
        }
        corners = new[] { Corner(false, false), Corner(true, false), Corner(false, true), Corner(true, true) };
        cornersFor = c;
    }

    // covers the glass corners: plastic outside, the dark lip, clear over the glass
    private static Texture2D Corner(bool right, bool bottom)
    {
        const int scale = 3;
        float size = GlassRadius + 2f;
        int s = (int)(size * scale);
        Color lip = new Color(0.03f, 0.03f, 0.03f);
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                // ui position inside the top left corner, y going down
                float ux = (x + 0.5f) / scale;
                float uy = (s - 1 - y + 0.5f) / scale;
                if (right)
                {
                    ux = size - ux;
                }
                if (bottom)
                {
                    uy = size - uy;
                }
                float dist = new Vector2(ux - size, uy - size).magnitude;
                float inner = Mathf.Clamp01(GlassRadius + 0.5f - dist);
                float outer = Mathf.Clamp01(dist - size + 0.5f);
                Color c = Color.Lerp(lip, plastic, outer);
                c.a = 1f - inner;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    public static void DrawCorners(Rect glass)
    {
        float size = GlassRadius + 2f;
        Rect r = glass.ExpandedBy(2f);
        Color old = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(r.x, r.y, size, size), corners[0]);
        GUI.DrawTexture(new Rect(r.xMax - size, r.y, size, size), corners[1]);
        GUI.DrawTexture(new Rect(r.x, r.yMax - size, size, size), corners[2]);
        GUI.DrawTexture(new Rect(r.xMax - size, r.yMax - size, size, size), corners[3]);
        GUI.color = old;
    }

    // small text printed on the plastic frame
    public static void FrameLabel(Rect r, string text, TextAnchor anchor, bool bright)
    {
        GameFont font = Text.Font;
        TextAnchor old = Text.Anchor;
        Color color = GUI.color;
        Text.Font = GameFont.Tiny;
        Text.Anchor = anchor;
        GUI.color = bright ? new Color(0.75f, 0.75f, 0.7f) : new Color(0.42f, 0.42f, 0.4f);
        Widgets.Label(r, text);
        GUI.color = color;
        Text.Anchor = old;
        Text.Font = font;
    }

    // round light on the frame, lit in the screen's color when on
    public static void Led(Rect r, bool on)
    {
        MakeTextures();
        Color old = GUI.color;
        GUI.color = new Color(0.04f, 0.04f, 0.04f);
        GUI.DrawTexture(r.ExpandedBy(1f), disc);
        Color lit = Phosphor.Get(JustTerminalMod.Settings.phosphor)[Ink.Normal];
        GUI.color = on ? lit : new Color(0.16f, 0.16f, 0.15f);
        GUI.DrawTexture(r, disc);
        if (on)
        {
            // glow around it
            GUI.color = new Color(lit.r, lit.g, lit.b, 0.25f);
            GUI.DrawTexture(r.ExpandedBy(3f), disc);
        }
        GUI.color = old;
    }

    public static void RoundedRect(Rect r, float radius, Color color)
    {
        radius = Mathf.Min(radius, r.width / 2f, r.height / 2f);
        Color old = GUI.color;
        GUI.color = color;
        Widgets.DrawBoxSolid(new Rect(r.x + radius, r.y, r.width - radius * 2f, r.height), color);
        Widgets.DrawBoxSolid(new Rect(r.x, r.y + radius, radius, r.height - radius * 2f), color);
        Widgets.DrawBoxSolid(new Rect(r.xMax - radius, r.y + radius, radius, r.height - radius * 2f), color);
        GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, radius, radius), disc, new Rect(0f, 0.5f, 0.5f, 0.5f));
        GUI.DrawTextureWithTexCoords(new Rect(r.xMax - radius, r.y, radius, radius), disc, new Rect(0.5f, 0.5f, 0.5f, 0.5f));
        GUI.DrawTextureWithTexCoords(new Rect(r.x, r.yMax - radius, radius, radius), disc, new Rect(0f, 0f, 0.5f, 0.5f));
        GUI.DrawTextureWithTexCoords(new Rect(r.xMax - radius, r.yMax - radius, radius, radius), disc, new Rect(0.5f, 0f, 0.5f, 0.5f));
        GUI.color = old;
    }

    // draws the frame and returns the glass
    public static Rect DrawFrame(Rect rect, Phosphor p)
    {
        MakeTextures();
        FrameColor(p.frame);
        Color edge = new Color(Mathf.Min(1f, plastic.r * 1.6f + 0.02f), Mathf.Min(1f, plastic.g * 1.6f + 0.02f), Mathf.Min(1f, plastic.b * 1.6f + 0.02f));
        RoundedRect(rect, FrameRadius, edge);
        RoundedRect(rect.ContractedBy(1f), FrameRadius - 1f, plastic);
        Rect glass = rect.ContractedBy(Frame);
        // a thin dark lip around the tube
        RoundedRect(glass.ExpandedBy(2f), GlassRadius + 2f, new Color(0.03f, 0.03f, 0.03f));
        Widgets.DrawBoxSolid(glass, p.glass);
        return glass;
    }

    public static void DrawText(Rect area, Screen screen, Phosphor p, int fontSize, float alpha)
    {
        Vector2 cell = TermFont.Cell(fontSize);
        GUIStyle style = TermFont.Style(fontSize);
        Color old = GUI.color;
        // backgrounds
        for (int y = 0; y < screen.Rows; y++)
        {
            for (int x = 0; x < screen.Cols;)
            {
                Ink bg = screen.At(x, y).bg;
                int start = x;
                while (x < screen.Cols && screen.At(x, y).bg == bg)
                {
                    x++;
                }
                if (bg != Ink.None)
                {
                    Color c = p[bg];
                    c.a *= alpha;
                    Widgets.DrawBoxSolid(new Rect(area.x + start * cell.x, area.y + y * cell.y, (x - start) * cell.x, cell.y), c);
                }
            }
        }
        // glow first, a few faint copies around the text, then the text
        float glow = JustTerminalMod.Settings.glow;
        // stronger glow is brighter and spreads a little more
        float spread = 1.5f * Mathf.Sqrt(Mathf.Max(glow, 1f));
        for (int pass = glow > 0f ? 0 : 4; pass < 5; pass++)
        {
            Vector2 offset = pass switch
            {
                0 => new Vector2(-spread, 0f),
                1 => new Vector2(spread, 0f),
                2 => new Vector2(0f, -spread),
                3 => new Vector2(0f, spread),
                _ => Vector2.zero
            };
            float strength = pass < 4 ? Mathf.Min(0.22f * glow, 0.9f) : 1f;
            for (int y = 0; y < screen.Rows; y++)
            {
                for (int x = 0; x < screen.Cols;)
                {
                    Ink fg = screen.At(x, y).fg;
                    int start = x;
                    run.Clear();
                    char single = '\0';
                    if (!TermFont.Fits(fontSize, screen.At(x, y).ch))
                    {
                        single = screen.At(x, y).ch;
                        run.Append(single);
                        x++;
                    }
                    else
                    {
                        while (x < screen.Cols && screen.At(x, y).fg == fg && TermFont.Fits(fontSize, screen.At(x, y).ch))
                        {
                            run.Append(screen.At(x, y).ch);
                            x++;
                        }
                    }
                    string text = run.ToString();
                    if (text.Trim().Length == 0)
                    {
                        continue;
                    }
                    Color c = p[fg];
                    c.a = alpha * strength;
                    GUI.color = c;
                    if (single != '\0')
                    {
                        // centered in its cell
                        float w = style.CalcSize(new GUIContent(text)).x;
                        GUI.Label(new Rect(area.x + start * cell.x + (cell.x - w) / 2f + offset.x, area.y + y * cell.y + offset.y, w + 4f, cell.y + 4f), text, style);
                        continue;
                    }
                    GUI.Label(new Rect(area.x + start * cell.x + offset.x, area.y + y * cell.y + offset.y, (x - start) * cell.x + 8f, cell.y + 4f), text, style);
                }
            }
        }
        GUI.color = old;
    }

    public static void DrawGlass(Rect glass, Phosphor p)
    {
        JustTerminalSettings set = JustTerminalMod.Settings;
        Color old = GUI.color;
        if (set.scanlines > 0f)
        {
            GUI.color = new Color(1f, 1f, 1f, Mathf.Min(0.32f * set.scanlines, 1f));
            GUI.BeginGroup(glass);
            for (float y = 0f; y < glass.height; y += ScanHeight)
            {
                GUI.DrawTexture(new Rect(0f, y, glass.width, ScanHeight), scanlines);
            }
            GUI.EndGroup();
        }
        if (set.band > 0f)
        {
            float t = Time.realtimeSinceStartup / 7f % 1f;
            float h = glass.height * 0.25f;
            Rect bandRect = new Rect(glass.x, glass.y - h + (glass.height + h) * t, glass.width, h);
            Color tint = p[Ink.Normal];
            GUI.color = new Color(tint.r, tint.g, tint.b, Mathf.Min(0.025f * set.band, 1f));
            GUI.BeginGroup(glass);
            GUI.DrawTexture(new Rect(bandRect.x - glass.x, bandRect.y - glass.y, bandRect.width, bandRect.height), band);
            GUI.EndGroup();
        }
        if (set.vignette > 0f)
        {
            GUI.color = new Color(1f, 1f, 1f, Mathf.Min(0.55f * set.vignette, 1f));
            GUI.DrawTexture(glass, vignette);
        }
        if (set.reflection > 0f)
        {
            // reflection on the upper half
            GUI.color = new Color(1f, 1f, 1f, Mathf.Min(0.035f * set.reflection, 1f));
            GUI.DrawTexture(new Rect(glass.x, glass.y, glass.width, glass.height * 0.45f), sheen);
        }
        GUI.color = old;
    }
}
