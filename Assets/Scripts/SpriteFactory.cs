using UnityEngine;

// =====================================================================
// SpriteFactory —— 程序化精灵，增加高分辨率 + 渐变/发光变体
// =====================================================================
public static class SpriteFactory
{
    static Sprite _circle, _square, _triangle, _ring, _softCircle, _diamond;

    public static Sprite Square()
    {
        if (_square != null) return _square;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        _square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _square;
    }

    public static Sprite Circle(int d = 64)
    {
        if (_circle != null) return _circle;
        _circle = Make(d, (x, y, c, r) => (new Vector2(x - c, y - c).magnitude <= r) ? 1f : 0f);
        return _circle;
    }

    // 柔和发光圆（径向渐变衰减）
    public static Sprite SoftCircle(int d = 64)
    {
        if (_softCircle != null) return _softCircle;
        _softCircle = Make(d, (x, y, c, r) =>
        {
            float dist = new Vector2(x - c, y - c).magnitude / r;
            return dist <= 1f ? Mathf.Pow(1f - dist, 1.5f) : 0f;
        });
        return _softCircle;
    }

    public static Sprite Ring(int d = 64, float thick = 0.12f)
    {
        if (_ring != null) return _ring;
        _ring = Make(d, (x, y, c, r) =>
        {
            float dist = new Vector2(x - c, y - c).magnitude;
            return (dist <= r && dist >= r * (1f - thick * 2f)) ? 1f : 0f;
        });
        return _ring;
    }

    // 菱形
    public static Sprite Diamond(int d = 64)
    {
        if (_diamond != null) return _diamond;
        _diamond = Make(d, (x, y, c, r) =>
        {
            float dx = Mathf.Abs(x - c) / r;
            float dy = Mathf.Abs(y - c) / r;
            return (dx + dy <= 1f) ? 1f : 0f;
        });
        return _diamond;
    }

    public static Sprite Triangle(int d = 64)
    {
        if (_triangle != null) return _triangle;
        var tex = new Texture2D(d, d, TextureFormat.RGBA32, false);
        var px = new Color[d * d];
        Vector2 a = new Vector2(0.5f, 0.95f), b = new Vector2(0.06f, 0.06f), cc = new Vector2(0.94f, 0.06f);
        for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / d, (y + 0.5f) / d);
                px[y * d + x] = InTri(p, a, b, cc) ? Color.white : Color.clear;
            }
        tex.SetPixels(px); tex.Apply();
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        _triangle = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), d);
        return _triangle;
    }

    static Sprite Make(int d, System.Func<float, float, float, float, float> field)
    {
        var tex = new Texture2D(d, d, TextureFormat.RGBA32, false);
        var px = new Color[d * d];
        float c = d / 2f, r = d / 2f - 1f;
        for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                float a = field(x + 0.5f, y + 0.5f, c, r);
                px[y * d + x] = new Color(1, 1, 1, a);
            }
        tex.SetPixels(px); tex.Apply();
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), d);
    }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
        bool neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool pos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(neg && pos);
    }
    static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) =>
        (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

    public static SpriteRenderer Spawn(string name, Sprite sprite, Color color, int order, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite; sr.color = color; sr.sortingOrder = order;
        return sr;
    }
}
