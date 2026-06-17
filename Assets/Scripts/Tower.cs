using UnityEngine;

// =====================================================================
// Tower —— 防御塔
// 激光塔增强：直射范围内无目标时，自动尝试通过镜子反射命中远处敌人
// =====================================================================
public class Tower : MonoBehaviour
{
    public TowerKind kind;
    public TowerDef def;
    public int c, r, origC, origR, level = 1, invest;
    public Vector2 pos;
    public float range, dmg, rate, cool = 0f, angleDeg = -90f, recoil = 0f;
    public LaserResult beam;

    Transform barrelPivot;
    SpriteRenderer rangeRing, moveRing;
    SpriteRenderer baseSr;

    // 镜面反射寻敌缓存（降低每帧开销）
    float mirrorScanTimer = 0f;
    Vector2 cachedMirrorDir;
    bool hasMirrorTarget = false;

    public void Init(TowerKind k, int c, int r)
    {
        kind = k; def = Defs.Towers[k];
        this.c = c; this.r = r; origC = c; origR = r;
        pos = new Vector2(c + 0.5f, r + 0.5f);
        transform.position = new Vector3(pos.x, pos.y, 0);
        range = def.range; dmg = def.dmg; rate = def.rate; invest = def.cost;

        rangeRing = SpriteFactory.Spawn("range", SpriteFactory.Ring(128, 0.015f),
            new Color(Defs.CYAN.r, Defs.CYAN.g, Defs.CYAN.b, 0.4f), 3, transform);
        rangeRing.transform.localScale = Vector3.one * (range * 2f);
        rangeRing.enabled = false;

        if (def.movable)
        {
            moveRing = SpriteFactory.Spawn("moveRange", SpriteFactory.Ring(128, 0.01f),
                new Color(Defs.AMBER.r, Defs.AMBER.g, Defs.AMBER.b, 0.25f), 2, transform);
            moveRing.transform.localScale = Vector3.one * (def.moveRange * 2f + 1f);
            moveRing.enabled = false;
        }

        baseSr = SpriteFactory.Spawn("base", SpriteFactory.Circle(),
            new Color(Defs.CARD_BG.r, Defs.CARD_BG.g, Defs.CARD_BG.b, 0.95f), 11, transform);
        baseSr.transform.localScale = Vector3.one * 0.78f;

        var ring = SpriteFactory.Spawn("basering", SpriteFactory.Ring(64, 0.08f), def.color, 12, transform);
        ring.transform.localScale = Vector3.one * 0.82f;

        var glow = SpriteFactory.Spawn("baseglow", SpriteFactory.SoftCircle(),
            new Color(def.color.r, def.color.g, def.color.b, 0.12f), 10, transform);
        glow.transform.localScale = Vector3.one * 1.2f;

        barrelPivot = new GameObject("barrelPivot").transform;
        barrelPivot.SetParent(transform, false);
        var barrel = SpriteFactory.Spawn("barrel", SpriteFactory.Square(), def.color, 13, barrelPivot);
        barrel.transform.localPosition = new Vector3(0.3f, 0, 0);
        barrel.transform.localScale = new Vector3(0.55f, kind == TowerKind.Cannon ? 0.22f : 0.14f, 1f);

        var tip = SpriteFactory.Spawn("tip", SpriteFactory.Circle(),
            Color.Lerp(def.color, Color.white, 0.3f), 14, barrelPivot);
        tip.transform.localPosition = new Vector3(0.55f, 0, 0);
        tip.transform.localScale = Vector3.one * (kind == TowerKind.Cannon ? 0.18f : 0.10f);

        var head = SpriteFactory.Spawn("head", SpriteFactory.Circle(),
            new Color(Defs.BG.r, Defs.BG.g, Defs.BG.b, 0.95f), 14, transform);
        head.transform.localScale = Vector3.one * 0.28f;
        var headring = SpriteFactory.Spawn("headring", SpriteFactory.Ring(64, 0.15f), def.color, 15, transform);
        headring.transform.localScale = Vector3.one * 0.30f;

        if (def.movable)
        {
            var moveIcon = SpriteFactory.Spawn("moveIcon", SpriteFactory.Diamond(),
                new Color(Defs.AMBER.r, Defs.AMBER.g, Defs.AMBER.b, 0.4f), 16, transform);
            moveIcon.transform.localPosition = new Vector3(0, -0.42f, 0);
            moveIcon.transform.localScale = Vector3.one * 0.12f;
        }
    }

    public void SetSelected(bool s)
    {
        if (rangeRing) rangeRing.enabled = s;
        if (moveRing) moveRing.enabled = s;
    }

