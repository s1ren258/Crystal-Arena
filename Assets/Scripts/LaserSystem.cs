using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// LaserSystem —— 激光光线追踪
//   · 反射：碰到镜子 / 怪物，按 r = d - 2(d·n)n（Vector2.Reflect）改变方向
//   · 折射：进入棱镜按 Snell 定律弯折（GLSL refract 公式）
//   · 分光：棱镜对三种"波长"取不同折射率，分裂出红/绿/蓝三束发散光（色散）
// 返回所有光段（用于 LineRenderer 绘制）与被命中的怪物列表。
// =====================================================================
public struct LaserSegment { public Vector2 a, b; public Color color; }

public class LaserResult
{
    public List<LaserSegment> segments = new List<LaserSegment>();
    public List<Enemy> hits = new List<Enemy>();
}

struct Beam { public Vector2 o, d; public float remaining; public int bounces; public Color color; }

public static class LaserSystem
{
    const float EPS = 0.02f;
    const int SEG_CAP = 48;

    public static LaserResult Trace(Vector2 origin, Vector2 dir, Game game)
    {
        var res = new LaserResult();
        if (dir.sqrMagnitude < 1e-6f) return res;

        var stack = new Stack<Beam>();
        stack.Push(new Beam { o = origin, d = dir.normalized, remaining = Defs.LASER_MAX_LEN, bounces = 0, color = new Color(1f, 0.85f, 0.9f) });

        while (stack.Count > 0 && res.segments.Count < SEG_CAP)
        {
            Beam beam = stack.Pop();
            if (beam.bounces > Defs.LASER_MAX_BOUNCE) continue;

            float bestT = beam.remaining;
            int kind = -1;             // 0=mirror 1=enemy 2=prism
            Mirror hitMir = null; Enemy hitEnemy = null; Prism hitPrism = null;

            // 镜子（线段）
            foreach (var m in game.mirrors)
            {
                Vector2 ma, mb;
                m.Endpoints(out ma, out mb);
                float t = RaySegment(beam.o, beam.d, ma, mb);
                if (t > 0 && t < bestT) { bestT = t; kind = 0; hitMir = m; }
            }
            // 怪物（圆）
            foreach (var e in game.enemies)
            {
                if (e.dead) continue;
                float t = RayCircle(beam.o, beam.d, e.pos, e.radius);
                if (t > 0 && t < bestT) { bestT = t; kind = 1; hitEnemy = e; }
            }
            // 棱镜（圆）
            foreach (var p in game.prisms)
            {
                float t = RayCircle(beam.o, beam.d, p.pos, Defs.PRISM_RADIUS);
                if (t > 0 && t < bestT) { bestT = t; kind = 2; hitPrism = p; }
            }

            if (kind == -1)
            {
                res.segments.Add(new LaserSegment { a = beam.o, b = beam.o + beam.d * beam.remaining, color = beam.color });
                continue;
            }

            Vector2 point = beam.o + beam.d * bestT;
            res.segments.Add(new LaserSegment { a = beam.o, b = point, color = beam.color });
            float rem = beam.remaining - bestT;
            if (rem <= 0) continue;

            if (kind == 1)   // 怪物：反弹
            {
                res.hits.Add(hitEnemy);
                Vector2 n = (point - hitEnemy.pos).normalized;
                if (n.sqrMagnitude < 1e-6f) n = -beam.d;
                Vector2 nd = Vector2.Reflect(beam.d, n);
                stack.Push(new Beam { o = point + nd * EPS, d = nd, remaining = rem, bounces = beam.bounces + 1, color = beam.color });
            }
            else if (kind == 0)   // 镜子：反射
            {
                Vector2 nd = Vector2.Reflect(beam.d, hitMir.Normal());
                stack.Push(new Beam { o = point + nd * EPS, d = nd, remaining = rem, bounces = beam.bounces + 1, color = beam.color });
            }
            else                  // 棱镜：折射 + 色散分光
            {
                Vector2 n = (point - hitPrism.pos).normalized;
                for (int i = 0; i < 3; i++)
                {
                    Vector2 rd = Refract(beam.d, n, Prism.Eta[i]);
                    if (rd.sqrMagnitude < 1e-6f) rd = Vector2.Reflect(beam.d, n);  // 全反射兜底
                    Vector2 start = hitPrism.pos + rd * (Defs.PRISM_RADIUS + 0.03f);
                    Color col = Color.Lerp(beam.color, Prism.SpectrumColor[i], 0.85f);
                    stack.Push(new Beam { o = start, d = rd, remaining = Mathf.Max(0, rem - Defs.PRISM_RADIUS), bounces = beam.bounces + 1, color = col });
                }
            }
        }
        return res;
    }

    // GLSL 风格折射：i 入射方向(单位)，n 表面法线，eta 折射率比 n1/n2
    static Vector2 Refract(Vector2 i, Vector2 n, float eta)
    {
        float ndoti = Vector2.Dot(n, i);
        if (ndoti > 0) { n = -n; ndoti = -ndoti; }          // 使法线朝向入射侧
        float k = 1f - eta * eta * (1f - ndoti * ndoti);
        if (k < 0f) return Vector2.zero;                     // 全内反射
        return (eta * i - (eta * ndoti + Mathf.Sqrt(k)) * n).normalized;
    }

    static float RayCircle(Vector2 o, Vector2 d, Vector2 c, float r)
    {
        Vector2 f = o - c;
        float b = 2f * Vector2.Dot(f, d);
        float cc = Vector2.Dot(f, f) - r * r;
        float disc = b * b - 4f * cc;
        if (disc < 0f) return -1f;
        float sq = Mathf.Sqrt(disc);
        float t1 = (-b - sq) / 2f, t2 = (-b + sq) / 2f;
        if (t1 > 0.01f) return t1;
        if (t2 > 0.01f) return t2;
        return -1f;
    }

    static float RaySegment(Vector2 o, Vector2 d, Vector2 a, Vector2 b)
    {
        Vector2 v1 = o - a, v2 = b - a, v3 = new Vector2(-d.y, d.x);
        float denom = Vector2.Dot(v2, v3);
        if (Mathf.Abs(denom) < 1e-9f) return -1f;
        float t = (v2.x * v1.y - v2.y * v1.x) / denom;
        float s = Vector2.Dot(v1, v3) / denom;
        if (t > 0.01f && s >= 0f && s <= 1f) return t;
        return -1f;
    }
}
