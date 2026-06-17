using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// Enemy —— 美化后的怪物造型：暗影游灵/幽光精灵/铁甲魔像/深渊巨龙
// 流畅动画、渐变色彩、发光特效
// =====================================================================
public class Enemy : MonoBehaviour
{
    public EnemyKind kind;
    public float maxHp, hp, speed, radius, armor;
    public int reward; public bool boss;
    public Color color;
    public int wp = 0;
    public Vector2 pos;
    public float slowT = 0f, burn = 0f, flash = 0f, facing = 1f, tphase;
    public bool dead = false, reached = false, counted = false;

    Transform vis;
    readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
    readonly List<Color> baseCols = new List<Color>();
    SpriteRenderer slowAura, hpBg, hpFill, hpFrame;
    float barW;
    Color bodyColor, accentColor, eyeColor;

    public bool Slowed => slowT > 0f;

    public void Init(EnemyKind k, float hpScale)
    {
        kind = k; var d = Defs.Enemies[k];
        maxHp = Mathf.Round(d.hp * hpScale); hp = maxHp;
        speed = d.speed; radius = d.radius; armor = d.armor; reward = d.reward;
        bodyColor = d.body; accentColor = d.accent; eyeColor = d.eye;
        color = d.body; boss = d.boss; tphase = Random.Range(0f, 6.28f);
        pos = Defs.Waypoints[0];
        transform.position = new Vector3(pos.x, pos.y, 0);

        slowAura = SpriteFactory.Spawn("aura", SpriteFactory.SoftCircle(), new Color(Defs.FROST.r,Defs.FROST.g,Defs.FROST.b,0.35f), 4, transform);
        slowAura.transform.localScale = Vector3.one * (radius*2 + 0.35f);
        slowAura.enabled = false;

        vis = new GameObject("vis").transform; vis.SetParent(transform, false);
        if (k == EnemyKind.Slime || k == EnemyKind.Mini) BuildSpiritOrb(radius, k == EnemyKind.Mini);
        else if (k == EnemyKind.Mech) BuildGolem(radius);
        else BuildDragon(radius);

        // 现代化血条：圆角背景 + 填充 + 边框
        barW = Mathf.Max(radius * 2.2f, 0.5f);
        float barH = boss ? 0.12f : 0.07f;
        float barY = radius + 0.22f;

        hpBg = SpriteFactory.Spawn("hpbg", SpriteFactory.Square(), new Color(0,0,0,0.55f), 9, transform);
        hpBg.transform.localScale = new Vector3(barW + 0.04f, barH + 0.03f, 1);
        hpBg.transform.localPosition = new Vector3(0, barY, 0);

        hpFill = SpriteFactory.Spawn("hpfill", SpriteFactory.Square(), Defs.HP, 10, transform);
        hpFill.transform.localScale = new Vector3(barW, barH, 1);
        hpFill.transform.localPosition = new Vector3(0, barY, 0);

        hpFrame = SpriteFactory.Spawn("hpframe", SpriteFactory.Square(), new Color(1,1,1,0.12f), 11, transform);
        hpFrame.transform.localScale = new Vector3(barW + 0.06f, barH + 0.05f, 1);
        hpFrame.transform.localPosition = new Vector3(0, barY, 0);
    }

