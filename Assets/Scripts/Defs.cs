using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// 全局配置 —— 现代化配色 + 音效枚举 + 炮台可移动参数
// =====================================================================
public enum TowerKind { Gun, Cannon, Frost, Laser }
public enum EnemyKind { Slime, Mini, Mech, Dino }
public enum SfxKind
{
    Place, Shoot, LaserHum, Hit, Kill, WaveStart, WaveClear, Error,
    Sell, Upgrade, Reflect, BossRoar, CoreHit, CardDraw, CardPick
}

public struct TowerDef
{
    public string name; public int cost; public float range; public float dmg;
    public float rate; public float bullet; public Color color;
    public float splash; public bool slow; public bool laser; public string desc;
    public bool movable; public int moveRange;          // 炮台可沿路径方向移动的格数
}
public struct EnemyDef
{
    public string name; public float hp; public float speed; public int reward;
    public Color body; public Color accent; public Color eye;
    public float radius; public float armor; public bool boss;
}

public static class Defs
{
    public const int COLS = 16, ROWS = 15;
    public const float SLOW_FACTOR = 0.5f, SLOW_TIME = 1.6f, FROST_VULN = 1.5f;
    public const int LASER_MAX_BOUNCE = 8;
    public const float LASER_MAX_LEN = 40f;
    public const int MIRROR_COST = 30, PRISM_COST = 60;
    public const float PRISM_RADIUS = 0.42f;

    // ---- 银河城 / 恶魔城风格调色板 ----
    public static readonly Color BG         = Hex("#241b33");
    public static readonly Color PANEL_BG   = Hex("#2a1d37");
    public static readonly Color PANEL_TOP  = Hex("#342142");
    public static readonly Color CARD_BG    = Hex("#352348");
    public static readonly Color CARD_HOVER = Hex("#3f2a55");
    public static readonly Color CARD_SEL   = Hex("#4c2f66");
    public static readonly Color BORDER     = Hex("#7f5a86");
    public static readonly Color BORDER_SEL = Hex("#d19bff");
    public static readonly Color INK        = Hex("#f6effa");
    public static readonly Color MUTED      = Hex("#d2bbdc");
    public static readonly Color DIM        = Hex("#9a7fa6");
    public static readonly Color CYAN       = Hex("#74d8ff");
    public static readonly Color AMBER      = Hex("#ffbf63");
    public static readonly Color FROST      = Hex("#89b7ff");
    public static readonly Color GOLD       = Hex("#ffd27b");
    public static readonly Color HP         = Hex("#73df95");
    public static readonly Color DANGER     = Hex("#ff7a9d");
    public static readonly Color MAGENTA    = Hex("#ff88f5");
    public static readonly Color LASER      = Hex("#ff5a8e");
    public static readonly Color LASER_GLOW = Hex("#ffb0c9");
    public static readonly Color GLASS      = Hex("#a6d6ff");
    public static readonly Color WALL       = Hex("#5b4775");
    public static readonly Color PATH       = Hex("#51658a");
    public static readonly Color PATH_EDGE  = Hex("#6e7fa6");

    // 按钮颜色
    public static readonly Color BTN_BG     = Hex("#342142");
    public static readonly Color BTN_HOVER  = Hex("#473058");
    public static readonly Color BTN_TEXT   = Hex("#eddcf6");
    public static readonly Color TITLE_GRAD1 = Hex("#ffd27b");
    public static readonly Color TITLE_GRAD2 = Hex("#c38cff");

    static Color Hex(string hex)
    {
        Color c = Color.magenta;
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }

    public static readonly Dictionary<TowerKind, TowerDef> Towers = new Dictionary<TowerKind, TowerDef>
    {
        { TowerKind.Laser,  new TowerDef{ name="激光炮台", cost=120, range=3.7f, dmg=42, rate=0f,    bullet=0f,  color=LASER,  splash=0f,   slow=false, laser=true,  desc="反射光束 · 穿甲",    movable=true,  moveRange=3 } },
        { TowerKind.Gun,    new TowerDef{ name="机枪台",   cost=50,  range=3.0f, dmg=12, rate=0.40f, bullet=13f, color=CYAN,   splash=0f,   slow=false, laser=false, desc="高射速 · 单体输出",  movable=false, moveRange=0 } },
        { TowerKind.Cannon, new TowerDef{ name="加农炮",   cost=100, range=2.5f, dmg=32, rate=1.05f, bullet=8f,  color=AMBER,  splash=1.2f, slow=false, laser=false, desc="范围溅射 · 群伤",    movable=false, moveRange=0 } },
        { TowerKind.Frost,  new TowerDef{ name="冰冻台",   cost=75,  range=2.7f, dmg=6,  rate=0.7f,  bullet=11f, color=FROST,  splash=0f,   slow=true,  laser=false, desc="减速 + 易伤增幅",    movable=false, moveRange=0 } },
    };

    public static readonly Dictionary<EnemyKind, EnemyDef> Enemies = new Dictionary<EnemyKind, EnemyDef>
    {
        { EnemyKind.Slime, new EnemyDef{ name="暗影游灵", hp=55,   speed=1.5f,  reward=7,   body=Hex("#6c5ce7"), accent=Hex("#a29bfe"), eye=Hex("#dfe6ff"), radius=0.30f, armor=0, boss=false } },
        { EnemyKind.Mini,  new EnemyDef{ name="幽光精灵", hp=30,   speed=2.9f,  reward=5,   body=Hex("#00b894"), accent=Hex("#55efc4"), eye=Hex("#dff9fb"), radius=0.23f, armor=0, boss=false } },
        { EnemyKind.Mech,  new EnemyDef{ name="铁甲魔像", hp=175,  speed=1.1f,  reward=18,  body=Hex("#636e72"), accent=Hex("#b2bec3"), eye=Hex("#ff7675"), radius=0.38f, armor=4, boss=false } },
        { EnemyKind.Dino,  new EnemyDef{ name="深渊巨龙", hp=1500, speed=0.75f, reward=200, body=Hex("#d63031"), accent=Hex("#ff7675"), eye=Hex("#ffeaa7"), radius=0.62f, armor=6, boss=true  } },
    };

    // 蛇形路径
    public static readonly Vector2[] Waypoints = new Vector2[]
    {
        new Vector2(-1.0f,13.5f), new Vector2(13.5f,13.5f), new Vector2(13.5f,10.5f),
        new Vector2(2.5f,10.5f),  new Vector2(2.5f,7.5f),   new Vector2(13.5f,7.5f),
        new Vector2(13.5f,4.5f),  new Vector2(2.5f,4.5f),   new Vector2(2.5f,1.5f),
        new Vector2(16.5f,1.5f),
    };
}
