using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// CardSystem —— 每波结束抽牌
// 负责卡牌草案、效果持续时间、奖励修正和状态标签绘制。
// =====================================================================
public enum CardKind
{
    ScoreDouble,
    TimeReverse,
    Freeze,
    Shield,
    GoldRush,
    LaserOverload,
    TimeWarp,
    GoldMagnet
}

public class CardSystem
{
    class CardDef
    {
        public CardKind kind;
        public string title;
        public string desc;
        public string shortLabel;
        public Color color;
    }

    readonly Game game;
    readonly List<CardDef> pool = new List<CardDef>();
    readonly List<CardDef> offered = new List<CardDef>();

    GUIStyle titleStyle;
    GUIStyle descStyle;
    GUIStyle chipStyle;
    GUIStyle overlayTitleStyle;
    GUIStyle buttonTextStyle;
    GUIStyle hudTextStyle;
    GUIStyle hudValueStyle;
    bool stylesReady;
    float draftAnim;

    int scoreDoubleWaves;
    int goldRushWaves;
    int laserOverloadWaves;
    int timeWarpWaves;
    int goldMagnetWaves;
    int shieldCharges;
    float freezeTimer;
    float reverseTimer;

    static Texture2D iconCircle;
    static Texture2D iconDiamond;
    static Texture2D iconTriangle;

    public bool HasDraft => offered.Count > 0;
    public bool AreEnemiesFrozen => freezeTimer > 0.01f;
    public bool AreEnemiesReversed => reverseTimer > 0.01f;
    public int ShieldCharges => shieldCharges;
    public float ScoreMultiplier => scoreDoubleWaves > 0 ? 2f : 1f;
    public float GoldMultiplier => goldRushWaves > 0 ? 1.5f : 1f;
    public float LaserDamageMultiplier => laserOverloadWaves > 0 ? 1.65f : 1f;
    public float LaserRangeMultiplier => laserOverloadWaves > 0 ? 1.15f : 1f;
    public float FireRateMultiplier => timeWarpWaves > 0 ? 1.35f : 1f;
    public float ProjectileSpeedMultiplier => timeWarpWaves > 0 ? 1.25f : 1f;
    public int GoldMagnetBonus => goldMagnetWaves > 0 ? 2 : 0;

    public CardSystem(Game owner)
    {
        game = owner;
        pool.Add(new CardDef { kind = CardKind.ScoreDouble, title = "分数翻倍", shortLabel = "双倍分数", desc = "接下来 1 波的击杀得分翻倍。", color = Defs.MAGENTA });
        pool.Add(new CardDef { kind = CardKind.TimeReverse, title = "时间反转", shortLabel = "时间反转", desc = "接下来一开波，敌人会短暂倒退回入口。", color = Defs.CYAN });
        pool.Add(new CardDef { kind = CardKind.Freeze, title = "冰封领域", shortLabel = "冰封领域", desc = "接下来一开波，敌人生成与移动会短暂冻结。", color = Defs.FROST });
        pool.Add(new CardDef { kind = CardKind.Shield, title = "核心护盾", shortLabel = "护盾", desc = "获得 3 层护盾，优先抵消漏怪伤害。", color = Defs.HP });
        pool.Add(new CardDef { kind = CardKind.GoldRush, title = "金币热潮", shortLabel = "金币热潮", desc = "接下来 1 波金币奖励提升 50%。", color = Defs.GOLD });
        pool.Add(new CardDef { kind = CardKind.LaserOverload, title = "激光超载", shortLabel = "激光超载", desc = "接下来 1 波激光塔伤害和射程提高。", color = Defs.LASER });
        pool.Add(new CardDef { kind = CardKind.TimeWarp, title = "时间扭曲", shortLabel = "时间扭曲", desc = "接下来 1 波炮台攻速和弹速提高。", color = Defs.AMBER });
        pool.Add(new CardDef { kind = CardKind.GoldMagnet, title = "金币磁铁", shortLabel = "金币磁铁", desc = "立刻获得 35 金币，并在接下来 2 波额外吸金。", color = Defs.GLASS });
    }

    public void Tick(float dt, bool gameplayActive)
    {
        draftAnim = Mathf.MoveTowards(draftAnim, HasDraft ? 1f : 0f, dt * 4f);
        if (!gameplayActive) return;

        if (freezeTimer > 0f) freezeTimer = Mathf.Max(0f, freezeTimer - dt);
        if (reverseTimer > 0f) reverseTimer = Mathf.Max(0f, reverseTimer - dt);
    }

