using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// Game —— 主控：现代化 UI、音效集成、炮台移动、Toast 通知
// 修复：所有 GUIStyle 使用显式赋值避免嵌套初始化器编译错误
// =====================================================================
public enum BuildSel { None, Arrow, Cannon, Frost, Laser, Mirror, Prism }

public class Game : MonoBehaviour
{
    public static Game Instance;

    public List<Enemy> enemies = new List<Enemy>();
    public List<Tower> towers = new List<Tower>();
    public List<Mirror> mirrors = new List<Mirror>();
    public List<Prism> prisms = new List<Prism>();
    public List<Projectile> projectiles = new List<Projectile>();
    List<ParticleBit> particles = new List<ParticleBit>();

    public int lives, gold, score, speed = 1;
    public string state = "ready";
    GameMap map;
    WaveManager waves;

    BuildSel sel = BuildSel.None;
    Tower selTower; Mirror selMirror; Prism selPrism;
    Tower movingTower;

    Camera cam;
    MusicManager music;
    CardSystem cards;
    Font uiFont;
    GUIStyle stLabel, stSmall, stBig, stBtn, stBtnSel, stTitle, stToast, stDesc, stHotkey;
    bool guiReady = false;
    float panelFrac = 0.28f;
    float musicVolume = 0.58f;
    Vector2 selectionScroll;

    Vector3 camBasePos;
    float shakeTimer;
    float shakeDur;
    float shakeAmp;
    float hitstopTimer;

    Transform laserHolder;
    List<LineRenderer> laserPool = new List<LineRenderer>();
    SpriteRenderer hover;
    List<SpriteRenderer> moveHighlights = new List<SpriteRenderer>();

    // Toast
    string toastMsg = "";
    float toastTimer = 0f;
    Color toastColor = Color.white;

    // Combo
    int combo = 0;
    float comboTimer = 0f;
    int bestCombo = 0;

    // 本局统计
    int totalKills;
    int wavesCleared;
    int towersBuilt;
    int towersUpgraded;
    int towersSold;
    int mirrorsBuilt;
    int prismsBuilt;
    int cardsPickedCount;
    int damageTaken;
    int damageBlocked;
    int totalGoldEarned;
    int totalGoldSpent;
    bool showGuide;