    SpriteRenderer Add(Sprite spr, Color col, float lx, float ly, float sx, float sy, float rot, int order)
    {
        var sr = SpriteFactory.Spawn("p", spr, col, order, vis);
        sr.transform.localPosition = new Vector3(lx, ly, 0);
        sr.transform.localScale = new Vector3(sx, sy, 1);
        sr.transform.localRotation = Quaternion.Euler(0, 0, rot);
        parts.Add(sr); baseCols.Add(col);
        return sr;
    }
    static Color Lighten(Color c, float t) => Color.Lerp(c, Color.white, t);
    static Color Darken(Color c, float t) => Color.Lerp(c, Color.black, t);
    static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    // ---- Q 弹史莱姆风 ----
    void BuildSpiritOrb(float r, bool mini)
    {
        float bodyScale = mini ? 1.8f : 2.15f;
        Color rim = Lighten(bodyColor, mini ? 0.22f : 0.14f);
        Add(SpriteFactory.SoftCircle(), WithAlpha(accentColor, 0.18f), 0, -r * 0.05f, r * 3.4f, r * 3.0f, 0, 3);
        Add(SpriteFactory.SoftCircle(), new Color(0, 0, 0, 0.12f), 0, -r * 0.95f, r * 2.0f, r * 0.65f, 0, 4);
        Add(SpriteFactory.Circle(), bodyColor, 0, -r * 0.05f, r * bodyScale, r * (bodyScale * 0.95f), 0, 5);
        Add(SpriteFactory.Circle(), rim, 0, r * 0.12f, r * (bodyScale * 0.9f), r * (bodyScale * 0.78f), 0, 6);

        float crestY = mini ? r * 1.0f : r * 1.14f;
        float crestScale = mini ? r * 0.5f : r * 0.62f;
        Add(SpriteFactory.Triangle(), accentColor, 0, crestY, crestScale, crestScale * 0.95f, 0, 7);
        Add(SpriteFactory.SoftCircle(), WithAlpha(Color.white, 0.18f), -r * 0.25f, r * 0.45f, r * 0.9f, r * 0.7f, 0, 7);

        float eyeY = mini ? r * 0.08f : r * 0.02f;
        float eyeGap = mini ? 0f : r * 0.32f;
        float eyeSize = mini ? r * 0.3f : r * 0.26f;
        if (mini)
        {
            Add(SpriteFactory.Circle(), eyeColor, 0, eyeY, eyeSize * 1.25f, eyeSize * 1.25f, 0, 8);
            Add(SpriteFactory.Circle(), new Color(0.08f, 0.08f, 0.14f), 0, eyeY, eyeSize * 0.6f, eyeSize * 0.6f, 0, 9);
        }
        else
        {
            Add(SpriteFactory.Circle(), eyeColor, -eyeGap, eyeY, eyeSize, eyeSize * 1.1f, 0, 8);
            Add(SpriteFactory.Circle(), eyeColor, eyeGap, eyeY, eyeSize, eyeSize * 1.1f, 0, 8);
            Add(SpriteFactory.Circle(), new Color(0.08f, 0.08f, 0.14f), -eyeGap, eyeY, eyeSize * 0.52f, eyeSize * 0.6f, 0, 9);
            Add(SpriteFactory.Circle(), new Color(0.08f, 0.08f, 0.14f), eyeGap, eyeY, eyeSize * 0.52f, eyeSize * 0.6f, 0, 9);
        }

        Add(SpriteFactory.Square(), WithAlpha(new Color(0.18f, 0.12f, 0.24f), 0.75f), 0, -r * 0.32f, r * 0.42f, r * 0.08f, 0, 8);
        Add(SpriteFactory.SoftCircle(), WithAlpha(bodyColor, 0.22f), 0, -r * 0.78f, r * 1.8f, r * 0.75f, 0, 4);
    }

    // ---- 圆润卡通机甲风 ----
    void BuildGolem(float r)
    {
        Color dark = Darken(bodyColor, 0.45f);
        Color shell = Lighten(bodyColor, 0.16f);
        Add(SpriteFactory.SoftCircle(), new Color(0, 0, 0, 0.14f), 0, -r * 1.0f, r * 2.2f, r * 0.72f, 0, 3);

        Add(SpriteFactory.Circle(), dark, -r * 0.72f, -r * 0.26f, r * 0.62f, r * 0.62f, 0, 4);
        Add(SpriteFactory.Circle(), dark, r * 0.72f, -r * 0.26f, r * 0.62f, r * 0.62f, 0, 4);
        Add(SpriteFactory.Circle(), dark, -r * 0.45f, -r * 1.0f, r * 0.46f, r * 0.6f, 0, 4);
        Add(SpriteFactory.Circle(), dark, r * 0.45f, -r * 1.0f, r * 0.46f, r * 0.6f, 0, 4);

        Add(SpriteFactory.Circle(), bodyColor, 0, -r * 0.08f, r * 2.0f, r * 1.65f, 0, 5);
        Add(SpriteFactory.Circle(), shell, 0, r * 0.12f, r * 1.55f, r * 1.2f, 0, 6);
        Add(SpriteFactory.Circle(), shell, -r * 0.72f, r * 0.18f, r * 0.62f, r * 0.72f, 0, 6);
        Add(SpriteFactory.Circle(), shell, r * 0.72f, r * 0.18f, r * 0.62f, r * 0.72f, 0, 6);

        Add(SpriteFactory.Circle(), accentColor, 0, r * 0.98f, r * 1.18f, r * 0.92f, 0, 7);
        Add(SpriteFactory.Square(), Defs.DANGER, 0, r * 0.95f, r * 0.72f, r * 0.16f, 0, 8);
        Add(SpriteFactory.Circle(), eyeColor, -r * 0.18f, r * 0.96f, r * 0.1f, r * 0.1f, 0, 9);
        Add(SpriteFactory.Circle(), eyeColor, r * 0.18f, r * 0.96f, r * 0.1f, r * 0.1f, 0, 9);

        Add(SpriteFactory.Circle(), WithAlpha(Defs.CYAN, 0.45f), 0, r * 0.02f, r * 0.46f, r * 0.46f, 0, 7);
        Add(SpriteFactory.Circle(), WithAlpha(Color.white, 0.75f), 0, r * 0.02f, r * 0.2f, r * 0.2f, 0, 8);
        Add(SpriteFactory.Triangle(), accentColor, -r * 0.32f, r * 1.55f, r * 0.16f, r * 0.36f, -15f, 8);
        Add(SpriteFactory.Triangle(), accentColor, r * 0.32f, r * 1.55f, r * 0.16f, r * 0.36f, 15f, 8);
        Add(SpriteFactory.Square(), WithAlpha(Lighten(bodyColor, 0.25f), 0.4f), 0, -r * 0.3f, r * 1.1f, r * 0.08f, 0, 7);
    }

