using UnityEngine;

/// <summary>
/// 运行时的调参面板 + 读数。用 IMGUI 是为了零资源依赖：
/// 打开 Play 就能调，不碰场景、不碰 prefab、不改工程设置。
/// </summary>
public class LabHud : MonoBehaviour
{
    TuningLab lab;

    GUIStyle label;
    GUIStyle small;
    GUIStyle title;
    Font cjkFont;
    bool stylesReady;

    int hitCount;
    TuningLab.HitInfo lastHit;
    bool hasHit;

    int collapseCount;
    string lastCollapse = "";

    public void Bind(TuningLab target)
    {
        UnbindEdges();
        if (lab != null) lab.PlayerHit -= OnPlayerHit;
        lab = target;
        if (lab != null) lab.PlayerHit += OnPlayerHit;
        if (lab != null)
        {
            if (lab.PlayerA != null) lab.PlayerA.EdgeCollapsed += OnEdgeCollapsed;
            if (lab.PlayerB != null) lab.PlayerB.EdgeCollapsed += OnEdgeCollapsed;
        }
    }

    void UnbindEdges()
    {
        if (lab == null) return;
        if (lab.PlayerA != null) lab.PlayerA.EdgeCollapsed -= OnEdgeCollapsed;
        if (lab.PlayerB != null) lab.PlayerB.EdgeCollapsed -= OnEdgeCollapsed;
    }

    void OnDisable()
    {
        if (lab != null) lab.PlayerHit -= OnPlayerHit;
        UnbindEdges();
    }

    // 这里演示"其它机制靠碰撞触发"：订阅事件就行，不用改 LabPlayer。
    void OnPlayerHit(TuningLab.HitInfo info)
    {
        hitCount++;
        lastHit = info;
        hasHit = true;
    }

    void OnEdgeCollapsed(LabPlayer p, LabPlayer.Edge e)
    {
        collapseCount++;
        lastCollapse = (p == lab.PlayerA ? "A" : "B") + " 的" + EdgeName(e);
    }

    static string EdgeName(LabPlayer.Edge e)
    {
        switch (e)
        {
            case LabPlayer.Edge.Top: return "上边";
            case LabPlayer.Edge.Bottom: return "下边";
            case LabPlayer.Edge.Left: return "左边";
            default: return "右边";
        }
    }

