using UnityEngine;

// =====================================================================
// Mirror —— 反射镜：可连续旋转到任意角度（滚轮 / Q E 键）。
// 反射面是一条沿 angle 方向的线段，激光按入射角=反射角反射。
// =====================================================================
public class Mirror : MonoBehaviour
{
    public int c, r;
    public Vector2 pos;
    public float angle = 30f;       // 任意角度（度）
    public int invest = Defs.MIRROR_COST;
    public const float LEN = 1.4f;

    SpriteRenderer bar, sel;

    public void Init(int c, int r)
    {
        this.c = c; this.r = r;
        pos = new Vector2(c + 0.5f, r + 0.5f);
        transform.position = new Vector3(pos.x, pos.y, 0);
        sel = SpriteFactory.Spawn("sel", SpriteFactory.Ring(), Defs.CYAN, 8, transform);
        sel.transform.localScale = Vector3.one * 1.1f;
        sel.enabled = false;
        var glow = SpriteFactory.Spawn("mglow", SpriteFactory.Square(), new Color(0.55f, 0.7f, 1f, 0.6f), 9, transform);
        glow.transform.localScale = new Vector3(LEN, 0.18f, 1);
        bar = SpriteFactory.Spawn("mbar", SpriteFactory.Square(), new Color(0.86f, 0.92f, 1f, 1f), 10, transform);
        bar.transform.localScale = new Vector3(LEN, 0.1f, 1);
        UpdateVisual();
    }

    public void SetSelected(bool s) { if (sel) sel.enabled = s; }

    public void Rotate(float deltaDeg)
    {
        angle = Mathf.Repeat(angle + deltaDeg, 360f);
        UpdateVisual();
    }

    void UpdateVisual()
    {
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    public Vector2 Dir()
    {
        float a = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }
    public Vector2 Normal()
    {
        Vector2 d = Dir();
        return new Vector2(-d.y, d.x);
    }
    public void Endpoints(out Vector2 a, out Vector2 b)
    {
        Vector2 d = Dir() * (LEN / 2f);
        a = pos - d; b = pos + d;
    }
    public int SellValue() => Mathf.RoundToInt(invest * 0.6f);
}
