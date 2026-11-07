using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 冰块（Issue #29）的规格：数据 → 物件、单块 / 整组破坏、地图重置恢复、冰面识别。
/// 红热边融冰和 v2 碰撞依赖真实碰撞回调，放在 Tuning Lab 里手工验收。
/// </summary>
public sealed class LabIceTests
{
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

        root = new GameObject("IceRoot");
        root.transform.SetParent(labGo.transform, false);

        LabIce.All.Clear();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
    }

    // 生成管子：关卡数据 → 场景物件 --------------------------------------

    [UnityTest]
    public IEnumerator LevelDataBuildsOneIcePerCell()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        level.Data.PlaceIce(new Vector2Int(-6, 0), 5);
        level.Data.PlaceIce(new Vector2Int(3, 4), 5);
        level.Data.PlaceIce(new Vector2Int(4, 4), 7);
        level.Data.Normalize();

        level.BuildRuntime(root.transform, slick);

        var ices = root.GetComponentsInChildren<LabIce>();
        Assert.AreEqual(3, ices.Length, "一格一块冰");

        Assert.AreEqual(new Vector3(-5.5f, 0.5f, 0f), ices[0].transform.position, "按格排序后落在格中心");
        Assert.AreEqual(new Vector3(3.5f, 4.5f, 0f), ices[1].transform.position);
        Assert.AreEqual(new Vector3(4.5f, 4.5f, 0f), ices[2].transform.position);
        Assert.AreEqual(5, ices[0].groupId);
        Assert.AreEqual(7, ices[2].groupId);

        for (int i = 0; i < ices.Length; i++)
        {
            var body = ices[i].GetComponent<BoxCollider2D>();
            Assert.IsFalse(body.isTrigger, "冰块是实体碰撞，能站");
            Assert.AreEqual(Vector2.one, body.size, "碰撞 = 1×1");
            Assert.IsNotNull(ices[i].GetComponent<SpriteRenderer>().sprite, "冰块有看得见的贴图");
        }

        yield return null;
    }

    // 组色 ---------------------------------------------------------------

    [Test]
    public void IceGroupColorsAreDistinctForManyGroups()
    {
        var seen = new HashSet<Color32>();
        for (int groupId = 0; groupId < LabIce.GroupColorCount; groupId++)
        {
            Color32 color = LabIce.GroupColor(groupId);
            Assert.IsTrue(seen.Add(color), "组号 " + groupId + " 的组色与前面的组重复");
        }

        Assert.AreEqual(LabIce.DefaultBorderColor, LabIce.GroupColor(0), "0 号组使用深蓝边框");
        Assert.AreNotEqual(LabIce.DefaultColor, LabIce.GroupColor(0), "0 号组边框和填充不能同色");
        Assert.AreNotEqual(LabIce.GroupColor(1), LabIce.GroupColor(2), "相邻组号颜色不同");
    }

    [UnityTest]
    public IEnumerator RuntimeIceKeepsBlueFillAndUsesPerGroupBorder()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        level.Data.PlaceIce(new Vector2Int(0, 0), 1);
        level.Data.PlaceIce(new Vector2Int(2, 0), 2);
        level.Data.Normalize();

        level.BuildRuntime(root.transform, slick);

        var ices = root.GetComponentsInChildren<LabIce>();
        Assert.AreEqual(2, ices.Length, "两格各一块冰");

        var first = ices[0].GetComponent<SpriteRenderer>();
        var second = ices[1].GetComponent<SpriteRenderer>();
        Assert.AreEqual(LabIce.DefaultColor, first.color, "填充保持默认冰蓝");
        Assert.AreEqual(LabIce.DefaultColor, second.color, "填充保持默认冰蓝");

        Assert.IsNotNull(ices[0].GroupBorder, "1 号组有边框");
        Assert.IsNotNull(ices[1].GroupBorder, "2 号组有边框");

        // 边框色 = 组色 RGB + BorderAlpha，柔和一点但组别仍然分得清。
        Color expected1 = LabIce.GroupColor(1);
        Color expected2 = LabIce.GroupColor(2);
        expected1.a *= LabIce.BorderAlpha;
        expected2.a *= LabIce.BorderAlpha;
        Assert.AreEqual(expected1, ices[0].GroupBorder.color, "1 号组边框色");
        Assert.AreEqual(expected2, ices[1].GroupBorder.color, "2 号组边框色");
        Assert.AreNotEqual(ices[0].GroupBorder.color, ices[1].GroupBorder.color,
            "不同组的边框颜色不同");

        var texture = ices[0].GroupBorder.sprite.texture;
        Assert.AreEqual(1f, texture.GetPixel(0, 0).a, 0.02f, "组色边框不透明");
        Assert.AreEqual(0f, texture.GetPixel(texture.width / 2, texture.height / 2).a,
            0.02f, "边框中间透明，露出蓝色填充");

        // 线宽：#88 —— 可见可分辨，但只占 1 像素（整格的 1/16），不能糊成厚边。
        // 量 x 方向要取中间那一行：贴边的整行都在边框带上，用 y=0 横着量没有意义。
        int midRow = texture.height / 2;
        Assert.AreEqual(1, LabIce.BorderPixels, "边框线宽按 #88 定成 1 像素");
        Assert.AreEqual(1f, texture.GetPixel(0, midRow).a, 0.02f, "最外沿 1 像素是边框");
        Assert.AreEqual(0f, texture.GetPixel(LabIce.BorderPixels, midRow).a, 0.02f,
            "第 2 像素起就是透明填充，说明边框只有 1 像素宽");
        yield return null;
    }

    // 破坏 ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator BreakOneLeavesTheRestOfTheGroupIntact()
    {
        var first = MakeIce(new Vector2Int(0, 0), 3);
        var second = MakeIce(new Vector2Int(1, 0), 3);
        var otherGroup = MakeIce(new Vector2Int(2, 0), 4);

        first.BreakOne();

        Assert.IsTrue(first.Broken, "被接触的那块碎了");
        Assert.IsFalse(second.Broken, "同组其它块不受影响");
        Assert.IsFalse(otherGroup.Broken, "别的组不受影响");
        Assert.IsFalse(first.GetComponent<BoxCollider2D>().enabled, "碎掉的冰块碰撞关闭");
        Assert.IsFalse(first.GetComponent<SpriteRenderer>().enabled, "碎掉的冰块不再渲染");

        Assert.AreEqual(2, LabIce.GroupOf(3).Count, "静态组表仍能查到同组冰块");
        yield return null;
    }

    [UnityTest]
    public IEnumerator BreakGroupBreaksOnlyThatGroup()
    {
        var a = MakeIce(new Vector2Int(0, 0), 3);
        var b = MakeIce(new Vector2Int(1, 0), 3);
        var c = MakeIce(new Vector2Int(2, 0), 4);

        a.BreakGroup();

        Assert.IsTrue(a.Broken && b.Broken, "同组全部碎");
        Assert.IsFalse(c.Broken, "别组保留");
        Assert.AreEqual(2, LabIce.GroupOf(3).Count, "破坏后对象仍留在组表里，等待重置");
        yield return null;
    }

    [UnityTest]
    public IEnumerator ResetIceRestoresColliderAndRenderer()
    {
        var ice = MakeIce(new Vector2Int(0, 0), 1);
        ice.BreakOne();
        Assert.IsTrue(ice.Broken);

        ice.ResetIce();

        Assert.IsFalse(ice.Broken, "重置后不再是破坏态");
        Assert.IsTrue(ice.GetComponent<BoxCollider2D>().enabled, "碰撞恢复");
        Assert.IsTrue(ice.GetComponent<SpriteRenderer>().enabled, "渲染恢复");
        yield return null;
    }

    /// <summary>
    /// #78：冰并进地形的 CompositeCollider2D 之后，BoxCollider2D.enabled 不再代表
    /// "这一格挡不挡人"，语义读数必须能区分破坏和恢复。这条就是当初永远为红的那条。
    /// </summary>
    [UnityTest]
    public IEnumerator MergedIceReportsSolidStateThroughSemantics()
    {
        MakeIceLevel(1);
        yield return null;
        Physics2D.SyncTransforms();

        var ice = LabIce.All[0];
        Assert.IsTrue(ice.IsSolidAtCell(), "合并地形里的冰初始挡得住人");

        ice.BreakOne();
        Physics2D.SyncTransforms();
        Assert.IsTrue(ice.Broken, "破坏态");
        Assert.IsFalse(ice.IsSolidAtCell(), "破坏后不再挡人");

        ice.ResetIce();
        Physics2D.SyncTransforms();
        Assert.IsFalse(ice.Broken, "重置后不再是破坏态");
        Assert.IsTrue(ice.IsSolidAtCell(), "重置后重新挡得住人");
    }

    // 冰面识别 -----------------------------------------------------------

    [UnityTest]
    public IEnumerator StandingOnIceIsDetected()
    {
        MakeFloor();
        MakeIce(new Vector2Int(0, 0), 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));

        yield return WaitUntil(() => a.Grounded, 2f);
        Assert.IsTrue(a.OnIce, "站在冰块上时 OnIce 为真");
        yield return null;
    }

    [UnityTest]
    public IEnumerator StandingOnNormalFloorIsNotIce()
    {
        MakeFloor();

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, a.HalfHeight));

        yield return WaitUntil(() => a.Grounded, 2f);
        Assert.IsFalse(a.OnIce, "普通地面不算冰面");
        yield return null;
    }

    [UnityTest]
    public IEnumerator StandingOnLevelIceIsDetected()
    {
        MakeIceLevel(3);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));

        yield return WaitUntil(() => a.Grounded, 2f);
        Assert.IsTrue(a.OnIce, "合并地形里脚下是冰也应识别为冰面");
        yield return null;
    }

    [UnityTest]
    public IEnumerator SlidingOnLevelIceAtV2DoesNotBreakOrSnag()
    {
        MakeIceLevel(30);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));
        yield return WaitUntil(() => a.Grounded && a.OnIce, 2f);

        float startSpeed = lab.IceBreakSpeed + 4f;
        float startX = a.transform.position.x;
        a.SetVelocity(new Vector2(startSpeed, 0f));
        float minSpeed = startSpeed;
        for (int i = 0; i < 30; i++)
        {
            yield return new WaitForFixedUpdate();
            minSpeed = Mathf.Min(minSpeed, a.Velocity.x);
        }

        var ices = root.GetComponentsInChildren<LabIce>();
        for (int i = 0; i < ices.Length; i++)
            Assert.IsFalse(ices[i].Broken, "冰面 v2 滑行不应碎冰，第 " + i + " 块碎了");

        Assert.Greater(a.transform.position.x, startX + 10f,
            "冰面 v2 滑行不应卡脚；实际位移 " + (a.transform.position.x - startX));
        Assert.Greater(minSpeed, startSpeed - 1f,
            "冰面接缝不应吃掉横向速度，最低 " + minSpeed);
        Assert.IsTrue(a.OnIce, "滑行后仍应判定在冰面");
    }

    // 参数 ---------------------------------------------------------------

    [Test]
    public void IceBreakTierDefaultsToV2AndCapIsAboveIt()
    {
        Assert.AreEqual(2, lab.IceBreakTier, "默认按 v2 判定");
        Assert.AreEqual(2, lab.TierOfSpeed(lab.IceBreakSpeed + 0.01f),
            "刚超过门槛速度就是 v2");
        Assert.AreEqual(1, lab.TierOfSpeed(lab.IceBreakSpeed - 0.01f),
            "低于门槛仍是 v1");
        Assert.Greater(lab.IceMaxSpeed, lab.IceBreakSpeed, "冰面加速上限高于破坏门槛");
    }

    // 滑行 ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator SlidingAcrossIceAtV2DoesNotBreakTheGroup()
    {
        MakeFloor();
        var row = new System.Collections.Generic.List<LabIce>();
        for (int x = 0; x < 20; x++) row.Add(MakeIce(new Vector2Int(x, 0), 1));

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);
        Assert.IsTrue(a.OnIce, "起始站在冰面上");

        a.SetVelocity(new Vector2(lab.IceBreakSpeed + 4f, 0f));
        for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

        Assert.IsFalse(row[0].Broken, "在冰面上滑行不应触发整组破坏");
        Assert.IsFalse(row[19].Broken, "滑过的冰块也不应被破坏");
    }

    [UnityTest]
    public IEnumerator SlidingAcrossIceKeepsHorizontalSpeed()
    {
        MakeFloor();
        for (int x = 0; x < 30; x++) MakeIce(new Vector2Int(x, 0), 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        const float startSpeed = 24f;
        a.SetVelocity(new Vector2(startSpeed, 0f));
        float minSpeed = startSpeed;
        for (int i = 0; i < 30; i++)
        {
            yield return new WaitForFixedUpdate();
            minSpeed = Mathf.Min(minSpeed, a.Velocity.x);
        }

        Assert.Greater(minSpeed, startSpeed - 1f,
            "滑过冰块接缝时不应突然掉速，最低 " + minSpeed);
    }

    [UnityTest]
    public IEnumerator SlidingAcrossIceAtHighSpeedKeepsHorizontalSpeed()
    {
        MakeFloor();
        for (int x = 0; x < 40; x++) MakeIce(new Vector2Int(x, 0), 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        const float startSpeed = 38f;
        a.SetVelocity(new Vector2(startSpeed, 0f));
        float minSpeed = startSpeed;
        for (int i = 0; i < 30; i++)
        {
            yield return new WaitForFixedUpdate();
            minSpeed = Mathf.Min(minSpeed, a.Velocity.x);
        }

        Assert.Greater(minSpeed, startSpeed - 1f,
            "高速滑过冰块接缝时不应突然掉速，最低 " + minSpeed);
    }

    [UnityTest]
    public IEnumerator RuntimeIceGroupBreaksTogether()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        for (int x = 0; x < 14; x++) level.Data.PlaceIce(new Vector2Int(x, 0), 1);
        level.Data.Normalize();
        level.BuildRuntime(root.transform, slick);

        var ices = root.GetComponentsInChildren<LabIce>();
        Assert.AreEqual(14, ices.Length, "运行时生成 14 块同组冰");
        Assert.AreEqual(14, LabIce.GroupOf(1).Count, "静态组表包含全部 14 块");

        ices[0].BreakGroup();

        for (int i = 0; i < ices.Length; i++)
            Assert.IsTrue(ices[i].Broken, "同组第 " + i + " 块也应碎");
        yield return null;
    }

    [UnityTest]
    public IEnumerator HittingIceSideAtV2StillBreaksTheGroup()
    {
        MakeFloor();
        var ice = MakeIce(new Vector2Int(1, 0), 1);
        var neighbor = MakeIce(new Vector2Int(2, 0), 1);   // 同组，侧撞应一起碎

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.1f, a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        a.SetVelocity(new Vector2(lab.IceBreakSpeed + 4f, 0f));
        yield return WaitUntil(() => ice.Broken, 2f);

        Assert.IsTrue(ice.Broken, "侧撞 v2 应破坏这一块");
        Assert.IsTrue(neighbor.Broken, "同组冰块应一起碎");
    }

    [UnityTest]
    public IEnumerator SlidingAcrossDifferentIceGroupsKeepsSpeed()
    {
        MakeFloor();
        for (int x = 0; x < 30; x++)
            MakeIce(new Vector2Int(x, 0), x == 15 ? 5 : 1);   // 中间插一块别组

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        const float startSpeed = 24f;
        a.SetVelocity(new Vector2(startSpeed, 0f));
        float minSpeed = startSpeed;
        for (int i = 0; i < 30; i++)
        {
            yield return new WaitForFixedUpdate();
            minSpeed = Mathf.Min(minSpeed, a.Velocity.x);
        }

        Assert.Greater(minSpeed, startSpeed - 1f,
            "经过不同分组冰块交界时不应突然掉速，最低 " + minSpeed);
        Assert.IsTrue(a.OnIce, "滑过交界后仍应判定在冰面上");
    }

    [UnityTest]
    public IEnumerator HotEdgeStandingOnIceDoesNotMeltTheSupportBlock()
    {
        MakeFloor();
        var row = new System.Collections.Generic.List<LabIce>();
        for (int x = 0; x < 20; x++) row.Add(MakeIce(new Vector2Int(x, 0), 1));

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, 1f + a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);
        Assert.IsTrue(a.OnIce);

        a.StrikeEdge(LabPlayer.Edge.Bottom, true);   // 底边 Hot2
        a.SetVelocity(new Vector2(10f, 0f));          // 低速滑过多个接缝
        for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

        for (int i = 0; i < row.Count; i++)
            Assert.IsFalse(row[i].Broken, "承重接触不应融掉脚下的冰块，第 " + i + " 块碎了");
    }

    [UnityTest]
    public IEnumerator HotEdgeSideImpactAtV2BreaksWholeGroup()
    {
        MakeFloor();
        var first = MakeIce(new Vector2Int(1, 0), 1);
        var second = MakeIce(new Vector2Int(2, 0), 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.1f, a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        a.StrikeEdge(LabPlayer.Edge.Right, true);     // 侧撞会落在右边
        a.SetVelocity(new Vector2(lab.IceBreakSpeed + 4f, 0f));
        yield return WaitUntil(() => first.Broken, 2f);

        Assert.IsTrue(first.Broken, "v2 侧撞应破坏被撞块");
        Assert.IsTrue(second.Broken, "v2 撞击优先于红热，整组一起碎");
    }

    [UnityTest]
    public IEnumerator HotEdgeLowSpeedSideImpactMeltsOnlyOneBlock()
    {
        MakeFloor();
        var first = MakeIce(new Vector2Int(1, 0), 1);
        var second = MakeIce(new Vector2Int(2, 0), 1);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.1f, a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        a.StrikeEdge(LabPlayer.Edge.Right, true);
        a.SetVelocity(new Vector2(15f, 0f));
        yield return WaitUntil(() => first.Broken, 2f);
        for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

        Assert.IsTrue(first.Broken, "低速红热侧撞融掉被接触的一块");
        Assert.IsFalse(second.Broken, "低速红热只融一块，不整组");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Right),
            "融化后该边回普通");
    }

    [UnityTest]
    public IEnumerator SideImpactOnLevelIceAtV2BreaksGroup()
    {
        MakeIceOnFloorLevel();
        var ices = root.GetComponentsInChildren<LabIce>();
        Assert.AreEqual(2, ices.Length);

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        a.SetVelocity(new Vector2(lab.IceBreakSpeed + 4f, 0f));
        yield return WaitUntil(() => ices[0].Broken, 2f);

        Assert.IsTrue(ices[0].Broken, "合并地形里的侧面 v2 撞击也应碎冰");
        Assert.IsTrue(ices[1].Broken, "同组一起碎");
    }

    [UnityTest]
    public IEnumerator HotEdgeOnLevelIceMeltsOneBlock()
    {
        MakeIceOnFloorLevel();
        var ices = root.GetComponentsInChildren<LabIce>();

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        a.Teleport(new Vector2(0.5f, a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);

        a.StrikeEdge(LabPlayer.Edge.Right, true);
        a.SetVelocity(new Vector2(15f, 0f));
        yield return WaitUntil(() => ices[0].Broken, 2f);
        for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

        Assert.IsTrue(ices[0].Broken, "红热边侧撞应融掉被接触的一块");
        Assert.IsFalse(ices[1].Broken, "只融一块，不整组");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, a.GetEdge(LabPlayer.Edge.Right),
            "融化后该边回普通");
    }

    // 工具 ---------------------------------------------------------------

    void MakeFloor()
    {
        var go = new GameObject("Floor");
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(0f, -1f, 0f);
        go.transform.localScale = new Vector3(60f, 2f, 1f);
        go.AddComponent<BoxCollider2D>().sharedMaterial = slick;
    }

    LabIce MakeIce(Vector2Int cell, int groupId)
    {
        var spec = new LabIceSpec(cell, groupId);
        var go = new GameObject("Ice");
        go.transform.SetParent(root.transform, false);
        go.transform.position = spec.Center;

        var ice = go.AddComponent<LabIce>();
        ice.cell = cell;
        ice.groupId = groupId;

        var body = go.GetComponent<BoxCollider2D>();
        if (body != null) body.sharedMaterial = slick;

        ice.ApplyShape();
        return ice;
    }

    /// <summary>用正式生成路径铺一排冰，走 LabLevel 的合并碰撞。</summary>
    LabLevel MakeIceLevel(int count)
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        for (int x = 0; x < count; x++) level.Data.PlaceIce(new Vector2Int(x, 0), 1);
        level.Data.Normalize();
        level.BuildRuntime(root.transform, slick);
        return level;
    }

    /// <summary>实心地板上放两块同组冰，用来测合并地形里的侧面撞击和红热融冰。</summary>
    LabLevel MakeIceOnFloorLevel()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        for (int x = 0; x < 11; x++) level.Data.AddCell(new Vector2Int(x, -1));
        level.Data.PlaceIce(new Vector2Int(2, 0), 1);
        level.Data.PlaceIce(new Vector2Int(3, 0), 1);
        level.Data.Normalize();
        level.BuildRuntime(root.transform, slick);
        return level;
    }

    LabPlayer MakePlayer(string name, int pixels, bool isFrame)
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
        return p;
    }

    static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds)
    {
        float elapsed = 0f;
        while (!condition() && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        Assert.IsTrue(condition(), $"等待条件超时（{timeoutSeconds}s）");
    }

    // #108：直角拐角处的冰撞不碎 ------------------------------------------

    /// <summary>
    /// Issue #108 的布局：(0,1) 一块孤立的冰，正下方 (0,0) 接一整排冰，形成直角。
    /// 从右侧水平撞向 (0,1)，速度达到破坏门槛时它必须碎。
    /// </summary>
    [UnityTest]
    public IEnumerator IceAtRightAngleCornerBreaksWhenHitFromTheSide()
    {
        var level = root.AddComponent<LabLevel>();
        lab.level = level;
        for (int x = 0; x <= 8; x++) level.Data.PlaceIce(new Vector2Int(x, 0), 1);   // 下面一整排
        level.Data.PlaceIce(new Vector2Int(0, 1), 1);                               // 拐角那块
        level.Data.Normalize();
        level.BuildRuntime(root.transform, slick);
        yield return null;
        Physics2D.SyncTransforms();

        LabIce corner = null;
        for (int i = 0; i < LabIce.All.Count; i++)
            if (LabIce.All[i].cell == new Vector2Int(0, 1)) { corner = LabIce.All[i]; break; }
        Assert.IsNotNull(corner, "拐角那块冰要存在");

        var a = MakePlayer("A_Box", TuningLab.A_Pixels, true);
        // 站在第二行冰顶上，贴着 (0,1) 的右侧，水平向左撞
        a.Teleport(new Vector2(2.5f, 1f + a.HalfHeight));
        yield return WaitUntil(() => a.Grounded, 2f);
        yield return new WaitForFixedUpdate();

        float speed = lab.IceBreakSpeed + 4f;
        a.SetVelocity(new Vector2(-speed, 0f));

        // 逐帧记录：位置、速度、撞击读数，失败时能直接看出卡在哪一步
        string trace = "";
        for (int i = 0; i < 20; i++)
        {
            yield return new WaitForFixedUpdate();
            trace += i + ": x=" + a.transform.position.x.ToString("F2")
                + " v=" + a.Velocity.x.ToString("F1")
                + " tier=" + a.LastImpactTier
                + " speed=" + a.LastImpactSpeed.ToString("F1")
                + " n=" + a.LastImpactNormal
                + " grounded=" + a.Grounded + "\n";
        }

        Assert.IsTrue(corner.Broken,
            "从右侧撞直角拐角处的冰，达到破坏速度就该碎。实际 x=" +
            a.transform.position.x.ToString("F2") + " speed=" + speed.ToString("F1") +
            " 门槛=" + lab.IceBreakSpeed.ToString("F1") + "\n" + trace);
    }
}