    // Textures
    Texture2D texWhite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Object.FindObjectOfType<Game>() == null)
            new GameObject("Game").AddComponent<Game>();
    }

    void Awake()
    {
        Instance = this;
        if (!GetComponent<SfxManager>()) gameObject.AddComponent<SfxManager>();
        music = GetComponent<MusicManager>();
        if (!music) music = gameObject.AddComponent<MusicManager>();
        music.SetVolume(musicVolume);

        SetupCamera();
        map = new GameMap();
        map.BuildBackground();
        laserHolder = new GameObject("Lasers").transform;

        hover = SpriteFactory.Spawn("hover", SpriteFactory.Square(), new Color(0.2f, 0.85f, 0.6f, 0.25f), 2);
        hover.transform.localScale = Vector3.one * 0.96f;
        hover.enabled = false;

        for (int i = 0; i < 50; i++)
        {
            var mh = SpriteFactory.Spawn("mh", SpriteFactory.Square(), new Color(Defs.AMBER.r, Defs.AMBER.g, Defs.AMBER.b, 0.12f), 1);
            mh.transform.localScale = Vector3.one * 0.92f;
            mh.enabled = false;
            moveHighlights.Add(mh);
        }

        uiFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "PingFang SC", "Heiti SC", "STHeiti", "SimHei",
                    "Noto Sans CJK SC", "WenQuanYi Micro Hei", "Arial" }, 16);

        texWhite = Texture2D.whiteTexture;
        Reset();
    }

    public GameMap GetMap() { return map; }
    public CardSystem Cards => cards;

    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); }
        EnsureAudioListener();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Defs.BG;
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        float worldW = Defs.COLS / (1f - panelFrac);
        float sizeByW = (worldW / 2f) / aspect;
        float sizeByH = Defs.ROWS / 2f + 0.6f;
        cam.orthographicSize = Mathf.Max(sizeByW, sizeByH);
        cam.transform.position = new Vector3(worldW / 2f, Defs.ROWS / 2f, -10f);
        camBasePos = cam.transform.position;
    }

    void Reset()
    {
        foreach (var e in enemies) if (e) Destroy(e.gameObject);
        foreach (var t in towers) if (t) Destroy(t.gameObject);
        foreach (var m in mirrors) if (m) Destroy(m.gameObject);
        foreach (var p in prisms) if (p) Destroy(p.gameObject);
        foreach (var b in projectiles) if (b) Destroy(b.gameObject);
        foreach (var pa in particles) if (pa) Destroy(pa.gameObject);
        enemies.Clear(); towers.Clear(); mirrors.Clear(); prisms.Clear();
        projectiles.Clear(); particles.Clear();
        lives = 20; gold = 260; score = 0; speed = 1;
        waves = new WaveManager(this);
        cards = new CardSystem(this);
        sel = BuildSel.None; selTower = null; selMirror = null; selPrism = null;
        movingTower = null;
        combo = 0; comboTimer = 0f;
        bestCombo = 0;
        totalKills = 0; wavesCleared = 0; towersBuilt = 0; towersUpgraded = 0; towersSold = 0;
        mirrorsBuilt = 0; prismsBuilt = 0; cardsPickedCount = 0; damageTaken = 0; damageBlocked = 0;
        totalGoldEarned = 260; totalGoldSpent = 0;
        showGuide = true;
        state = "ready";
    }

    void ShowToast(string msg, Color c)
    {
        toastMsg = msg;
        toastTimer = 2.2f;
        toastColor = c;
    }
    void ShowToast(string msg) { ShowToast(msg, Defs.INK); }
    public void Notify(string msg, Color c) { ShowToast(msg, c); }
    public void Notify(string msg) { ShowToast(msg); }

    // ================= 辅助：安全创建 GUIStyle =================
    static GUIStyle MakeStyle(GUIStyle baseStyle, Font f, int size, Color textCol,
        FontStyle fs = FontStyle.Normal, TextAnchor align = TextAnchor.UpperLeft,
        bool wrap = false, bool overflow = false)
    {
        var s = new GUIStyle(baseStyle);
        s.font = f; s.fontSize = size; s.fontStyle = fs; s.alignment = align;
        s.wordWrap = wrap;
        if (overflow) s.clipping = TextClipping.Overflow;
        s.normal.textColor = textCol;
        return s;
    }

    // ================= 主循环 =================
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f) * speed;
        if (hitstopTimer > 0f)
        {
            hitstopTimer = Mathf.Max(0f, hitstopTimer - Time.unscaledDeltaTime);
            dt *= 0.18f;
        }
        EnsureAudioListener();
        map.Tick(Time.time);
        SyncMusicState();
        if (cards != null) cards.Tick(dt, state == "playing" && waves.active && !cards.HasDraft);
        if (toastTimer > 0) toastTimer -= Time.deltaTime;
        if (comboTimer > 0) { comboTimer -= Time.deltaTime; if (comboTimer <= 0) combo = 0; }
        if (cards == null || !cards.HasDraft) HandleInput();
        else hover.enabled = false;

        if (state == "playing")
        {
            waves.Tick(dt);
            foreach (var t in towers) t.Tick(dt, this);
            foreach (var b in projectiles) b.Tick(dt);
            foreach (var e in enemies) { e.Tick(dt, this); if (e.reached) LoseLife(e); }
            foreach (var pa in particles) pa.Tick(dt);
            Cleanup();
        }
        RenderLasers();
        UpdateMoveHighlights();
        UpdateCameraFeedback();
    }

    void UpdateCameraFeedback()
    {
        if (cam == null) return;
        if (shakeTimer > 0f)
        {
            shakeTimer = Mathf.Max(0f, shakeTimer - Time.unscaledDeltaTime);
            float p = shakeDur <= 0.001f ? 0f : (shakeTimer / shakeDur);
            float a = shakeAmp * p;
            float ox = (Random.value * 2f - 1f) * a;
            float oy = (Random.value * 2f - 1f) * a;
            cam.transform.position = camBasePos + new Vector3(ox, oy, 0f);
        }
        else
        {
            cam.transform.position = camBasePos;
        }
    }

    void ApplyShake(float amp, float dur)
    {
        shakeAmp = Mathf.Max(shakeAmp, amp);
        shakeDur = Mathf.Max(shakeDur, dur);
        shakeTimer = Mathf.Max(shakeTimer, dur);
    }

    void ApplyHitstop(float dur)
    {
        hitstopTimer = Mathf.Max(hitstopTimer, dur);
    }

    public void OnCardPicked(Color c)
    {
        cardsPickedCount++;
        ApplyHitstop(0.06f);
        ApplyShake(0.18f, 0.20f);
    }

    void EnsureAudioListener()
    {
        if (FindObjectOfType<AudioListener>() != null) return;
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            cam.gameObject.AddComponent<AudioListener>();
            return;
        }

        if (!GetComponent<AudioListener>())
            gameObject.AddComponent<AudioListener>();
    }

    void SyncMusicState()
    {
        if (!music) return;
        bool bossNow = false;
        foreach (var e in enemies)
            if (e != null && e.boss && !e.dead) { bossNow = true; break; }
        music.SetBossMode(bossNow);
    }

    void Cleanup()
    {
        for (int i = enemies.Count - 1; i >= 0; i--) if (enemies[i].dead) { Destroy(enemies[i].gameObject); enemies.RemoveAt(i); }
        for (int i = projectiles.Count - 1; i >= 0; i--) if (projectiles[i].dead) { Destroy(projectiles[i].gameObject); projectiles.RemoveAt(i); }
        for (int i = particles.Count - 1; i >= 0; i--) if (particles[i].dead) { Destroy(particles[i].gameObject); particles.RemoveAt(i); }
    }

    // ================= 输入 =================
    bool PointerOverPanel() { return Input.mousePosition.x >= Screen.width * (1f - panelFrac); }

    void HandleInput()
    {
        if (selMirror != null)
        {
            float sc = Input.mouseScrollDelta.y;
            if (Mathf.Abs(sc) > 0.01f) { selMirror.Rotate(sc * 8f); if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Reflect, 0.3f); }
            if (Input.GetKey(KeyCode.Q)) selMirror.Rotate(90f * Time.deltaTime);
            if (Input.GetKey(KeyCode.E)) selMirror.Rotate(-90f * Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.R)) { selMirror.Rotate(15f); if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Reflect, 0.3f); }
        }
        if (Input.GetKeyDown(KeyCode.Space)) StartWave();

        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectBuild(BuildSel.Laser);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectBuild(BuildSel.Arrow);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectBuild(BuildSel.Cannon);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectBuild(BuildSel.Frost);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SelectBuild(BuildSel.Mirror);
        if (Input.GetKeyDown(KeyCode.Alpha6)) SelectBuild(BuildSel.Prism);
        if (Input.GetKeyDown(KeyCode.H)) showGuide = !showGuide;
        if (Input.GetKeyDown(KeyCode.Escape)) { sel = BuildSel.None; movingTower = null; ClearSel(); }

        if (Input.GetKeyDown(KeyCode.M) && selTower != null && selTower.def.movable)
        {
            movingTower = selTower;
            sel = BuildSel.None;
            ShowToast("点击目标格移动炮台（范围 " + selTower.def.moveRange + " 格）", Defs.AMBER);
        }

        UpdateHover();

        if (Input.GetMouseButtonDown(0) && state == "playing" && !PointerOverPanel())
        {
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
            int cc = Mathf.FloorToInt(w.x), rr = Mathf.FloorToInt(w.y);
            if (movingTower != null) TryMoveTower(cc, rr);
            else FieldClick(cc, rr);
        }

        if (Input.GetMouseButtonDown(1)) { sel = BuildSel.None; movingTower = null; ClearSel(); }
    }

    void SelectBuild(BuildSel s) { movingTower = null; sel = (sel == s) ? BuildSel.None : s; }

    void TryMoveTower(int nc, int nr)
    {
        if (movingTower == null) return;
        if (movingTower.CanMoveTo(nc, nr, this))
        {
            movingTower.MoveTo(nc, nr);
            movingTower = null;
            ShowToast("炮台已移动", Defs.HP);
        }
        else
        {
            if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Error, 0.5f);
            ShowToast("无法移动到此位置", Defs.DANGER);
        }
    }

    void UpdateMoveHighlights()
    {
        int idx = 0;
        if (movingTower != null)
        {
            int mr = movingTower.def.moveRange;
            for (int dr = -mr; dr <= mr; dr++)
                for (int dc = -mr; dc <= mr; dc++)
                {
                    if (Mathf.Abs(dr) + Mathf.Abs(dc) > mr) continue;
                    int nc = movingTower.origC + dc, nr = movingTower.origR + dr;
                    if (nc < 0 || nr < 0 || nc >= Defs.COLS || nr >= Defs.ROWS) continue;
                    bool canMove = movingTower.CanMoveTo(nc, nr, this);
                    if (idx < moveHighlights.Count)
                    {
                        moveHighlights[idx].enabled = true;
                        moveHighlights[idx].transform.position = new Vector3(nc + 0.5f, nr + 0.5f, 0);
                        moveHighlights[idx].color = canMove
                            ? new Color(Defs.HP.r, Defs.HP.g, Defs.HP.b, 0.18f)
                            : new Color(Defs.DANGER.r, Defs.DANGER.g, Defs.DANGER.b, 0.08f);
                        idx++;
                    }
                }
        }
        for (int i = idx; i < moveHighlights.Count; i++) moveHighlights[i].enabled = false;
    }

    void UpdateHover()
    {
        if (state == "playing" && (sel != BuildSel.None || movingTower != null) && !PointerOverPanel())
        {
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
            int c = Mathf.FloorToInt(w.x), r = Mathf.FloorToInt(w.y);
            if (c >= 0 && r >= 0 && c < Defs.COLS && r < Defs.ROWS)
            {
                bool ok = movingTower != null
                    ? movingTower.CanMoveTo(c, r, this)
                    : (map.Buildable(c, r) && !Occupied(c, r) && gold >= CostOf(sel));
                hover.enabled = true;
                hover.transform.position = new Vector3(c + 0.5f, r + 0.5f, 0);
                hover.color = ok ? new Color(0.2f, 0.85f, 0.6f, 0.3f) : new Color(0.95f, 0.45f, 0.45f, 0.3f);
                return;
            }
        }
        hover.enabled = false;
    }

    public bool Occupied(int c, int r)
    {
        foreach (var t in towers) if (t.c == c && t.r == r) return true;
        foreach (var m in mirrors) if (m.c == c && m.r == r) return true;
        foreach (var p in prisms) if (p.c == c && p.r == r) return true;
        return false;
    }

    int CostOf(BuildSel s)
    {
        if (s == BuildSel.Mirror) return Defs.MIRROR_COST;
        if (s == BuildSel.Prism) return Defs.PRISM_COST;
        return Defs.Towers[(TowerKind)((int)s - 1)].cost;
    }

    void ClearSel()
    {
        if (selTower) selTower.SetSelected(false);
        if (selMirror) selMirror.SetSelected(false);
        if (selPrism) selPrism.SetSelected(false);
        selTower = null; selMirror = null; selPrism = null;
        selectionScroll = Vector2.zero;
    }

    void FieldClick(int c, int r)
    {
        foreach (var t in towers) if (t.c == c && t.r == r) { ClearSel(); selTower = t; t.SetSelected(true); sel = BuildSel.None; return; }
        foreach (var m in mirrors) if (m.c == c && m.r == r) { ClearSel(); selMirror = m; m.SetSelected(true); sel = BuildSel.None; return; }
        foreach (var p in prisms) if (p.c == c && p.r == r) { ClearSel(); selPrism = p; p.SetSelected(true); sel = BuildSel.None; return; }

        if (sel != BuildSel.None && map.Buildable(c, r) && !Occupied(c, r) && gold >= CostOf(sel))
        {
            int cost = CostOf(sel);
            gold -= cost;
            totalGoldSpent += cost;
            ClearSel();
            if (sel == BuildSel.Mirror)
            {
                var m = new GameObject("Mirror").AddComponent<Mirror>(); m.Init(c, r);
                mirrors.Add(m); selMirror = m; m.SetSelected(true);
                SpawnUpgradeFx(m.pos, Defs.GLASS);
                mirrorsBuilt++;
            }
            else if (sel == BuildSel.Prism)
            {
                var p = new GameObject("Prism").AddComponent<Prism>(); p.Init(c, r);
                prisms.Add(p); selPrism = p; p.SetSelected(true);
                SpawnUpgradeFx(p.pos, Defs.GLASS);
                prismsBuilt++;
            }
            else
            {
                var k = (TowerKind)((int)sel - 1);
                var t = new GameObject("Tower").AddComponent<Tower>(); t.Init(k, c, r);
                towers.Add(t); selTower = t; t.SetSelected(true);
                SpawnUpgradeFx(t.pos, t.def.color);
                towersBuilt++;
            }
            if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Place);
            ShowToast("已部署", Defs.HP);
        }
        else if (sel != BuildSel.None)
        {
            if (gold < CostOf(sel)) ShowToast("金币不足", Defs.DANGER);
            else ShowToast("无法在此建造", Defs.DANGER);
            if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Error, 0.5f);
            ClearSel();
        }
        else ClearSel();
    }

    // ================= 事件 =================
    public void SpawnEnemy(EnemyKind k, float hpScale)
    {
        var e = new GameObject("Enemy").AddComponent<Enemy>(); e.Init(k, hpScale); enemies.Add(e);
        if (k == EnemyKind.Dino)
        {
            if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.BossRoar);
            ApplyHitstop(0.08f);
            ApplyShake(0.26f, 0.35f);
        }
    }
    public void SpawnProjectile(Tower t, Enemy target)
    {
        var b = new GameObject("Bullet").AddComponent<Projectile>(); b.Init(t, target, this); projectiles.Add(b);
    }
    public void SpawnHit(Vector2 p, Color c)
    {
        for (int i = 0; i < 6; i++) AddParticle(p, c, Random.Range(0f, 6.28f), Random.Range(0.8f, 2.5f), Random.Range(0.2f, 0.4f), Random.Range(0.06f, 0.13f));
        if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Hit, 0.3f);
    }
    public void SpawnExplosion(Vector2 p, Color c, int n)
    {
        for (int i = 0; i < Mathf.Max(10, n / 2); i++) AddParticle(p, c, Random.Range(0f, 6.28f), Random.Range(1.5f, 5.5f), Random.Range(0.3f, 0.7f), Random.Range(0.08f, 0.18f));
    }
    void SpawnUpgradeFx(Vector2 p, Color c)
    {
        for (int i = 0; i < 16; i++) AddParticle(p, c, i / 16f * 6.28f, 3.5f, 0.5f, 0.1f);
    }
    void AddParticle(Vector2 p, Color c, float ang, float sp, float life, float size)
    {
        var pa = new GameObject("p").AddComponent<ParticleBit>(); pa.Init(p, c, ang, sp, life, size); particles.Add(pa);
    }

    public void OnKill(Enemy e)
    {
        if (e.counted) return;
        e.counted = true;
        combo++; comboTimer = 1.5f;
        if (combo > bestCombo) bestCombo = combo;
        totalKills++;
        int bonus = combo >= 5 ? e.reward : (combo >= 3 ? Mathf.RoundToInt(e.reward * 0.5f) : 0);
        int goldGain = e.reward + bonus;
        int scoreGain = e.reward * 2 + bonus;
        if (cards != null)
        {
            goldGain = cards.ModifyKillGold(e.reward) + bonus;
            scoreGain = cards.ModifyScore(scoreGain);
        }
        gold += goldGain;
        totalGoldEarned += goldGain;
        score += scoreGain;
        SpawnExplosion(e.pos, e.color, 20);
        if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Kill, 0.6f);
        if (combo == 3) ShowToast("三连击！+奖励", Defs.GOLD);
        else if (combo == 5) ShowToast("五连击！双倍奖励！", Defs.AMBER);
        else if (combo >= 8) ShowToast(combo + "连击！！", Defs.MAGENTA);
    }
    public void OnWaveClear()
    {
        int reward = 20 + waves.wave * 5;
        if (cards != null) reward = cards.ModifyWaveGold(reward);
        gold += reward;
        totalGoldEarned += reward;
        wavesCleared++;
        if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.WaveClear);
        if (cards != null) cards.OnWaveCleared(waves.wave, waves.total);
        ShowToast("波次完成！奖励 " + reward + " 金币", Defs.GOLD);
        ApplyHitstop(0.05f);
        ApplyShake(0.10f, 0.16f);
        if (waves.wave >= waves.total) state = "won";
    }
    void LoseLife(Enemy e)
    {
        int loss = e.boss ? 3 : 1;
        bool absorbed = cards != null && cards.TryAbsorbLifeLoss(ref loss);
        int originalLoss = e.boss ? 3 : 1;
        if (absorbed) damageBlocked += originalLoss - loss;
        SpawnExplosion(e.pos, Defs.DANGER, e.boss ? 40 : 26);
        if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.CoreHit);
        if (absorbed && loss <= 0) ShowToast("护盾抵消了伤害！", Defs.HP);
        else ShowToast("核心受损！", Defs.DANGER);
        ApplyHitstop(absorbed && loss <= 0 ? 0.04f : 0.08f);
        ApplyShake(absorbed && loss <= 0 ? 0.14f : 0.28f, absorbed && loss <= 0 ? 0.18f : 0.32f);
        lives -= loss;
        damageTaken += loss;
        if (lives <= 0) { lives = 0; state = "lost"; }
    }

    void StartWave()
    {
        if (cards != null && cards.HasDraft) { ShowToast("先选择一张卡牌", Defs.AMBER); return; }
        if (state == "won" || state == "lost") { Reset(); state = "playing"; return; }
        if (state == "ready") state = "playing";
        if (state == "playing")
        {
            if (waves.Start())
            {
                if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.WaveStart);
                ShowToast("第 " + waves.wave + " 波来袭！", Defs.CYAN);
            }
        }
    }

    // ================= 激光渲染 =================
    void RenderLasers()
    {
        int idx = 0;
        foreach (var t in towers)
        {
            if (t.kind != TowerKind.Laser || t.beam == null) continue;
            foreach (var seg in t.beam.segments)
            {
                LineRenderer lr = GetLR(idx++);
                lr.enabled = true;
                lr.positionCount = 2;
                lr.SetPosition(0, new Vector3(seg.a.x, seg.a.y, 0));
                lr.SetPosition(1, new Vector3(seg.b.x, seg.b.y, 0));
                lr.startColor = seg.color;
                lr.endColor = seg.color;
                float pulse = 0.07f + Mathf.Sin(Time.time * 8f + idx) * 0.025f;
                lr.widthMultiplier = pulse;
            }
        }
        for (int i = idx; i < laserPool.Count; i++) laserPool[i].enabled = false;
    }
    LineRenderer GetLR(int i)
    {
        while (laserPool.Count <= i)
        {
            var go = new GameObject("beam"); go.transform.SetParent(laserHolder);
            var lr = go.AddComponent<LineRenderer>();
            GameMap.SetupLine(lr, 0.09f, Defs.LASER, 19);
            laserPool.Add(lr);
        }
        return laserPool[i];
    }

    // ================= OnGUI 界面 =================
    void EnsureStyles()
    {
        if (guiReady) return;

        stLabel = MakeStyle(GUI.skin.label, uiFont, 15, Defs.INK, overflow: true, wrap: true);
        stSmall = MakeStyle(GUI.skin.label, uiFont, 12, Defs.MUTED, overflow: true, wrap: true);
        stBig   = MakeStyle(GUI.skin.label, uiFont, 18, Defs.INK, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        stTitle = MakeStyle(GUI.skin.label, uiFont, 30, Defs.CYAN, FontStyle.Bold, TextAnchor.MiddleCenter, true);

        stBtn = new GUIStyle(GUI.skin.button);
        stBtn.font = uiFont; stBtn.fontSize = 13;
        stBtn.normal.textColor = Defs.BTN_TEXT;
        stBtn.hover.textColor = Defs.INK;
        stBtn.active.textColor = Color.white;
        stBtn.border = new RectOffset(2,2,2,2);
        stBtn.padding = new RectOffset(8,8,6,6);

        stBtnSel = new GUIStyle(stBtn);
        stBtnSel.fontStyle = FontStyle.Bold;
        stBtnSel.normal.textColor = Defs.CYAN;

        stToast = MakeStyle(GUI.skin.label, uiFont, 14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        stDesc  = MakeStyle(GUI.skin.label, uiFont, 11, Defs.DIM, overflow: true, wrap: true);
        stHotkey = MakeStyle(GUI.skin.label, uiFont, 10, new Color(Defs.DIM.r, Defs.DIM.g, Defs.DIM.b, 0.6f), align: TextAnchor.MiddleRight);

        guiReady = true;
    }

    GUIStyle TempStyle(GUIStyle basis, int size, Color col, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft)
    {
        var s = new GUIStyle(basis);
        s.fontSize = size; s.fontStyle = fs; s.alignment = a;
        s.normal.textColor = col;
        s.clipping = TextClipping.Overflow;
        return s;
    }

    void OnGUI()
    {
        EnsureStyles();
        float px = Screen.width * (1f - panelFrac);
        float pw = Screen.width * panelFrac;

        // === 右侧面板 ===
        GUI.color = new Color(Defs.PANEL_BG.r, Defs.PANEL_BG.g, Defs.PANEL_BG.b, 0.97f);
        GUI.DrawTexture(new Rect(px, 0, pw, Screen.height), texWhite);
        GUI.color = new Color(Defs.BORDER.r, Defs.BORDER.g, Defs.BORDER.b, 0.3f);
        GUI.DrawTexture(new Rect(px, 0, 1, Screen.height), texWhite);
        GUI.color = Color.white;

        float x = px + 8, w = pw - 16, y = 8;

        // 标题
        GUI.Label(new Rect(x, y, w, 18), "晶核竞技场",
            TempStyle(stLabel, 13, Defs.AMBER, FontStyle.Bold));
        y += 20;

        // 状态：两行两列，避免窄面板时遮挡
        float statGap = 4f;
        float statW = (w - statGap) / 2f;
        DrawStatMini(x, y, "生命 " + lives, Defs.DANGER, statW);
        DrawStatMini(x + statW + statGap, y, "金币 " + gold, Defs.GOLD, statW);
        y += 22;
        DrawStatMini(x, y, "波次 " + waves.wave + "/" + waves.total, Defs.FROST, statW);
        DrawStatMini(x + statW + statGap, y, "得分 " + score, Defs.MAGENTA, statW);
        y += 24;
        GUI.Label(new Rect(x, y, w, 16), "击杀 " + totalKills + " · 连击 " + bestCombo + " · 选牌 " + cardsPickedCount,
            TempStyle(stSmall, 9, Defs.MUTED));
        y += 16;

        float hudY = 12f;
        if (cards != null) cards.DrawActiveTags(12f, ref hudY, Mathf.Min(px - 24f, 280f), texWhite, uiFont);

        // 细分隔线
        GUI.color = new Color(Defs.BORDER.r, Defs.BORDER.g, Defs.BORDER.b, 0.25f);
        GUI.DrawTexture(new Rect(x, y, w, 1), texWhite);
        GUI.color = Color.white;
        y += 4;

        // 防御塔 + 道具（紧凑单行卡片）
        DrawCompactCard(ref y, x, w, BuildSel.Laser,  "激光炮台",       120, "1", Defs.LASER);
        DrawCompactCard(ref y, x, w, BuildSel.Arrow,   "机枪台",         50, "2", Defs.CYAN);
        DrawCompactCard(ref y, x, w, BuildSel.Cannon,  "加农炮",        100, "3", Defs.AMBER);
        DrawCompactCard(ref y, x, w, BuildSel.Frost,   "冰冻台",         75, "4", Defs.FROST);
        DrawCompactCard(ref y, x, w, BuildSel.Mirror,  "反射镜",  Defs.MIRROR_COST, "5", Defs.GLASS);
        DrawCompactCard(ref y, x, w, BuildSel.Prism,   "棱镜",    Defs.PRISM_COST,  "6", Defs.MAGENTA);

        // 分隔
        y += 1;
        GUI.color = new Color(Defs.BORDER.r, Defs.BORDER.g, Defs.BORDER.b, 0.25f);
        GUI.DrawTexture(new Rect(x, y, w, 1), texWhite);
        GUI.color = Color.white;
        y += 4;

        float footerTop = Screen.height - 126f;
        float detailTop = Mathf.Min(y, footerTop - 110f);
        DrawSelectionInfo(x, ref detailTop, w, footerTop - detailTop - 4f);

        // 底部控制
        y = footerTop;

        // 波次预告
        if (!waves.active && waves.wavePreview.Length > 0 && waves.wave < waves.total)
        {
            GUI.Label(new Rect(x, y, w, 24), "下一波: " + waves.wavePreview,
                MakeStyle(stSmall, uiFont, 9, Defs.AMBER, wrap: true));
            y += 24;
        }

        string startLabel = (cards != null && cards.HasDraft) ? "先选卡牌" : (waves.active ? "\u2694 进攻中…" : "\u25B6 开始下一波");
        if (GUI.Button(new Rect(x, y, w, 24), startLabel, stBtn) && !waves.active) StartWave();
        y += 27;
        float btnHalfW = w / 2 - 2;
        if (GUI.Button(new Rect(x, y, btnHalfW, 22), "速度 " + speed + "\u00D7", stBtn)) speed = speed == 1 ? 2 : 1;
        if (GUI.Button(new Rect(x + btnHalfW + 4, y, btnHalfW, 22), "重新开始", stBtn)) Reset();
        y += 26;

        GUI.Label(new Rect(x, y, 48, 14), "乐音", TempStyle(stSmall, 10, Defs.CYAN));
        float newMusicVolume = GUI.HorizontalSlider(new Rect(x + 30, y + 2, w - 68, 14), musicVolume, 0f, 1f);
        GUI.Label(new Rect(x + w - 34, y - 2, 32, 18), Mathf.RoundToInt(newMusicVolume * 100) + "%",
            TempStyle(stSmall, 9, Defs.INK, FontStyle.Normal, TextAnchor.MiddleRight));
        if (Mathf.Abs(newMusicVolume - musicVolume) > 0.001f)
        {
            musicVolume = newMusicVolume;
            if (music) music.SetVolume(musicVolume);
        }

        // Boss 血条（游戏区域底部）
        DrawBossBar(px);

        // 连击显示（游戏区域右下）
        if (combo >= 2 && comboTimer > 0)
        {
            float ca = Mathf.Clamp01(comboTimer);
            string comboText = combo + " COMBO!";
            GUI.Label(new Rect(px - 180, Screen.height - 60, 170, 36), comboText,
                TempStyle(stLabel, 20, new Color(Defs.GOLD.r, Defs.GOLD.g, Defs.GOLD.b, ca),
                    FontStyle.Bold, TextAnchor.MiddleRight));
        }

        if (state == "playing" && showGuide) DrawGuideOverlay(px);
        if (state != "playing") DrawOverlay(px);
        if (cards != null) cards.DrawDraftOverlay(px, Screen.height, texWhite, uiFont);
        DrawToast(px);
    }

    void DrawSeparator(float x, ref float y, float w)
    {
        y += 4;
        GUI.color = new Color(Defs.BORDER.r, Defs.BORDER.g, Defs.BORDER.b, 0.3f);
        GUI.DrawTexture(new Rect(x, y, w, 1), texWhite);
        GUI.color = Color.white;
        y += 8;
    }

    void DrawStatMini(float x, float y, string text, Color c, float w)
    {
        GUI.color = new Color(c.r, c.g, c.b, 0.12f);
        GUI.DrawTexture(new Rect(x, y, w, 18), texWhite);
        GUI.color = new Color(c.r, c.g, c.b, 0.75f);
        GUI.DrawTexture(new Rect(x, y, 2, 18), texWhite);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 6, y - 1, w - 8, 18), text,
            TempStyle(stLabel, 9, c, FontStyle.Bold, TextAnchor.MiddleLeft));
    }

    void DrawStatBadge2(float x, float y, string text, Color c, float w)
    {
        float h = 24;
        GUI.color = new Color(c.r, c.g, c.b, 0.1f);
        GUI.DrawTexture(new Rect(x, y, w, h), texWhite);
        GUI.color = new Color(c.r, c.g, c.b, 0.6f);
        GUI.DrawTexture(new Rect(x, y, 2, h), texWhite);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 6, y, w - 8, h), text,
            TempStyle(stLabel, 12, c, FontStyle.Bold, TextAnchor.MiddleLeft));
    }

    void DrawStatBadge(ref float x, float y, string text, Color c, float w)
    {
        DrawStatBadge2(x, y + 4, text, c, w);
        x += w + 6;
    }

    // 紧凑单行卡片：名称 + 费用 + 快捷键，高度仅 32px
    void DrawCompactCard(ref float y, float x, float w, BuildSel s, string name, int cost, string hotkey, Color accent)
    {
        float h = 26;
        bool seld = sel == s;
        Rect cardRect = new Rect(x, y, w, h);

        Color bgCol = seld ? Defs.CARD_SEL : Defs.CARD_BG;
        GUI.color = new Color(bgCol.r, bgCol.g, bgCol.b, seld ? 0.95f : 0.6f);
        GUI.DrawTexture(cardRect, texWhite);

        GUI.color = new Color(accent.r, accent.g, accent.b, seld ? 1f : 0.45f);
        GUI.DrawTexture(new Rect(x, y, 3, h), texWhite);

        if (seld)
        {
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.35f);
            GUI.DrawTexture(new Rect(x, y, w, 1), texWhite);
            GUI.DrawTexture(new Rect(x, y + h - 1, w, 1), texWhite);
        }
        GUI.color = Color.white;

        // 名称
        GUI.Label(new Rect(x + 7, y, w - 80, h), name,
            TempStyle(stLabel, 11, seld ? accent : Defs.INK, seld ? FontStyle.Bold : FontStyle.Normal));

        // 费用
        GUI.Label(new Rect(x + w - 72, y, 44, h), "\u25C6" + cost,
            TempStyle(stSmall, 9, Defs.GOLD, FontStyle.Normal, TextAnchor.MiddleLeft));

        // 快捷键
        GUI.Label(new Rect(x + w - 22, y, 16, h), hotkey, stHotkey);

        if (GUI.Button(cardRect, "", GUIStyle.none))
        {
            movingTower = null;
            sel = (sel == s) ? BuildSel.None : s;
        }
        y += h + 1;
    }

    void DrawSelectionInfo(float x, ref float y, float w, float maxHeight)
    {
        Rect area = new Rect(x, y, w, maxHeight);
        GUI.color = new Color(Defs.CARD_BG.r, Defs.CARD_BG.g, Defs.CARD_BG.b, 0.52f);
        GUI.DrawTexture(area, texWhite);
        GUI.color = Color.white;

        float contentW = Mathf.Max(10f, w - 18f);
        float contentH = 210f;
        if (selTower != null) contentH = 250f;
        else if (selMirror != null) contentH = 190f;
        else if (selPrism != null) contentH = 170f;

        selectionScroll = GUI.BeginScrollView(area, selectionScroll, new Rect(0, 0, contentW, contentH), false, true);
        float yy = 6f;
        float xx = 6f;

        if (selTower != null)
        {
            var t = selTower;
            GUI.Label(new Rect(xx, yy, contentW - 12, 18), t.def.name + " Lv." + t.level,
                TempStyle(stLabel, 12, t.def.color, FontStyle.Bold));
            yy += 18;

            if (t.kind == TowerKind.Laser)
            {
                GUI.Label(new Rect(xx, yy, contentW - 12, 16), "伤害 " + t.dmg.ToString("0") + "/秒 · 穿甲", TempStyle(stSmall, 10, Defs.INK)); yy += 14;
                GUI.Label(new Rect(xx, yy, contentW - 12, 16), "射程 " + t.range.ToString("0.0") + " · 反射", TempStyle(stSmall, 10, Defs.MUTED)); yy += 14;
                if (t.def.movable)
                {
                    GUI.Label(new Rect(xx, yy, contentW - 12, 24), "M 键移动（范围 " + t.def.moveRange + " 格）",
                        MakeStyle(stSmall, uiFont, 10, Defs.AMBER, wrap: true)); yy += 22;
                }
            }
            else
            {
                GUI.Label(new Rect(xx, yy, contentW - 12, 16), "伤害 " + t.dmg.ToString("0") + " · 射程 " + t.range.ToString("0.0"), TempStyle(stSmall, 10, Defs.INK)); yy += 14;
                GUI.Label(new Rect(xx, yy, contentW - 12, 16), "射速 " + (1f / t.rate).ToString("0.0") + "/秒", TempStyle(stSmall, 10, Defs.MUTED)); yy += 16;
            }
            if (t.CanUpgrade())
            {
                if (GUI.Button(new Rect(xx, yy, contentW - 12, 24), "升级 Lv." + (t.level + 1) + "（\u25C6 " + t.UpgradeCost() + "）", stBtn) && gold >= t.UpgradeCost())
                { gold -= t.UpgradeCost(); totalGoldSpent += t.UpgradeCost(); towersUpgraded++; t.Upgrade(); SpawnUpgradeFx(t.pos, t.def.color); }
            }
            else GUI.Label(new Rect(xx, yy, contentW - 12, 18), "已满级", TempStyle(stSmall, 10, Defs.GOLD));
            yy += 28;
            if (GUI.Button(new Rect(xx, yy, contentW - 12, 22), "出售（\u25C6 " + t.SellValue() + "）", stBtn)) SellTower();
            yy += 24;
        }
        else if (selMirror != null)
        {
            GUI.Label(new Rect(xx, yy, contentW - 12, 18), "反射镜 角度 " + Mathf.RoundToInt(selMirror.angle) + "\u00B0",
                TempStyle(stLabel, 12, Defs.GLASS, FontStyle.Bold));
            yy += 18;
            GUI.Label(new Rect(xx, yy, contentW - 12, 24), "滚轮 / Q E / R +15\u00B0", MakeStyle(stSmall, uiFont, 10, Defs.MUTED, wrap: true)); yy += 22;
            if (GUI.Button(new Rect(xx, yy, contentW - 12, 22), "旋转 15\u00B0", stBtn))
            { selMirror.Rotate(15f); if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Reflect, 0.3f); }
            yy += 24;
            if (GUI.Button(new Rect(xx, yy, contentW - 12, 22), "出售（\u25C6 " + selMirror.SellValue() + "）", stBtn)) SellMirror();
            yy += 24;
        }
        else if (selPrism != null)
        {
            GUI.Label(new Rect(xx, yy, contentW - 12, 18), "分光棱镜",
                TempStyle(stLabel, 12, Defs.MAGENTA, FontStyle.Bold));
            yy += 18;
            GUI.Label(new Rect(xx, yy, contentW - 12, 24), "折射并分成三色光束", MakeStyle(stSmall, uiFont, 10, Defs.MUTED, wrap: true)); yy += 22;
            if (GUI.Button(new Rect(xx, yy, contentW - 12, 22), "出售（\u25C6 " + selPrism.SellValue() + "）", stBtn)) SellPrism();
            yy += 24;
        }
        else
        {
            GUI.Label(new Rect(xx, yy, contentW - 12, 32), "点击塔、镜子或棱镜查看详情", TempStyle(stSmall, 10, Defs.MUTED));
            yy += 24;
        }

        GUI.EndScrollView();
        y += maxHeight;
    }

    void DrawBossBar(float pw)
    {
        Enemy boss = null;
        foreach (var e in enemies) if (e.boss) { boss = e; break; }
        if (boss == null) return;
        // 游戏区域底部居中
        float bw = Mathf.Min(pw - 40, 400);
        float bx = (pw - bw) / 2f;
        float by = Screen.height - 40;
        float bh = 18;
        // 背景
        GUI.color = new Color(0.04f, 0.06f, 0.12f, 0.92f);
        GUI.DrawTexture(new Rect(bx - 4, by - 4, bw + 8, bh + 12), texWhite);
        // 血条底
        GUI.color = new Color(0.15f, 0.1f, 0.1f, 1f);
        GUI.DrawTexture(new Rect(bx, by, bw, bh), texWhite);
        float frac = Mathf.Clamp01(boss.hp / boss.maxHp);
        Color hpCol = frac > 0.5f ? Defs.DANGER : (frac > 0.25f ? new Color(1f, 0.5f, 0.2f) : new Color(0.8f, 0.15f, 0.15f));
        GUI.color = hpCol;
        GUI.DrawTexture(new Rect(bx, by, bw * frac, bh), texWhite);
        GUI.color = Color.white;
        GUI.Label(new Rect(bx + 6, by - 1, 200, bh + 2), "深渊巨龙",
            TempStyle(stSmall, 12, Color.white, FontStyle.Bold));
        GUI.Label(new Rect(bx + bw - 56, by - 1, 52, bh + 2), Mathf.RoundToInt(frac * 100) + "%",
            TempStyle(stSmall, 12, Color.white, FontStyle.Normal, TextAnchor.MiddleRight));
    }

    void DrawToast(float pw)
    {
        if (toastTimer <= 0) return;
        float alpha = Mathf.Clamp01(toastTimer * 2f);
        float tw = Mathf.Min(pw * 0.5f, 300);
        float tx = (pw - tw) / 2f;
        float ty = 52;
        // 背景
        GUI.color = new Color(0.04f, 0.07f, 0.15f, 0.92f * alpha);
        GUI.DrawTexture(new Rect(tx, ty, tw, 32), texWhite);
        // 顶部彩色线
        GUI.color = new Color(toastColor.r, toastColor.g, toastColor.b, 0.7f * alpha);
        GUI.DrawTexture(new Rect(tx, ty, tw, 2), texWhite);
        GUI.color = Color.white;
        stToast.normal.textColor = new Color(toastColor.r, toastColor.g, toastColor.b, alpha);
        GUI.Label(new Rect(tx, ty + 2, tw, 28), toastMsg, stToast);
    }

    void DrawGuideOverlay(float gw)
    {
        float bw = Mathf.Min(292f, gw * 0.42f);
        float bh = 158f;
        float bx = gw - bw - 14f;
        float by = 14f;
        GUI.color = new Color(Defs.PANEL_BG.r, Defs.PANEL_BG.g, Defs.PANEL_BG.b, 0.86f);
        GUI.DrawTexture(new Rect(bx, by, bw, bh), texWhite);
        GUI.color = new Color(Defs.AMBER.r, Defs.AMBER.g, Defs.AMBER.b, 0.82f);
        GUI.DrawTexture(new Rect(bx, by, bw, 2), texWhite);
        GUI.color = Color.white;

        GUI.Label(new Rect(bx + 10, by + 6, bw - 20, 20), "作战手册", TempStyle(stLabel, 12, Defs.AMBER, FontStyle.Bold));
        string[] lines = {
            "1-6 建造不同设施，空格开始下一波",
            "激光可被镜子反射，被棱镜分成三束",
            "选中激光炮台后按 M 可以移动",
            "每波结束抽卡，优先选择当前缺的效果",
            "H 可隐藏此说明"
        };
        float yy = by + 28f;
        foreach (var line in lines)
        {
            GUI.Label(new Rect(bx + 10, yy, bw - 20, 22), line, TempStyle(stSmall, 10, Defs.INK));
            yy += 24f;
        }
    }

    void DrawOverlay(float gw)
    {
        GUI.color = new Color(5 / 255f, 8 / 255f, 18 / 255f, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, gw, Screen.height), texWhite);
        GUI.color = Color.white;
        float cx = gw / 2f;
        string title;
        string[] sub;
        Color titleCol;

        if (state == "won")
        {
            title = "\u2726 守卫成功！"; titleCol = Defs.HP;
            sub = new[] { "你击败了深渊巨龙，守住了晶核！", "最终得分：" + score, "按 H 可查看/关闭作战手册，点「开始下一波」再玩一次" };
        }
        else if (state == "lost")
        {
            title = "核心失守"; titleCol = Defs.DANGER;
            sub = new[] { "晶核在第 " + waves.wave + " 波被攻陷", "得分：" + score, "复盘卡牌选择与光路布局后可继续挑战" };
        }
        else
        {
            title = "晶核竞技场"; titleCol = Defs.CYAN;
            sub = new[] {
                "建造激光炮台，用镜子和棱镜构建光路网络",
                "激光碰镜子反射，过棱镜折射并分成三色光",
                "选中镜子后用滚轮可任意角度旋转",
                "激光炮台可按 M 键在范围内移动",
                "每波结束都可抽一张卡牌强化战局",
                "第 15 波将出现深渊巨龙！",
                "",
                "快捷键：1-6 选塔 · 空格 下一波 · Esc 取消"
            };
        }
        stTitle.normal.textColor = titleCol;
        GUI.Label(new Rect(cx - 250, Screen.height * 0.3f, 500, 44), title, stTitle);
        float yy = Screen.height * 0.3f + 55;
        foreach (var s in sub) { GUI.Label(new Rect(cx - 280, yy, 560, 24), s, stBig); yy += 28; }

        if (state == "won" || state == "lost")
        {
            float boxW = 420f;
            float boxH = 138f;
            float boxX = cx - boxW / 2f;
            float boxY = yy + 6f;
            GUI.color = new Color(Defs.PANEL_BG.r, Defs.PANEL_BG.g, Defs.PANEL_BG.b, 0.92f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), texWhite);
            GUI.color = new Color(Defs.AMBER.r, Defs.AMBER.g, Defs.AMBER.b, 0.75f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 2), texWhite);
            GUI.color = Color.white;
            GUI.Label(new Rect(boxX + 12, boxY + 8, boxW - 24, 20), "本局统计", TempStyle(stLabel, 13, Defs.AMBER, FontStyle.Bold, TextAnchor.MiddleCenter));

            string[] cols = {
                "击杀总数  " + totalKills,
                "完成波次  " + wavesCleared,
                "最佳连击  " + bestCombo,
                "抽卡次数  " + cardsPickedCount,
                "炮台建造  " + towersBuilt,
                "镜子/棱镜  " + mirrorsBuilt + "/" + prismsBuilt,
                "升级次数  " + towersUpgraded,
                "出售次数  " + towersSold,
                "获得金币  " + totalGoldEarned,
                "消耗金币  " + totalGoldSpent,
                "承受伤害  " + damageTaken,
                "护盾抵挡  " + damageBlocked
            };
            float colW = (boxW - 36f) / 2f;
            float rowY = boxY + 34f;
            for (int i = 0; i < cols.Length; i++)
            {
                float colX = boxX + 12f + (i % 2) * colW;
                float ry = rowY + (i / 2) * 16f;
                GUI.Label(new Rect(colX, ry, colW - 8f, 16f), cols[i], TempStyle(stSmall, 10, Defs.INK));
            }
        }
    }

    void SellTower()
    {
        if (selTower == null) return;
        gold += selTower.SellValue(); SpawnExplosion(selTower.pos, Defs.MUTED, 24);
        totalGoldEarned += selTower.SellValue();
        towersSold++;
        towers.Remove(selTower); Destroy(selTower.gameObject); selTower = null;
        if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Sell);
        ShowToast("已出售", Defs.MUTED);
    }
    void SellMirror()
    {
        if (selMirror == null) return;
        gold += selMirror.SellValue(); SpawnExplosion(selMirror.pos, Defs.GLASS, 20);
        totalGoldEarned += selMirror.SellValue();
        towersSold++;
        mirrors.Remove(selMirror); Destroy(selMirror.gameObject); selMirror = null;
        if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Sell);
        ShowToast("已出售", Defs.MUTED);
    }
    void SellPrism()
    {
        if (selPrism == null) return;
        gold += selPrism.SellValue(); SpawnExplosion(selPrism.pos, Defs.GLASS, 20);
        totalGoldEarned += selPrism.SellValue();
        towersSold++;
        prisms.Remove(selPrism); Destroy(selPrism.gameObject); selPrism = null;
        if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.Sell);
        ShowToast("已出售", Defs.MUTED);
    }
}