    public int UpgradeCost() => Mathf.RoundToInt(def.cost * 0.8f * level);
    public bool CanUpgrade() => level < 3;
    public void Upgrade()
    {
        invest += UpgradeCost(); level++;
        dmg = Mathf.Round(dmg * 1.6f); range = range * 1.12f;
        if (rate > 0) rate *= 0.88f;
        rangeRing.transform.localScale = Vector3.one * (range * 2f);
        if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Upgrade);
    }
    public int SellValue() => Mathf.RoundToInt(invest * 0.6f);

    public bool CanMoveTo(int nc, int nr, Game game)
    {
        if (!def.movable) return false;
        int dist = Mathf.Abs(nc - origC) + Mathf.Abs(nr - origR);
        if (dist > def.moveRange) return false;
        if (!game.GetMap().Buildable(nc, nr)) return false;
        if (game.Occupied(nc, nr) && !(nc == c && nr == r)) return false;
        return true;
    }

    public void MoveTo(int nc, int nr)
    {
        c = nc; r = nr;
        pos = new Vector2(c + 0.5f, r + 0.5f);
        transform.position = new Vector3(pos.x, pos.y, 0);
        if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Place);
    }

    // ---- 直接射程内选敌 ----
    Enemy PickTarget(Game game, float scanRange)
    {
        Enemy best = null; float bestProg = -1f;
        foreach (var e in game.enemies)
        {
            if (e.dead) continue;
            if ((e.pos - pos).magnitude <= scanRange)
            {
                int ni = Mathf.Min(e.wp + 1, Defs.Waypoints.Length - 1);
                float prog = e.wp * 1000f - (Defs.Waypoints[ni] - e.pos).magnitude;
                if (prog > bestProg) { bestProg = prog; best = e; }
            }
        }
        return best;
    }

    // ---- 镜面反射寻敌：尝试朝每面镜子发射，看反射后能否命中怪物 ----
    bool TryFindMirrorBounce(Game game, out Vector2 bestDir)
    {
        bestDir = Vector2.zero;
        if (game.mirrors.Count == 0 || game.enemies.Count == 0)
            return false;

        int bestHits = 0;
        float bestProgress = -1f;

        foreach (var m in game.mirrors)
        {
            // 朝镜子中心射
            Vector2 toMirror = m.pos - pos;
            if (toMirror.sqrMagnitude < 0.01f) continue;

            // 模拟光线追踪
            LaserResult test = LaserSystem.Trace(pos, toMirror, game);

            if (test.hits.Count > 0)
            {
                // 计算命中的最前方敌人的进度
                float topProg = -1f;
                foreach (var e in test.hits)
                {
                    if (e.dead) continue;
                    int ni = Mathf.Min(e.wp + 1, Defs.Waypoints.Length - 1);
                    float prog = e.wp * 1000f - (Defs.Waypoints[ni] - e.pos).magnitude;
                    if (prog > topProg) topProg = prog;
                }

                // 选命中数最多的路径，或进度最高的
                if (test.hits.Count > bestHits ||
                    (test.hits.Count == bestHits && topProg > bestProgress))
                {
                    bestHits = test.hits.Count;
                    bestProgress = topProg;
                    bestDir = toMirror;
                }
            }
        }

        return bestHits > 0;
    }

    // ---- 主循环 ----
    public void Tick(float dt, Game game)
    {
        if (cool > 0) cool -= dt;
        if (recoil > 0) recoil -= dt * 4f;
        float rangeMul = (kind == TowerKind.Laser && game.Cards != null) ? game.Cards.LaserRangeMultiplier : 1f;
        float dmgMul = (kind == TowerKind.Laser && game.Cards != null) ? game.Cards.LaserDamageMultiplier : 1f;
        float rateMul = game.Cards != null ? game.Cards.FireRateMultiplier : 1f;
        float effectiveRange = range * rangeMul;
        float effectiveDamage = dmg * dmgMul;
        float effectiveRate = rate / Mathf.Max(0.01f, rateMul);
        Enemy t = PickTarget(game, effectiveRange);
        beam = null;

        if (kind == TowerKind.Laser)
        {
            if (t != null)
            {
                // 优先直接射击范围内目标
                Aim(t);
                beam = LaserSystem.Trace(pos, t.pos - pos, game);
                hasMirrorTarget = false;
            }
            else if (game.enemies.Count > 0)
            {
                // 范围内无目标 → 尝试通过镜子反射命中
                mirrorScanTimer -= dt;
                if (mirrorScanTimer <= 0f)
                {
                    mirrorScanTimer = 0.15f;  // 每 0.15 秒扫描一次，节省性能
                    Vector2 dir;
                    hasMirrorTarget = TryFindMirrorBounce(game, out dir);
                    if (hasMirrorTarget) cachedMirrorDir = dir;
                }

                if (hasMirrorTarget)
                {
                    // 瞄准镜子方向
                    angleDeg = Mathf.Atan2(cachedMirrorDir.y, cachedMirrorDir.x) * Mathf.Rad2Deg;
                    beam = LaserSystem.Trace(pos, cachedMirrorDir, game);
                }
            }
            else
            {
                hasMirrorTarget = false;
            }

            // 处理激光伤害
            if (beam != null)
            {
                foreach (var e in beam.hits)
                {
                    e.Hurt(effectiveDamage * dt, pierce: true, flashHit: false);
                    e.burn = 0.12f;
                    if (e.dead && !e.reached) game.OnKill(e);
                }
            }
        }
        else if (t != null)
        {
            Aim(t);
            if (cool <= 0f)
            {
                cool = effectiveRate; recoil = 1f;
                game.SpawnProjectile(this, t);
                if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Shoot, 0.4f);
            }
        }

        if (barrelPivot)
        {
            barrelPivot.localRotation = Quaternion.Euler(0, 0, angleDeg);
            float rec = recoil > 0 ? recoil * 0.12f : 0f;
            barrelPivot.localPosition = new Vector3(-Mathf.Cos(angleDeg * Mathf.Deg2Rad) * rec, -Mathf.Sin(angleDeg * Mathf.Deg2Rad) * rec, 0);
        }
    }

    void Aim(Enemy t)
    {
        Vector2 dir = t.pos - pos;
        angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }
}