    // ---- 深渊巨龙 ----
    void BuildDragon(float r)
    {
        Color dark = Darken(bodyColor, 0.4f), belly = Lighten(accentColor, 0.2f);
        // 翅膀暗影
        Add(SpriteFactory.Triangle(), WithAlpha(dark, 0.5f), -r*0.6f, r*0.8f, r*2.2f, r*1.6f, 15, 3);
        Add(SpriteFactory.Triangle(), WithAlpha(dark, 0.5f),  r*0.6f, r*0.8f, r*2.2f, r*1.6f, -15, 3);
        // 尾巴
        Add(SpriteFactory.Triangle(), bodyColor, -r*1.3f, 0f, r*1.8f, r*0.8f, 90, 4);
        Add(SpriteFactory.Triangle(), dark, -r*1.8f, 0f, r*0.8f, r*0.5f, 90, 4);
        // 双腿
        Add(SpriteFactory.Square(), dark, -r*0.35f, -r*0.8f, r*0.35f, r*0.9f, 0, 4);
        Add(SpriteFactory.Square(), dark,  r*0.35f, -r*0.8f, r*0.35f, r*0.9f, 0, 4);
        // 主体
        Add(SpriteFactory.Circle(), bodyColor, 0, 0, r*2.0f, r*1.6f, 0, 5);
        // 胸腹
        Add(SpriteFactory.Circle(), belly, 0, -r*0.25f, r*1.3f, r*0.9f, 0, 6);
        // 背刺
        for (int i = 0; i < 5; i++)
        {
            float ox = (-0.6f + i * 0.3f) * r;
            float sz = (i == 2 ? 0.45f : 0.3f) * r;
            Add(SpriteFactory.Triangle(), accentColor, ox, r*0.7f, sz, sz * 1.2f, 0, 6);
        }
        // 头部
        Add(SpriteFactory.Circle(), bodyColor, r*1.0f, r*0.4f, r*1.1f, r*0.9f, 0, 7);
        // 额角
        Add(SpriteFactory.Triangle(), accentColor, r*0.8f, r*1.0f, r*0.3f, r*0.5f, -10, 8);
        Add(SpriteFactory.Triangle(), accentColor, r*1.2f, r*1.0f, r*0.3f, r*0.5f, 10, 8);
        // 下颌
        Add(SpriteFactory.Triangle(), Darken(bodyColor, 0.2f), r*1.4f, r*0.1f, r*0.8f, r*0.5f, -90, 7);
        // 眼睛
        Add(SpriteFactory.Circle(), eyeColor, r*1.15f, r*0.55f, r*0.28f, r*0.28f, 0, 9);
        Add(SpriteFactory.Circle(), new Color(0.08f,0.02f,0f), r*1.18f, r*0.55f, r*0.14f, r*0.18f, 0, 10);
        // 眼睛发光
        Add(SpriteFactory.SoftCircle(), WithAlpha(eyeColor, 0.35f), r*1.15f, r*0.55f, r*0.6f, r*0.6f, 0, 8);
        // 火焰口气（可选视觉暗示）
        Add(SpriteFactory.SoftCircle(), WithAlpha(Defs.AMBER, 0.15f), r*1.8f, r*0.2f, r*0.8f, r*0.5f, 0, 6);
    }