    void EnsureStyles()
    {
        if (stylesReady) return;
        stylesReady = true;

        // Unity 自带的 GUI 字体没有中文字形，这里直接拿系统字体。
        cjkFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun", "Arial" }, 14);

        label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false };
        small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = false };
        title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        if (cjkFont != null)
        {
            label.font = cjkFont;
            small.font = cjkFont;
            title.font = cjkFont;
        }
        label.normal.textColor = new Color(0.92f, 0.94f, 0.98f);
        small.normal.textColor = new Color(0.72f, 0.76f, 0.84f);
        title.normal.textColor = Color.white;
    }

    void OnGUI()
    {
        if (lab == null) return;
        EnsureStyles();

        Time.timeScale = lab.timeScale;

        DrawTuningPanel();
        DrawReadout();
        DrawRuler();
    }

    // ------------------------------------------------------------------

    void DrawTuningPanel()
    {
        GUILayout.BeginArea(new Rect(10f, 10f, 340f, Screen.height - 20f), GUI.skin.box);

        GUILayout.Label("手感调试台", title);
        GUILayout.Label("A（大框）  A/D 移动   W/S 冲刺方向   空格 跳   左Shift 冲刺", small);
        GUILayout.Label("B（小块）  方向键 上下左右   /  跳   句号 .  冲刺", small);
        GUILayout.Label("膛里装了 B 时：B 的移动无效，句号 . 变成开炮", small);
        GUILayout.Label("向下冲刺只允许在空中（站在地上按下去无效，不耗充能）", small);
        GUILayout.Label("冲刺方向 竖直输入 > 水平输入 > 空中的竖直速度 > 朝向", small);
        GUILayout.Label("R 重置    P 像素网格    F1 碎一条边（演示形态）    F2 恢复边框", small);
        GUILayout.Space(6f);

        lab.timeScale = Slider("时间缩放", lab.timeScale, 0.05f, 2f, "0.00");
        lab.gravity = Slider("重力（格/s²）", lab.gravity, 10f, 120f, "0.0");
        lab.jumpCells = Slider("跳高（格）", lab.jumpCells, 0.5f, 4f, "0.00");
        lab.walkSpeed = Slider("走路速度", lab.walkSpeed, 2f, 20f, "0.0");
        lab.walkAccel = Slider("起步加速度", lab.walkAccel, 20f, 400f, "0");
        lab.groundBrake = Slider("地面制动", lab.groundBrake, 0f, 200f, "0");
        lab.airBrake = Slider("空中制动", lab.airBrake, 0f, 60f, "0");
        lab.dashLockTime = Slider("冲刺锁输入", lab.dashLockTime, 0f, 0.4f, "0.00");
        lab.dashCooldown = Slider("冲刺充能时间", lab.dashCooldown, 0.1f, 3f, "0.00");
        lab.dashDistanceCells = Slider("冲刺位移(格)", lab.dashDistanceCells, 0f, 8f, "0.0");
        lab.dashOverV0Cells = Slider("Vd1 高出 v0(格)", lab.dashOverV0Cells, 0.01f, 1f, "0.00");
        lab.maxFallSpeed = Slider("最大下落速度", lab.maxFallSpeed, 10f, 60f, "0.0");

        GUILayout.Space(4f);
        lab.holdToJump = GUILayout.Toggle(lab.holdToJump, " 按住跳跃连跳", small);
        lab.horizontalAirDrag = GUILayout.Toggle(lab.horizontalAirDrag, " 水平空气制动 = g", small);
        lab.groundOnlyDash = GUILayout.Toggle(lab.groundOnlyDash, " 只能在地面冲刺", small);
        lab.verticalHitEnabled = GUILayout.Toggle(lab.verticalHitEnabled, " 允许竖直撞击", small);

        GUILayout.Space(6f);
        GUILayout.Label("等级（等距）", title);
        lab.tierCount = Mathf.RoundToInt(Slider("等级数（不含 v0）", lab.tierCount, 1f, 6f, "0"));
        lab.tierFirstCells = Slider("首级门槛(格)", lab.tierFirstCells, 0.5f, 10f, "0.0");
        lab.tierSpacingCells = Slider("每级间距(格)", lab.tierSpacingCells, 0.5f, 6f, "0.0");
        lab.redHotTierIndex = Mathf.RoundToInt(Slider("红热门槛级", lab.redHotTierIndex, 0f, 4f, "0"));
        lab.forgeTierIndex = Mathf.RoundToInt(Slider("锻造门槛级", lab.forgeTierIndex, 0f, 4f, "0"));
        lab.iceBreakTierIndex = Mathf.RoundToInt(Slider("冰块破坏级", lab.iceBreakTierIndex, 0f, 4f, "0"));
        lab.redHotDuration = Slider("红热1持续(秒)", lab.redHotDuration, 0.2f, 6f, "0.00");

        GUILayout.Space(6f);
        if (GUILayout.Button("重置玩家 (R)")) lab.ResetPlayers();
        if (GUILayout.Button("紧凑预设 (g=50)")) ApplyPreset(50f);
        if (GUILayout.Button("舒缓预设 (g=25)")) ApplyPreset(25f);

        GUILayout.EndArea();
    }

    void ApplyPreset(float g)
    {
        lab.gravity = g;
        lab.jumpCells = 2.2f;
        lab.walkSpeed = 8f;
        lab.walkAccel = 140f;
        lab.groundBrake = 30f;
        lab.airBrake = 0f;
        lab.dashCooldown = 0.8f;
        lab.tierCount = 4;
        lab.tierFirstCells = 4f;
        lab.tierSpacingCells = 4f;
        lab.redHotTierIndex = 1;
        lab.forgeTierIndex = 0;
        lab.redHotDuration = 2.25f;
        lab.maxFallSpeed = TierMath.SpeedForCells(17f, g);
    }

    float Slider(string name, float value, float min, float max, string format)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, small, GUILayout.Width(120f));
        float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(130f));
        GUILayout.Label(v.ToString(format), small, GUILayout.Width(52f));
        GUILayout.EndHorizontal();
        return v;
    }

    // ------------------------------------------------------------------

    void DrawReadout()
    {
        float w = 330f;
        GUILayout.BeginArea(new Rect(Screen.width - w - 10f, 10f, w, Screen.height - 20f), GUI.skin.box);

        GUILayout.Label("读数", title);
        if (lab.PlayerA == null) GUILayout.Label("P1 塌框死亡：相机改绑 P2，尺寸不再伸缩", WarnStyle());
        else DrawPlayer(lab.PlayerA, "P1");
        GUILayout.Space(6f);
        if (lab.PlayerB == null) GUILayout.Label("P2 塌框死亡：相机改绑 P1，尺寸不再伸缩", WarnStyle());
        else DrawPlayer(lab.PlayerB, "P2");

        GUILayout.Space(10f);
        GUILayout.Label("诊断", title);

        // 站跳落地：按最高点算，正好等于跳高
        float jumpV = lab.JumpVelocity;
        float jumpCells = lab.CellsForSpeed(jumpV);
        int jumpTier = lab.TierOfCells(jumpCells);
        bool jumpLeaks = jumpTier >= 1;
        GUILayout.Label(
            string.Format("站跳落地  v={0:0.0}  ≈{1:0.00} 格  → v{2}{3}",
                jumpV, jumpCells, jumpTier, jumpLeaks ? "  ⚠ 站跳就能充能" : "  ✓"),
            jumpLeaks ? WarnStyle() : small);

        GUILayout.Label(string.Format("冲刺      位移恒定 {0:0.0} 格   Vd1={1:0.00}（≈{2:0.00} 格，v0={3:0.00}）",
            lab.DashDistance, lab.Vd1, lab.CellsForSpeed(lab.Vd1), lab.V0Speed), small);
        GUILayout.Label("  反向进入 → 冲刺 Vd1，结束 0", small);
        GUILayout.Label("  零级同向 → 冲刺 Vd1，结束 V前", small);
        GUILayout.Label("  非零级同向 → 冲刺 V前，结束 V前", small);

        GUILayout.Label(string.Format("红热      Vp ≥ {0:0}（v{1} 门槛）→ 参与碰撞的边加热",
            lab.RedHotSpeed * lab.RedHotSpeed, lab.redHotTierIndex), small);
        GUILayout.Label(string.Format("锻造      撞场景沿轴 v ≥ {0:0}（v{1} 门槛）→ 常态①→热②→遇冷砸碎◇→遇热塌框",
            lab.ForgeSpeed, Mathf.Min(lab.forgeTierIndex, lab.redHotTierIndex)), small);
        GUILayout.Label(string.Format("冰块      法线撞击 ≥ {0:0}（v{1} 门槛）→ 整组碎（优先）；红热非承重接触 → 只融一块",
            lab.IceBreakSpeed, lab.IceBreakTier), small);
        if (collapseCount > 0)
            GUILayout.Label(string.Format("  塌框 {0} 次   最近 {1}（热二遇红热 = 死亡）", collapseCount, lastCollapse), WarnStyle());
        GUILayout.Label(string.Format("大炮      v' = √(Vb² + {0:0})   一个等级间距 = {1:0.0} 格   方向 = 进入方向的反向",
            lab.CannonBoostV2, lab.tierSpacingCells), small);
        GUILayout.Label("  碎 1 条边 = u 大炮，相邻 2 = L，相对 2 = =/‖，只剩 1 = 矛，全碎 = 灵体", small);
        GUILayout.Label("碰撞      速度 + 冲刺态 整体互换（动量守恒）", small);
        if (hasHit)
        {
            string who = lastHit.hitter == lab.PlayerA ? "A" : "B";
            string whom = lastHit.victim == lab.PlayerA ? "A" : "B";
            GUILayout.Label(string.Format("  碰撞 {0} 次   最近 {1}→{2}  Vp {3:0}  v{4}→v{5}  {6}",
                hitCount, who, whom, lastHit.vp, lastHit.tierBefore, lastHit.tierAfter,
                lastHit.horizontal ? "水平轴" : "竖直轴"), small);
        }

        GUILayout.Space(6f);
        GUILayout.Label("等级门槛", title);
        for (int i = 0; i < lab.Bounds.Length; i++)
        {
            float cells = lab.Bounds[i];
            float sp = lab.SpeedForCells(cells);
            GUILayout.Label(string.Format("v{0}/v{1}  {2:0.0} 格  v={3:0.0}  v²={4:0}",
                i, i + 1, cells, sp, sp * sp), small);
        }

        GUILayout.Space(6f);
        GUILayout.Label("像素密度", title);
        GUILayout.Label(string.Format("1 格 = {0} px   原生 {1}×{2}",
            TuningLab.CellPixels, TuningLab.RefWidth, TuningLab.RefHeight), small);
        GUILayout.Label(string.Format("A {0}×{0} px（墙 2，内腔 {1}）  B {2}×{2} px",
            TuningLab.A_Pixels, TuningLab.A_Pixels - 4, TuningLab.B_Pixels), small);
        GUILayout.Label(string.Format("可见 {0:0.0} × {1:0.0} 格   屏幕 {2}×{3}",
            lab.VisibleCellsX, lab.VisibleCellsY, Screen.width, Screen.height), small);

        float texelScale = lab.TexelScale;
        bool aligned = texelScale > 0.01f && Mathf.Abs(texelScale - Mathf.Round(texelScale)) < 0.02f;
        GUILayout.Label(string.Format("缩放 {0:0.00}×  {1}", texelScale,
            aligned ? "✓ 逐像素对齐" : "⚠ 非整数倍，像素会大小不一"),
            aligned ? small : WarnStyle());

        lab.showPixelGrid = GUILayout.Toggle(lab.showPixelGrid, " 像素网格 (P)", small);
        lab.showGrid = GUILayout.Toggle(lab.showGrid, " 格子网格 (Tab)", small);

        GUILayout.Space(6f);
        GUILayout.Label("说明", title);
        GUILayout.Label("· 拖尾/颜色 = 当前等级；数字 = 持有等级（本 demo 二者合一）", small);
        GUILayout.Label("· 水平冲刺后速度仍在 v1 区间时不受重力（悬停）；竖直冲刺不变", small);
        GUILayout.Label("· 充能只认站在地面或碰到队友，贴墙不算", small);
        GUILayout.Label("· 空中制动 = 0 时，v1/v2 可以一直保持着飞", small);
        GUILayout.Label("· 站跳落地和从 2 格高处走下来，速度完全一样", small);
        GUILayout.Label("· 所以「降重力」不能把站跳排除在 v1 之外，只能抬门槛", small);

        GUILayout.EndArea();
    }

    /// <summary>膛内状态：是否装填、是否可装填，以及这一发会打出多大速度。</summary>
    string ChamberText(LabPlayer a)
    {
        if (a.ConverterLoaded)
            return string.Format("转向装填  {0:0.0}s 后自动从另一缺口射出（原速）",
                a.ConverterTimeLeft);
        if (a.Loaded)
            return string.Format("已装填  Vb={0:0.0} → v'={1:0.0}（B 的句号开炮）",
                a.ChamberVelocity.magnitude, a.LaunchSpeed);
        if (a.Form == LabForm.Converter) return "L 转向器  空膛（速度高于 v0 即可进入）";
        if (a.CanLoad) return "空膛  可装填（B 以冲刺态撞进口里）";
        return "不装填（不是 u 形或 L 形）";
    }

    static string EdgeGlyph(LabPlayer.EdgeState s)
    {
        if (s == LabPlayer.EdgeState.Hot2) return "②";
        if (s == LabPlayer.EdgeState.Hot1) return "①";
        if (s == LabPlayer.EdgeState.Spirit) return "◇";
        return "·";
    }

    GUIStyle warn;
    GUIStyle WarnStyle()
    {
        if (warn == null)
        {
            warn = new GUIStyle(small);
            warn.normal.textColor = new Color(1f, 0.55f, 0.35f);
        }
        return warn;
    }

    void DrawPlayer(LabPlayer p, string name)
    {
        if (p == null) return;

        GUILayout.BeginHorizontal();
        var prev = GUI.color;
        GUI.color = LabPlayer.TintForTier(p.SpeedTier);
        GUILayout.Label("■", label, GUILayout.Width(16f));
        GUI.color = prev;
        string state = p.OnIce ? "  (冰面)"
            : p.Grounded ? "  (地面)"
            : p.Hovering ? "  (v1 悬停)"
            : p.RidingOnOther ? "  (驮着)"
            : p.Touching ? "  (空中·碰队友)"
            : "  (空中)";
        GUILayout.Label(name + state, label);
        GUILayout.EndHorizontal();

        GUILayout.Label(string.Format("等级 v{0}   速度 {1:0.0}   v² {2:0}   折算 {3:0.00} 格",
            p.SpeedTier, p.Speed, p.Speed * p.Speed, lab.CellsForSpeed(p.Speed)), small);
        GUILayout.Label("　钥匙 " + p.KeyCount, small);

        // 只认轴的游戏里，真正可用的是这两个分轴等级；上面那个合速度等级只是"总量"
        if (p.canBeRedHot)
        {
            GUILayout.Label(string.Format("　边框  上{0} 下{1} 左{2} 右{3}   ◇=碎",
                EdgeGlyph(p.GetEdge(LabPlayer.Edge.Top)),
                EdgeGlyph(p.GetEdge(LabPlayer.Edge.Bottom)),
                EdgeGlyph(p.GetEdge(LabPlayer.Edge.Left)),
                EdgeGlyph(p.GetEdge(LabPlayer.Edge.Right))), small);
            GUILayout.Label("　形态  " + p.FormLabel, small);
            GUILayout.Label("　大炮  " + ChamberText(p), small);
        }
        else if (p.Suspended)
        {
            GUILayout.Label(string.Format("　膛内  跟着 {0}，移动无效，句号开炮。存速 {1:0.0} → 出膛 {2:0.0}",
                p.Host == lab.PlayerA ? "A" : "B",
                p.Host.ChamberVelocity.magnitude, p.Host.LaunchSpeed), small);
        }

        GUILayout.Label(string.Format("　水平 v{0} ({1:0.0})　　竖直 v{2} ({3:0.0})",
            p.HorizontalTier, p.HorizontalSpeed, p.VerticalTier, p.VerticalSpeed), small);

        if (p.LastImpactTier >= 0)
        {
            string axisName = Mathf.Abs(p.LastImpactNormal.x) > 0.5f ? "撞墙"
                : (p.LastImpactNormal.y > 0f ? "撞地面" : "撞天花板");
            GUILayout.Label(string.Format("　最近撞击 {0}  分量 {1:0.0} = {2:0.00} 格 → v{3}",
                axisName, p.LastImpactSpeed, lab.CellsForSpeed(p.LastImpactSpeed), p.LastImpactTier), small);
        }

        GUILayout.Label(string.Format("净落差 {0:0.00} 格 → v{1}    顶点落差 {2:0.00} 格 → v{3}",
            p.NetDescent, p.LaunchTier, p.ApexDescent, p.ApexTier), small);

        GUILayout.BeginHorizontal();
        GUILayout.Label("冲刺充能", small, GUILayout.Width(70f));
        var r = GUILayoutUtility.GetRect(150f, 14f);
        GUI.Box(r, GUIContent.none);
        var fill = new Rect(r.x + 1f, r.y + 1f, (r.width - 2f) * Mathf.Clamp01(p.Charge), r.height - 2f);
        if (p.Charge > 0.01f)
        {
            var prevColor = GUI.color;
            GUI.color = p.Charge >= 1f ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.5f, 0.6f, 0.7f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = prevColor;
        }
        string chargeState = p.Charge >= 1f ? "就绪"
            : p.RefillArmed ? "充能中"
            : "待接触";
        GUILayout.Label(chargeState, small);
        GUILayout.EndHorizontal();
    }

    // ------------------------------------------------------------------

    void DrawRuler()
    {
        var cam = lab.Cam != null ? lab.Cam : Camera.main;
        if (cam == null) return;

        for (int y = 0; y <= 12; y += 2)
        {
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(-11.7f, y, 0f));
            if (sp.z <= 0f) continue;
            var rect = new Rect(sp.x, Screen.height - sp.y - 9f, 40f, 18f);
            GUI.Label(rect, y + "格", small);
        }
    }
}