    public void OnWaveCleared(int wave, int total)
    {
        if (scoreDoubleWaves > 0) scoreDoubleWaves--;
        if (goldRushWaves > 0) goldRushWaves--;
        if (laserOverloadWaves > 0) laserOverloadWaves--;
        if (timeWarpWaves > 0) timeWarpWaves--;
        if (goldMagnetWaves > 0) goldMagnetWaves--;

        if (wave >= total)
        {
            offered.Clear();
            return;
        }

        BuildOffer();
        draftAnim = 0f;
        if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.CardDraw, 0.8f);
    }

    public bool TryAbsorbLifeLoss(ref int loss)
    {
        if (shieldCharges <= 0 || loss <= 0) return false;
        int absorbed = Mathf.Min(shieldCharges, loss);
        shieldCharges -= absorbed;
        loss -= absorbed;
        return absorbed > 0;
    }

    public int ModifyKillGold(int baseReward)
    {
        int value = Mathf.RoundToInt(baseReward * GoldMultiplier);
        if (GoldMagnetBonus > 0) value += GoldMagnetBonus;
        return value;
    }

    public int ModifyWaveGold(int baseReward)
    {
        int value = Mathf.RoundToInt(baseReward * GoldMultiplier);
        if (GoldMagnetBonus > 0) value += 8;
        return value;
    }

    public int ModifyScore(int baseScore)
    {
        return Mathf.RoundToInt(baseScore * ScoreMultiplier);
    }

    void BuildOffer()
    {
        offered.Clear();
        List<int> used = new List<int>();
        while (offered.Count < 3 && used.Count < pool.Count)
        {
            int idx = Random.Range(0, pool.Count);
            if (used.Contains(idx)) continue;
            used.Add(idx);
            offered.Add(pool[idx]);
        }
    }

    void Pick(CardDef card)
    {
        switch (card.kind)
        {
            case CardKind.ScoreDouble:
                scoreDoubleWaves = Mathf.Max(scoreDoubleWaves, 1);
                game.Notify("已获得卡牌：分数翻倍", card.color);
                break;
            case CardKind.TimeReverse:
                reverseTimer = Mathf.Max(reverseTimer, 4.2f);
                game.Notify("已获得卡牌：时间反转", card.color);
                break;
            case CardKind.Freeze:
                freezeTimer = Mathf.Max(freezeTimer, 3.4f);
                game.Notify("已获得卡牌：冰封领域", card.color);
                break;
            case CardKind.Shield:
                shieldCharges += 3;
                game.Notify("核心护盾 +3", card.color);
                break;
            case CardKind.GoldRush:
                goldRushWaves = Mathf.Max(goldRushWaves, 1);
                game.Notify("下一波金币热潮已激活", card.color);
                break;
            case CardKind.LaserOverload:
                laserOverloadWaves = Mathf.Max(laserOverloadWaves, 1);
                game.Notify("激光超载已就绪", card.color);
                break;
            case CardKind.TimeWarp:
                timeWarpWaves = Mathf.Max(timeWarpWaves, 1);
                game.Notify("时间扭曲已就绪", card.color);
                break;
            case CardKind.GoldMagnet:
                goldMagnetWaves = Mathf.Max(goldMagnetWaves, 2);
                game.gold += 35;
                game.Notify("金币磁铁激活，立刻获得 35 金币", card.color);
                break;
        }

        offered.Clear();
        game.OnCardPicked(card.color);
        if (SfxManager.Instance) SfxManager.Instance.Play(SfxKind.CardPick, 0.9f);
    }

    void EnsureStyles(Font font)
    {
        if (stylesReady) return;

        EnsureIcons();

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.font = font;
        titleStyle.fontSize = 16;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;
        titleStyle.alignment = TextAnchor.UpperLeft;
        titleStyle.clipping = TextClipping.Overflow;

        descStyle = new GUIStyle(GUI.skin.label);
        descStyle.font = font;
        descStyle.fontSize = 11;
        descStyle.wordWrap = true;
        descStyle.normal.textColor = Defs.INK;

        chipStyle = new GUIStyle(GUI.skin.label);
        chipStyle.font = font;
        chipStyle.fontSize = 11;
        chipStyle.fontStyle = FontStyle.Bold;
        chipStyle.alignment = TextAnchor.MiddleCenter;
        chipStyle.normal.textColor = Color.white;
        chipStyle.clipping = TextClipping.Overflow;

        overlayTitleStyle = new GUIStyle(titleStyle);
        overlayTitleStyle.fontSize = 28;
        overlayTitleStyle.alignment = TextAnchor.MiddleCenter;

        buttonTextStyle = new GUIStyle(titleStyle);
        buttonTextStyle.fontSize = 13;
        buttonTextStyle.alignment = TextAnchor.MiddleLeft;

        hudTextStyle = new GUIStyle(GUI.skin.label);
        hudTextStyle.font = font;
        hudTextStyle.fontSize = 10;
        hudTextStyle.fontStyle = FontStyle.Bold;
        hudTextStyle.alignment = TextAnchor.MiddleLeft;
        hudTextStyle.normal.textColor = Defs.INK;
        hudTextStyle.clipping = TextClipping.Overflow;

        hudValueStyle = new GUIStyle(hudTextStyle);
        hudValueStyle.fontSize = 9;
        hudValueStyle.fontStyle = FontStyle.Normal;
        hudValueStyle.alignment = TextAnchor.MiddleRight;
        hudValueStyle.normal.textColor = Defs.MUTED;

        stylesReady = true;
    }

    void EnsureIcons()
    {
        if (iconCircle != null && iconDiamond != null && iconTriangle != null) return;
        iconCircle = MakeMaskTex(18, (x, y, cx, cy, r) => ((x - cx) * (x - cx) + (y - cy) * (y - cy)) <= r * r);
        iconDiamond = MakeMaskTex(18, (x, y, cx, cy, r) => Mathf.Abs(x - cx) + Mathf.Abs(y - cy) <= r);
        iconTriangle = MakeMaskTex(18, (x, y, cx, cy, r) =>
        {
            float nx = (x - cx) / r;
            float ny = (y - cy) / r;
            if (ny < -0.6f || ny > 0.9f) return false;
            float w = (0.9f - ny) * 0.9f;
            return Mathf.Abs(nx) <= w;
        });
    }

    delegate bool MaskFn(float x, float y, float cx, float cy, float r);

    static Texture2D MakeMaskTex(int size, MaskFn inside)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float r = size * 0.38f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool ok = inside(x, y, cx, cy, r);
                float edge = 0f;
                if (ok)
                {
                    float dx = (x - cx);
                    float dy = (y - cy);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    edge = Mathf.Clamp01((r + 1.2f - d) / 1.2f);
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, ok ? edge : 0f));
            }
        tex.Apply(false, true);
        return tex;
    }

    public void DrawActiveTags(float x, ref float y, float width, Texture2D texWhite, Font font)
    {
        EnsureStyles(font);
        var effects = new List<(string name, string value, Color color, Texture2D icon, int pri)>();
        if (freezeTimer > 0f) effects.Add(("冰封", freezeTimer.ToString("0.0") + "s", Defs.FROST, iconCircle, 0));
        if (reverseTimer > 0f) effects.Add(("反转", reverseTimer.ToString("0.0") + "s", Defs.CYAN, iconTriangle, 1));
        if (shieldCharges > 0) effects.Add(("护盾", "x" + shieldCharges, Defs.HP, iconDiamond, 2));
        if (scoreDoubleWaves > 0) effects.Add(("分数", scoreDoubleWaves + "波", Defs.MAGENTA, iconDiamond, 3));
        if (goldRushWaves > 0) effects.Add(("金币", goldRushWaves + "波", Defs.GOLD, iconCircle, 4));
        if (laserOverloadWaves > 0) effects.Add(("超载", laserOverloadWaves + "波", Defs.LASER, iconTriangle, 5));
        if (timeWarpWaves > 0) effects.Add(("扭曲", timeWarpWaves + "波", Defs.AMBER, iconDiamond, 6));
        if (goldMagnetWaves > 0) effects.Add(("磁铁", goldMagnetWaves + "波", Defs.GLASS, iconCircle, 7));

        effects.Sort((a, b) => a.pri.CompareTo(b.pri));

        float headerH = 18f;
        float headerW = Mathf.Min(width, 190f);
        GUI.color = new Color(Defs.PANEL_BG.r, Defs.PANEL_BG.g, Defs.PANEL_BG.b, 0.82f);
        GUI.DrawTexture(new Rect(x, y, headerW, headerH), texWhite);
        GUI.color = new Color(Defs.AMBER.r, Defs.AMBER.g, Defs.AMBER.b, 0.8f);
        GUI.DrawTexture(new Rect(x, y, headerW, 2), texWhite);
        GUI.color = Color.white;
        hudTextStyle.normal.textColor = Defs.INK;
        GUI.Label(new Rect(x + 7, y - 1, headerW - 12, headerH), effects.Count == 0 ? "卡牌：无" : "增益", hudTextStyle);
        y += 22f;

        if (effects.Count == 0) return;

        float itemH = 18f;
        float maxW = Mathf.Max(120f, Mathf.Min(width, 280f));
        float itemW = Mathf.Min(maxW, 136f);
        int perRow = Mathf.Max(1, Mathf.FloorToInt(maxW / (itemW + 6f)));

        int idx = 0;
        foreach (var e in effects)
        {
            float ix = x + (idx % perRow) * (itemW + 6f);
            float iy = y + (idx / perRow) * (itemH + 5f);

            GUI.color = new Color(Defs.PANEL_BG.r, Defs.PANEL_BG.g, Defs.PANEL_BG.b, 0.78f);
            GUI.DrawTexture(new Rect(ix, iy, itemW, itemH), texWhite);
            GUI.color = new Color(e.color.r, e.color.g, e.color.b, 0.85f);
            GUI.DrawTexture(new Rect(ix, iy, 2, itemH), texWhite);
            GUI.color = Color.white;

            Rect iconRect = new Rect(ix + 6, iy + 2, 14, 14);
            GUI.color = new Color(e.color.r, e.color.g, e.color.b, 0.95f);
            if (e.icon != null) GUI.DrawTexture(iconRect, e.icon);
            GUI.color = Color.white;

            hudTextStyle.normal.textColor = Defs.INK;
            GUI.Label(new Rect(ix + 22, iy - 1, itemW - 56, itemH), e.name, hudTextStyle);
            hudValueStyle.normal.textColor = e.color;
            GUI.Label(new Rect(ix + itemW - 38, iy - 1, 34, itemH), e.value, hudValueStyle);

            idx++;
        }

        int rows = Mathf.CeilToInt(effects.Count / (float)perRow);
        y += rows * (itemH + 5f) + 2f;
    }

    public void DrawDraftOverlay(float gameplayWidth, float screenHeight, Texture2D texWhite, Font font)
    {
        if (!HasDraft) return;
        EnsureStyles(font);

        GUI.color = new Color(0.02f, 0.04f, 0.08f, 0.82f);
        GUI.DrawTexture(new Rect(0, 0, gameplayWidth, screenHeight), texWhite);
        GUI.color = Color.white;

        float overlayY = Mathf.Lerp(screenHeight * 0.18f + 30f, screenHeight * 0.18f, draftAnim);
        GUI.Label(new Rect(gameplayWidth / 2f - 220f, overlayY - 48f, 440f, 36f), "波次结束，选择一张卡牌", overlayTitleStyle);

        float cardW = Mathf.Min(220f, gameplayWidth / 3f - 24f);
        float gap = 12f;
        float totalW = cardW * offered.Count + gap * (offered.Count - 1);
        float startX = (gameplayWidth - totalW) / 2f;

        for (int i = 0; i < offered.Count; i++)
        {
            CardDef card = offered[i];
            float lift = Mathf.Sin((Time.unscaledTime + i * 0.37f) * 2.6f) * 4f;
            Rect rect = new Rect(startX + i * (cardW + gap), overlayY + lift, cardW, 176f);

            GUI.color = new Color(Defs.CARD_BG.r, Defs.CARD_BG.g, Defs.CARD_BG.b, 0.96f);
            GUI.DrawTexture(rect, texWhite);
            GUI.color = new Color(card.color.r, card.color.g, card.color.b, 0.9f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 4), texWhite);
            GUI.color = new Color(card.color.r, card.color.g, card.color.b, 0.18f);
            GUI.DrawTexture(new Rect(rect.x + 8, rect.y + 14, rect.width - 16, 36), texWhite);
            GUI.color = Color.white;

            titleStyle.normal.textColor = card.color;
            GUI.Label(new Rect(rect.x + 14, rect.y + 16, rect.width - 28, 24), card.title, titleStyle);
            descStyle.normal.textColor = Defs.INK;
            GUI.Label(new Rect(rect.x + 14, rect.y + 58, rect.width - 28, 56), card.desc, descStyle);
            chipStyle.normal.textColor = card.color;
            GUI.Label(new Rect(rect.x + 14, rect.y + 118, rect.width - 28, 18), "即时生效 / 下波联动", chipStyle);

            Rect btnRect = new Rect(rect.x + 14, rect.y + 140, rect.width - 28, 24);
            GUI.color = new Color(card.color.r, card.color.g, card.color.b, 0.16f);
            GUI.DrawTexture(btnRect, texWhite);
            GUI.color = Color.white;
            buttonTextStyle.normal.textColor = card.color;
            GUI.Label(new Rect(btnRect.x + 10, btnRect.y + 2, btnRect.width - 20, 20), "选择这张卡牌", buttonTextStyle);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                Pick(card);
                break;
            }
        }
    }
}