    public void Tick(float dt, Game game)
    {
        if (slowT > 0) slowT -= dt;
        if (flash > 0) flash -= dt;
        if (burn > 0) burn -= dt;

        var wpts = Defs.Waypoints;
        bool reversing = game.Cards != null && game.Cards.AreEnemiesReversed;
        bool frozen = game.Cards != null && game.Cards.AreEnemiesFrozen;
        float step = speed * (Slowed ? Defs.SLOW_FACTOR : 1f) * dt;

        if (reversing)
        {
            Vector2 target = wp <= 0 ? wpts[0] : wpts[wp];
            Vector2 delta = target - pos;
            if (delta.x > 0.01f) facing = 1f; else if (delta.x < -0.01f) facing = -1f;
            if (!frozen)
            {
                if (delta.magnitude <= step)
                {
                    pos = target;
                    if (wp > 0) wp--;
                    else { dead = true; return; }
                }
                else pos += delta.normalized * step;
            }
        }
        else
        {
            if (wp + 1 >= wpts.Length) { reached = true; dead = true; return; }
            Vector2 target = wpts[wp + 1];
            Vector2 delta = target - pos;
            if (delta.x > 0.01f) facing = 1f; else if (delta.x < -0.01f) facing = -1f;
            if (!frozen)
            {
                if (delta.magnitude <= step) { pos = target; wp++; }
                else pos += delta.normalized * step;
            }
        }
        transform.position = new Vector3(pos.x, pos.y, 0);

        // 动画
        float t = Time.time + tphase;
        if (kind == EnemyKind.Slime || kind == EnemyKind.Mini)
        {
            // 果冻感压缩回弹
            float stretch = 1f + Mathf.Sin(t * 4.6f) * 0.08f;
            float bob = Mathf.Abs(Mathf.Sin(t * 2.3f)) * 0.09f - 0.03f;
            vis.localScale = new Vector3(1f / stretch, stretch, 1f);
            vis.localPosition = new Vector3(0, bob, 0);
        }
        else if (kind == EnemyKind.Mech)
        {
            // 圆滚滚机甲左右轻晃
            float bob = Mathf.Sin(t * 3.2f) * 0.025f * radius * 3f;
            vis.localPosition = new Vector3(0, bob, 0);
            vis.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 2.2f) * 3.5f);
            vis.localScale = new Vector3(facing, 1f, 1f);
        }
        else
        {
            // 巨龙摆动
            float bob = Mathf.Sin(t*2f)*0.05f*radius*4f;
            vis.localPosition = new Vector3(0, bob, 0);
            vis.localScale = new Vector3(facing, 1f, 1f);
            vis.localRotation = Quaternion.identity;
        }

        // 受击闪白（更柔和的闪光效果）
        bool fl = flash > 0;
        for (int i = 0; i < parts.Count; i++)
        {
            if (fl)
                parts[i].color = Color.Lerp(baseCols[i], Color.white, 0.7f);
            else
                parts[i].color = baseCols[i];
        }

        slowAura.enabled = Slowed;

        // 血条更新
        float frac = Mathf.Clamp01(hp / maxHp);
        float barH = boss ? 0.12f : 0.07f;
        hpFill.transform.localScale = new Vector3(barW*frac, barH, 1);
        hpFill.transform.localPosition = new Vector3(-barW*(1-frac)/2f, radius + 0.22f, 0);
        hpFill.color = frac > 0.5f ? Defs.HP : (frac > 0.25f ? Defs.AMBER : Defs.DANGER);
    }

    public void Hurt(float raw, bool pierce = false, bool flashHit = true)
    {
        float dmg = pierce ? raw : Mathf.Max(1, raw - armor);
        if (Slowed) dmg *= Defs.FROST_VULN;
        hp -= dmg;
        if (flashHit) flash = 0.1f;
        if (hp <= 0) dead = true;
    }
}
