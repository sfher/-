using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 修复方块（Issue #63）的规格：数据 → 物件、贴着就修、不消耗、不参与锻造状态机，
/// 以及装填态 / 幽魂态不响应。
///
/// 门禁只写在 <see cref="LabPlayer.TryRepairEdge"/> 一处，所以门禁类用例直接调它；
/// "贴上就修"依赖真实碰撞回调，走 LabLevel 的正式生成路径（并进地形的合并碰撞体）。
/// </summary>
public sealed class LabRepairBlockTests
{
    const float AHalf = TuningLab.A_Pixels / (2f * TuningLab.CellPixels);
    const float BHalf = TuningLab.B_Pixels / (2f * TuningLab.CellPixels);
    const float EntrySpeed = 22f;

    GameObject labGo;
    GameObject root;
    TuningLab lab;
    PhysicsMaterial2D slick;

    [SetUp]
    public void SetUp()
    {
        Physics2D.gravity = new Vector2(0f, -9.81f);
        slick = new PhysicsMaterial2D("LabSlick") { friction = 0f, bounciness = 0f };

        labGo = new GameObject("Lab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        lab.autoBuild = false;
        labGo.SetActive(true);

        root = new GameObject("RepairRoot");
        root.transform.SetParent(labGo.transform, false);

        LabRepairBlock.All.Clear();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        LabRepairBlock.All.Clear();
        yield return null;
        yield return null;
    }

    // 生成管子：关卡数据 → 场景物件 --------------------------------------

    [UnityTest]
    public IEnumerator LevelDataBuildsOneRepairBlockPerCell()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        level.Data.PlaceRepairBlock(new Vector2Int(-6, 0));
        level.Data.PlaceRepairBlock(new Vector2Int(3, 4));
        level.Data.Normalize();

        level.BuildRuntime(root.transform, slick);

        var blocks = root.GetComponentsInChildren<LabRepairBlock>();
        Assert.AreEqual(2, blocks.Length, "一格一块修复方块");
        Assert.AreEqual(new Vector3(-5.5f, 0.5f, 0f), blocks[0].transform.position, "按格排序后落在格中心");
        Assert.AreEqual(new Vector3(3.5f, 4.5f, 0f), blocks[1].transform.position);

        for (int i = 0; i < blocks.Length; i++)
        {
            var body = blocks[i].GetComponent<BoxCollider2D>();
            Assert.IsFalse(body.isTrigger, "修复方块是实体碰撞，能站、能挡路");
            Assert.AreEqual(Vector2.one, body.size, "碰撞 = 1×1");
            var sr = blocks[i].GetComponent<SpriteRenderer>();
            Assert.IsNotNull(sr.sprite, "修复方块有看得见的贴图");
            Assert.AreEqual(TuningLab.ColorA, sr.color, "外形和 A 一样蓝");
        }

        yield return null;
    }

    // 贴着就修 -----------------------------------------------------------

    [UnityTest]
    public IEnumerator HotEdgePressedOnRepairFaceReturnsToNormalAndIsNotConsumed()
    {
        MakeRepairLevel(new Vector2Int(0, 0));

        var a = MakeFrame();
        a.Teleport(new Vector2(0.5f, 1f + AHalf));
        yield return WaitUntil(() => a.Grounded, 2f);

        a.StrikeEdge(LabPlayer.Edge.Bottom, true);
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, a.GetEdge(LabPlayer.Edge.Bottom), "先砸成热二");

        yield return WaitUntil(
            () => a.GetEdge(LabPlayer.Edge.Bottom) == LabPlayer.EdgeState.Normal, 2f);
        Assert.IsTrue(a.IsAlive, "热二贴上修复面是修复，不是再吃一记红热撞击");

