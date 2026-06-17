using UnityEngine;

// =====================================================================
// ParticleBit —— 轻量粒子：带初速度，逐帧衰减并淡出后自毁
// =====================================================================
public class ParticleBit : MonoBehaviour
{
    Vector2 pos, vel;
    float life, maxLife, size;
    SpriteRenderer sr;
    public bool dead = false;

    public void Init(Vector2 p, Color color, float angle, float speed, float lifeT, float sz)
    {
        pos = p; vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        life = maxLife = lifeT; size = sz;
        transform.position = new Vector3(p.x, p.y, 0);
        sr = SpriteFactory.Spawn("p", SpriteFactory.Circle(), color, 22, transform);
        sr.transform.localScale = Vector3.one * size;
    }

    public void Tick(float dt)
    {
        pos += vel * dt; vel *= 0.9f; life -= dt;
        if (life <= 0) { dead = true; return; }
        transform.position = new Vector3(pos.x, pos.y, 0);
        float a = Mathf.Clamp01(life / maxLife);
        var c = sr.color; c.a = a; sr.color = c;
        sr.transform.localScale = Vector3.one * (size * a);
    }
}
