using UnityEngine;

// =====================================================================
// Projectile —— 增强版子弹：拖尾效果、加农炮弹更大、冰冻弹发光
// =====================================================================
public class Projectile : MonoBehaviour
{
    public Vector2 pos;
    public Enemy target;
    public float speed, dmg, splash;
    public bool slow;
    public Color color;
    public bool dead = false;
    Game game;

    SpriteRenderer trailSr;
    Vector2 prevPos;

    public void Init(Tower tower, Enemy t, Game g)
    {
        game = g; target = t; pos = tower.pos; prevPos = pos;
        float projMul = (game.Cards != null) ? game.Cards.ProjectileSpeedMultiplier : 1f;
        speed = tower.def.bullet * projMul; dmg = tower.dmg; splash = tower.def.splash;
        slow = tower.def.slow; color = tower.def.color;
        transform.position = new Vector3(pos.x, pos.y, 0);

        float bulletSize = splash > 0 ? 0.22f : (slow ? 0.18f : 0.14f);
        var sr = SpriteFactory.Spawn("bullet", SpriteFactory.Circle(), color, 20, transform);
        sr.transform.localScale = Vector3.one * bulletSize;

        // 拖尾光晕
        Color trailCol = new Color(color.r, color.g, color.b, 0.25f);
        trailSr = SpriteFactory.Spawn("trail", SpriteFactory.SoftCircle(), trailCol, 19, transform);
        trailSr.transform.localScale = Vector3.one * (bulletSize * 2.5f);

        // 冰冻弹外圈
        if (slow)
        {
            var frost = SpriteFactory.Spawn("frost", SpriteFactory.Ring(64, 0.15f),
                new Color(Defs.FROST.r, Defs.FROST.g, Defs.FROST.b, 0.4f), 21, transform);
            frost.transform.localScale = Vector3.one * 0.26f;
        }
    }

    public void Tick(float dt)
    {
        if (target == null || target.dead) { dead = true; return; }
        prevPos = pos;
        Vector2 to = target.pos - pos;
        float step = speed * dt;
        if (to.magnitude <= step) { Hit(); dead = true; }
        else
        {
            pos += to.normalized * step;
            transform.position = new Vector3(pos.x, pos.y, 0);

            // 拖尾朝运动方向拉伸
            Vector2 dir = pos - prevPos;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                trailSr.transform.localRotation = Quaternion.Euler(0, 0, angle);
                trailSr.transform.localScale = new Vector3(dir.magnitude * 6f + 0.2f, 0.2f, 1f);
            }
        }
    }

    void Hit()
    {
        if (splash > 0)
        {
            game.SpawnExplosion(target.pos, color, 20);
            foreach (var e in game.enemies)
                if (!e.dead && (e.pos - target.pos).magnitude <= splash) Apply(e);
        }
        else { Apply(target); game.SpawnHit(target.pos, color); }
    }

    void Apply(Enemy e)
    {
        if (slow) e.slowT = Defs.SLOW_TIME;
        e.Hurt(dmg);
        if (e.dead && !e.reached) game.OnKill(e);
    }
}
