using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// GameMap —— 增强版地图：路径边缘发光、拐角标记、核心脉冲光环
// =====================================================================
public class GameMap
{
    public HashSet<Vector2Int> path = new HashSet<Vector2Int>();
    Transform coreStar, coreHalo;
    SpriteRenderer fogA, fogB;
    Vector3 fogAAnchor, fogBAnchor;
    Vector3 fogAScaleBase, fogBScaleBase;
    Color fogAColor, fogBColor;

    public GameMap() { MarkPath(); }

    void MarkPath()
    {
        var wp = Defs.Waypoints;
        for (int i = 0; i < wp.Length - 1; i++)
        {
            Vector2 a = wp[i], b = wp[i + 1];
            int steps = Mathf.Max(1, Mathf.CeilToInt((a - b).magnitude * 2f));
            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(a, b, (float)s / steps);
                path.Add(new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)));
            }
        }
    }

    public bool Buildable(int c, int r)
    {
        if (c < 0 || r < 0 || c >= Defs.COLS || r >= Defs.ROWS) return false;
        return !path.Contains(new Vector2Int(c, r));
    }

    public void BuildBackground()
    {
        var holder = new GameObject("Background").transform;

        // 棋盘地块
        for (int r = 0; r < Defs.ROWS; r++)
            for (int c = 0; c < Defs.COLS; c++)
            {
                bool onPath = path.Contains(new Vector2Int(c, r));
                Color col;
                if (onPath)
                {
                    // 路径格略有渐变
                    float t = (float)(c + r) / (Defs.COLS + Defs.ROWS);
                    col = Color.Lerp(Defs.PATH, Defs.PATH_EDGE, t * 0.3f);
                }
                else
                {
                    col = (c + r) % 2 == 0
                        ? new Color(64/255f, 42/255f, 84/255f)
                        : new Color(46/255f, 30/255f, 66/255f);
                }
                var sr = SpriteFactory.Spawn("tile", SpriteFactory.Square(), col, -10, holder);
                sr.transform.position = new Vector3(c + 0.5f, r + 0.5f, 0);
                sr.transform.localScale = new Vector3(onPath ? 1f : 0.94f, onPath ? 1f : 0.94f, 1f);

                // 路径边缘发光点
                if (onPath)
                {
                    bool above = !path.Contains(new Vector2Int(c, r + 1));
                    bool below = !path.Contains(new Vector2Int(c, r - 1));
                    bool left  = !path.Contains(new Vector2Int(c - 1, r));
                    bool right = !path.Contains(new Vector2Int(c + 1, r));
                    if (above || below || left || right)
                    {
                        var edge = SpriteFactory.Spawn("edge", SpriteFactory.Square(),
                            new Color(Defs.AMBER.r, Defs.AMBER.g, Defs.AMBER.b, 0.08f), -9, holder);
                        edge.transform.position = new Vector3(c + 0.5f, r + 0.5f, 0);
                        edge.transform.localScale = Vector3.one;
                    }
                }
            }

        fogA = SpriteFactory.Spawn("fogA", SpriteFactory.SoftCircle(),
            new Color(Defs.MAGENTA.r, Defs.MAGENTA.g, Defs.MAGENTA.b, 0.10f), -12, holder);
        fogAAnchor = new Vector3(Defs.COLS * 0.35f, Defs.ROWS * 0.62f, 0);
        fogAScaleBase = Vector3.one * 24f;
        fogAColor = fogA.color;
        fogA.transform.position = fogAAnchor;
        fogA.transform.localScale = fogAScaleBase;

        fogB = SpriteFactory.Spawn("fogB", SpriteFactory.SoftCircle(),
            new Color(Defs.AMBER.r, Defs.AMBER.g, Defs.AMBER.b, 0.07f), -12, holder);
        fogBAnchor = new Vector3(Defs.COLS * 0.82f, Defs.ROWS * 0.18f, 0);
        fogBScaleBase = Vector3.one * 20f;
        fogBColor = fogB.color;
        fogB.transform.position = fogBAnchor;
        fogB.transform.localScale = fogBScaleBase;

        // 路径发光带
        var glowGO = new GameObject("PathGlow");
        glowGO.transform.SetParent(holder);
        var lr = glowGO.AddComponent<LineRenderer>();
        SetupLine(lr, 0.8f, new Color(Defs.MAGENTA.r, Defs.MAGENTA.g, Defs.MAGENTA.b, 0.14f), -8);
        var wp = Defs.Waypoints;
        lr.positionCount = wp.Length;
        for (int i = 0; i < wp.Length; i++) lr.SetPosition(i, new Vector3(wp[i].x, wp[i].y, 0));

        // 路径拐角标记
        for (int i = 1; i < wp.Length - 1; i++)
        {
            var dot = SpriteFactory.Spawn("corner", SpriteFactory.Circle(),
                new Color(Defs.AMBER.r, Defs.AMBER.g, Defs.AMBER.b, 0.16f), -7, holder);
            dot.transform.position = new Vector3(wp[i].x, wp[i].y, 0);
            dot.transform.localScale = Vector3.one * 0.4f;
        }

        // 入口标记
        var entry = SpriteFactory.Spawn("entry", SpriteFactory.Triangle(),
            new Color(Defs.DANGER.r, Defs.DANGER.g, Defs.DANGER.b, 0.42f), -7, holder);
        entry.transform.position = new Vector3(wp[0].x + 0.5f, wp[0].y, 0);
        entry.transform.localScale = Vector3.one * 0.5f;
        entry.transform.rotation = Quaternion.Euler(0, 0, -90);

        // 能量核心 —— 多层光环
        Vector2 core = wp[wp.Length - 1];
        float cx = Mathf.Min(core.x, Defs.COLS - 0.4f);

        // 外层脉冲光环
        var halo2 = SpriteFactory.Spawn("CoreHalo2", SpriteFactory.SoftCircle(),
            new Color(Defs.MAGENTA.r, Defs.MAGENTA.g, Defs.MAGENTA.b, 0.16f), -7, holder);
        halo2.transform.position = new Vector3(cx, core.y, 0);
        halo2.transform.localScale = Vector3.one * 2.4f;
        coreHalo = halo2.transform;

        var halo = SpriteFactory.Spawn("CoreHalo", SpriteFactory.Circle(),
            new Color(Defs.AMBER.r, Defs.AMBER.g, Defs.AMBER.b, 0.35f), -6, holder);
        halo.transform.position = new Vector3(cx, core.y, 0);
        halo.transform.localScale = Vector3.one * 1.4f;

        var star = SpriteFactory.Spawn("CoreStar", SpriteFactory.Triangle(),
            new Color(1f, 0.88f, 0.68f, 1f), -5, holder);
        star.transform.position = new Vector3(cx, core.y, 0);
        star.transform.localScale = Vector3.one * 0.65f;
        coreStar = star.transform;

        // 内核亮点
        var inner = SpriteFactory.Spawn("CoreInner", SpriteFactory.Circle(),
            new Color(0.9f, 0.95f, 1f, 0.8f), -4, holder);
        inner.transform.position = new Vector3(cx, core.y, 0);
        inner.transform.localScale = Vector3.one * 0.25f;
    }

    public void Tick(float t)
    {
        if (fogA != null)
        {
            float dx = Mathf.Sin(t * 0.07f + 1.2f) * 0.65f;
            float dy = Mathf.Cos(t * 0.05f + 0.7f) * 0.45f;
            fogA.transform.position = fogAAnchor + new Vector3(dx, dy, 0);
            float s = 1f + Mathf.Sin(t * 0.12f + 0.9f) * 0.035f;
            fogA.transform.localScale = fogAScaleBase * s;
            float a = fogAColor.a * (0.86f + Mathf.Sin(t * 0.22f + 0.3f) * 0.14f);
            fogA.color = new Color(fogAColor.r, fogAColor.g, fogAColor.b, a);
        }
        if (fogB != null)
        {
            float dx = Mathf.Sin(t * 0.06f + 2.1f) * 0.55f;
            float dy = Mathf.Cos(t * 0.045f + 1.1f) * 0.52f;
            fogB.transform.position = fogBAnchor + new Vector3(dx, dy, 0);
            float s = 1f + Mathf.Sin(t * 0.11f + 1.7f) * 0.04f;
            fogB.transform.localScale = fogBScaleBase * s;
            float a = fogBColor.a * (0.84f + Mathf.Sin(t * 0.19f + 1.3f) * 0.16f);
            fogB.color = new Color(fogBColor.r, fogBColor.g, fogBColor.b, a);
        }
        if (coreStar != null)
        {
            coreStar.rotation = Quaternion.Euler(0, 0, t * 50f);
            float s = 0.65f + Mathf.Sin(t * 2.5f) * 0.08f;
            coreStar.localScale = Vector3.one * s;
        }
        if (coreHalo != null)
        {
            float pulse = 2.2f + Mathf.Sin(t * 1.5f) * 0.4f;
            coreHalo.localScale = Vector3.one * pulse;
        }
    }

    public static void SetupLine(LineRenderer lr, float width, Color color, int order)
    {
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.widthMultiplier = width;
        lr.numCapVertices = 4;
        lr.textureMode = LineTextureMode.Stretch;
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = order;
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;
    }
}
