using UnityEngine;

// =====================================================================
// Prism —— 棱镜（玻璃）：激光进入时按折射定律弯折，并因色散分成红/绿/蓝三束。
// 简化为一个半径 PRISM_RADIUS 的圆形玻璃体，在入射点按 Snell 折射，
// 三个波长取不同折射率 → 三束略微发散的彩光（分光）。
// =====================================================================
public class Prism : MonoBehaviour
{
    public int c, r;
    public Vector2 pos;
    public int invest = Defs.PRISM_COST;

    SpriteRenderer sel;

    // 三种"波长"的折射率（红折射率最小、蓝最大 → 色散）
    public static readonly float[] Eta = { 1.0f / 1.42f, 1.0f / 1.50f, 1.0f / 1.58f };
    public static readonly Color[] SpectrumColor = {
        new Color(1f, 0.35f, 0.35f), new Color(0.45f, 1f, 0.5f), new Color(0.45f, 0.6f, 1f)
    };

    public void Init(int c, int r)
    {
        this.c = c; this.r = r;
        pos = new Vector2(c + 0.5f, r + 0.5f);
        transform.position = new Vector3(pos.x, pos.y, 0);
        sel = SpriteFactory.Spawn("sel", SpriteFactory.Ring(), Defs.CYAN, 8, transform);
        sel.transform.localScale = Vector3.one * (Defs.PRISM_RADIUS * 2.4f);
        sel.enabled = false;
        var glow = SpriteFactory.Spawn("pglow", SpriteFactory.Triangle(), new Color(Defs.GLASS.r, Defs.GLASS.g, Defs.GLASS.b, 0.25f), 9, transform);
        glow.transform.localScale = Vector3.one * (Defs.PRISM_RADIUS * 2.4f);
        var body = SpriteFactory.Spawn("pbody", SpriteFactory.Triangle(), new Color(Defs.GLASS.r, Defs.GLASS.g, Defs.GLASS.b, 0.5f), 10, transform);
        body.transform.localScale = Vector3.one * (Defs.PRISM_RADIUS * 2f);
    }

    public void SetSelected(bool s) { if (sel) sel.enabled = s; }
    public int SellValue() => Mathf.RoundToInt(invest * 0.6f);
}
