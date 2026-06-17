using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// WaveManager —— 增强版波次系统
//   · 15 波（原 10 波），波次 5 出现小 BOSS 铁甲精英
//   · 波次 10 出现铁甲编队
//   · 波次 15 终极 BOSS 深渊巨龙
//   · 波间奖励递增，击杀连击奖励
// =====================================================================
public class WaveManager
{
    public int wave = 0, total = 15;
    public bool active = false;
    public string wavePreview = "";       // 下一波预告
    Game game;
    Queue<(EnemyKind kind, float hp)> queue = new Queue<(EnemyKind, float)>();
    float timer = 0f;

    public WaveManager(Game g) { game = g; UpdatePreview(); }

    public void BuildWave(int n)
    {
        queue.Clear();
        float hp = Mathf.Pow(1.13f, n - 1);

        if (n == 15)
        {
            // 终极 BOSS 波：精英护卫 + 巨龙
            for (int i = 0; i < 6; i++) queue.Enqueue((EnemyKind.Mech, hp * 1.5f));
            for (int i = 0; i < 4; i++) queue.Enqueue((EnemyKind.Slime, hp));
            queue.Enqueue((EnemyKind.Dino, 1f));
            return;
        }

        int count = 4 + n * 2;

        if (n == 5 || n == 10)
        {
            // 精英波：大量机甲
            count = 6 + n;
            for (int i = 0; i < count; i++)
            {
                EnemyKind k = (i % 3 == 0) ? EnemyKind.Mech : EnemyKind.Slime;
                if (i % 5 == 2) k = EnemyKind.Mini;
                queue.Enqueue((k, hp * (n == 10 ? 1.3f : 1.1f)));
            }
            return;
        }

        for (int i = 0; i < count; i++)
        {
            EnemyKind k = EnemyKind.Slime;
            if (n >= 2 && i % 4 == 1) k = EnemyKind.Mini;
            if (n >= 4 && i % 5 == 0) k = EnemyKind.Mech;
            if (n >= 7 && i % 3 == 0) k = EnemyKind.Mech;
            if (n >= 12 && i % 2 == 0) k = EnemyKind.Mech;
            queue.Enqueue((k, hp));
        }
    }

    void UpdatePreview()
    {
        int next = wave + 1;
        if (next > total) { wavePreview = ""; return; }
        if (next == 15) wavePreview = "最终波！深渊巨龙来袭！";
        else if (next == 5) wavePreview = "精英波：机甲编队";
        else if (next == 10) wavePreview = "强化波：铁甲军团";
        else if (next >= 12) wavePreview = "后期波：大量重甲";
        else if (next >= 7) wavePreview = "混合编队";
        else if (next >= 4) wavePreview = "机甲混入";
        else wavePreview = "普通波次";
    }

    public bool Start()
    {
        if (active || wave >= total) return false;
        wave++; BuildWave(wave); timer = 0f; active = true;
        return true;
    }

    public void Tick(float dt)
    {
        if (!active) return;
        if (queue.Count > 0)
        {
            if (game.Cards != null && game.Cards.AreEnemiesFrozen) return;
            timer -= dt;
            if (timer <= 0f)
            {
                var s = queue.Dequeue();
                game.SpawnEnemy(s.kind, s.hp);
                timer = s.kind == EnemyKind.Dino ? 1.0f : 0.55f;
            }
        }
        else if (game.enemies.Count == 0)
        {
            active = false;
            game.OnWaveClear();
            UpdatePreview();
        }
    }
}