        // 再来一次：方块不消耗，同一格能反复修
        a.StrikeEdge(LabPlayer.Edge.Bottom, true);
        yield return WaitUntil(
            () => a.GetEdge(LabPlayer.Edge.Bottom) == LabPlayer.EdgeState.Normal, 2f);
        Assert.AreEqual(1, root.GetComponentsInChildren<LabRepairBlock>().Length,
            "用过之后方块还在原处");
    }

    [UnityTest]
    public IEnumerator SpiritEdgePressedOnRepairFaceRestoresShape()
    {
        // 地形铺到 (0,0)..(3,0)，修复方块立在 (4,1)，与玩家同一层。
        //
        // #97 原来的场景把方块摆在 (2,0)、让 A 从 (0.5, AHalf) 开过去：
        // 一来 6 格/秒会被地面制动（groundBrake 30）在 0.2 秒内吃光，
        // 根本走不到方块跟前；二来方块和玩家不在同一层，
        // 那样即使贴上也只有脚底接触、拿不到侧面接触点。两处都不成立。
        // 现在直接把 A 摆在方块左侧同一层，再给一点向右的速度顶上去。
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        for (int x = 0; x <= 3; x++) level.Data.AddCell(new Vector2Int(x, 0));
        level.Data.PlaceRepairBlock(new Vector2Int(4, 1));   // 和玩家同一层
        level.Data.Normalize();
        level.BuildRuntime(root.transform, slick);

        var a = MakeFrame();
        a.Teleport(new Vector2(4f - AHalf - 0.1f, 1f + AHalf));
        for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
        Assert.IsTrue(a.Grounded, "A 要站在地形上");

        Shatter(a, LabPlayer.Edge.Right);
        Assert.AreEqual(LabPlayer.EdgeState.Spirit, a.GetEdge(LabPlayer.Edge.Right));
        Assert.AreEqual(LabForm.Cannon, a.Form, "碎一条边就是 u 形大炮");

        a.SetVelocity(new Vector2(4f, 0f));
        yield return WaitUntil(
            () => a.GetEdge(LabPlayer.Edge.Right) == LabPlayer.EdgeState.Normal, 2f);

        Assert.AreEqual(0, a.SpiritMask, "灵体边回普通后碎边掩码清空");
        Assert.AreEqual(LabForm.Normal, a.Form, "形态跟着碎边掩码重算");
        Assert.AreEqual(1, root.GetComponentsInChildren<LabRepairBlock>().Length,
            "修复方块不会被消耗");
    }

    [UnityTest]
    public IEnumerator StandingOnRepairFaceDoesNotForge()
    {
        MakeRepairLevel(new Vector2Int(0, 0));

        var a = MakeFrame();
        a.Teleport(new Vector2(0.5f, 1f + AHalf));
        yield return WaitUntil(() => a.Grounded, 2f);

        for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();

        Assert.IsFalse(a.AnyHot, "修复面不参与锻造：站着不会点热");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Bottom));
        Assert.IsTrue(a.IsAlive);
    }

    // 门禁：装填态 / 幽魂态 / 普通边 / B --------------------------------

    [UnityTest]
    public IEnumerator LoadedAIsNotRepairable()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Top);          // 口朝上的大炮

        yield return LoadInto(a, b, Vector2.down, EntrySpeed);
        Assert.IsTrue(a.Loaded, "膛里有人");

        Assert.IsFalse(a.TryRepairEdge(LabPlayer.Edge.Top), "装填态不响应修复方块");
        Assert.AreEqual(LabPlayer.EdgeState.Spirit, a.GetEdge(LabPlayer.Edge.Top),
            "炮口没有被修成实体边");
        Assert.IsTrue(b.Suspended, "B 仍留在膛里");
    }

    [UnityTest]
    public IEnumerator SpiritAIsNotRepairable()
    {
        var a = MakeFrame();
        Shatter(a, LabPlayer.Edge.Top);
        Shatter(a, LabPlayer.Edge.Bottom);
        Shatter(a, LabPlayer.Edge.Left);
        Shatter(a, LabPlayer.Edge.Right);
        Assert.AreEqual(LabForm.Spirit, a.Form, "四条边全碎 = 幽魂态");

        for (int i = 0; i < 4; i++)
            Assert.IsFalse(a.TryRepairEdge((LabPlayer.Edge)i),
                "幽魂态修任意一条边等于白送一次重构，不该响应");

        Assert.AreEqual(LabForm.Spirit, a.Form, "形态没有变");
        yield return null;
    }

    [UnityTest]
    public IEnumerator NormalEdgeAndSmallChunkHaveNothingToRepair()
    {
        var a = MakeFrame();
        Assert.IsFalse(a.TryRepairEdge(LabPlayer.Edge.Bottom), "普通边没什么可修");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Bottom));

        var b = MakeChunk();
        Assert.IsFalse(b.TryRepairEdge(LabPlayer.Edge.Bottom), "B 没有「边」这个概念");
        yield return null;
    }

    // 工具 ---------------------------------------------------------------

    /// <summary>用正式生成路径摆几块修复方块（并进地形的合并碰撞体）。</summary>
    LabLevel MakeRepairLevel(params Vector2Int[] cells)
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        for (int i = 0; i < cells.Length; i++) level.Data.PlaceRepairBlock(cells[i]);
        level.Data.Normalize();
        level.BuildRuntime(root.transform, slick);
        return level;
    }

    void MakeFloor()
    {
        var go = new GameObject("Floor");
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(0f, -1f, 0f);
        go.transform.localScale = new Vector3(60f, 2f, 1f);
        go.AddComponent<BoxCollider2D>().sharedMaterial = slick;
    }

    LabPlayer MakeFrame()
    {
        return MakePlayer("A_Box", TuningLab.A_Pixels, true, TuningLab.ColorA);
    }

    LabPlayer MakeChunk()
    {
        return MakePlayer("B_Chunk", TuningLab.B_Pixels, false, TuningLab.ColorB);
    }

    LabPlayer MakePlayer(string name, int pixels, bool isFrame, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector2(0f, pixels / (2f * TuningLab.CellPixels));

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(pixels / (float)TuningLab.CellPixels, pixels / (float)TuningLab.CellPixels);
        bc.sharedMaterial = slick;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = lab.GravityScale;

        var p = go.AddComponent<LabPlayer>();
        p.lab = lab;
        p.pixels = pixels;
        p.canBeRedHot = isFrame;
        p.scheme = isFrame ? LabPlayer.Scheme.P1 : LabPlayer.Scheme.P2;
        p.visual = LabVisual.Build(go.transform, pixels, isFrame, color);
        return p;
    }

    void Link(LabPlayer a, LabPlayer b)
    {
        a.other = b;
        b.other = a;
    }

    /// <summary>把 B 以冲刺态送进膛里：dir = 进入方向，speed = 进入速度。</summary>
    IEnumerator LoadInto(LabPlayer a, LabPlayer b, Vector2 dir, float speed, float gap = 1.5f)
    {
        Vector2 start = (Vector2)a.transform.position - dir * (a.HalfHeight + b.HalfHeight + gap);
        b.Teleport(start);
        b.ApplyDash(new LabPlayer.DashState
        {
            active = true,
            dir = dir,
            speed = speed,
            exitSpeed = speed,
            timer = (gap + 0.5f) / Mathf.Max(0.01f, speed)
        });

        yield return WaitUntil(() => a.Loaded || !b.InDashState, 3f);
        Assert.IsTrue(a.Loaded, "冲刺态撞进 u 形口应装填");
    }

    static void Shatter(LabPlayer p, LabPlayer.Edge edge)
    {
        p.StrikeEdge(edge, true);     // 常态吃红热 → 热二
        p.StrikeEdge(edge, false);    // 热二吃冷 → 灵体
    }

    static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds)
    {
        int steps = 0;
        int maxSteps = Mathf.CeilToInt(timeoutSeconds / Mathf.Max(0.0001f, Time.fixedDeltaTime));
        while (!condition() && steps < maxSteps)
        {
            yield return new UnityEngine.WaitForFixedUpdate();
            steps++;
        }

        Assert.IsTrue(condition(), $"等待条件超时（{timeoutSeconds}s）");
    }
}
