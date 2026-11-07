using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Issue #82：两块贴在一起时，只有在"真撞上"或"有人持续提供推力"时才该有撞击音效；
/// 静止贴贴不该反复播报。用 <see cref="TuningLab.PlayerHit"/> 的次数当音效探针
/// （撞击音就是在这个事件的订阅者里播的）。
/// </summary>
public sealed class LabImpactSfxTests
{
    const float AHalf = TuningLab.A_Pixels / (2f * TuningLab.CellPixels);
    const float BHalf = TuningLab.B_Pixels / (2f * TuningLab.CellPixels);

    GameObject labGo;
    GameObject root;
    TuningLab lab;
    PhysicsMaterial2D slick;

    [SetUp]
    public void SetUp()
    {
        Physics2D.gravity = new Vector2(0f, -9.81f);
        slick = new PhysicsMaterial2D("Slick") { friction = 0f, bounciness = 0f };

        labGo = new GameObject("Lab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        lab.autoBuild = false;
        labGo.SetActive(true);

        root = new GameObject("Root");
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.Destroy(root);
        if (labGo != null) Object.Destroy(labGo);
        Object.Destroy(slick);
    }

    LabPlayer MakePlayer(string name, int pixels, Color color)
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
        go.AddComponent<SpriteRenderer>().color = color;
        return p;
    }

    void Link(LabPlayer a, LabPlayer b) { a.other = b; b.other = a; }

    void MakeFloor()
    {
        var go = new GameObject("Floor");
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(0f, -1f, 0f);
        go.transform.localScale = new Vector3(60f, 2f, 1f);
        go.AddComponent<BoxCollider2D>().sharedMaterial = slick;
    }

    /// <summary>两人肩并肩贴住，等它们贴稳。</summary>
    IEnumerator SettleSideBySide(LabPlayer a, LabPlayer b)
    {
        a.Teleport(new Vector2(0f, AHalf));
        b.Teleport(new Vector2(AHalf + BHalf + 0.001f, BHalf));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
    }

    [UnityTest]
    public IEnumerator TwoPlayersRestingSideBySideDoNotKeepReportingHits()
    {
        MakeFloor();
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, TuningLab.ColorA);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, TuningLab.ColorB);
        Link(a, b);
        yield return SettleSideBySide(a, b);

        int hits = 0;
        lab.PlayerHit += _ => hits++;
        hits = 0;

        // 贴住不动静置 1 秒，这个窗口里一次都不该报（旧实现会报 5 次，约每个冷却一次）。
        float t = 0f;
        while (t < 1f)
        {
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        Assert.AreEqual(0, hits, "静止贴贴不该反复报撞击（#82）");
    }

    /// <summary>
    /// 反向护栏：贴住之后有人持续推着，仍然要算撞击。
    /// 否则就是靠"把功能砍掉"来让上一条变绿。
    /// </summary>
    [UnityTest]
    public IEnumerator PushingWhileTouchingStillCountsAsHit()
    {
        MakeFloor();
        var a = MakePlayer("A_Box", TuningLab.A_Pixels, TuningLab.ColorA);
        var b = MakePlayer("B_Chunk", TuningLab.B_Pixels, TuningLab.ColorB);
        Link(a, b);
        yield return SettleSideBySide(a, b);

        // 对撞有 0.18s 冷却，贴住的那一下已经把冷却用掉了，先等它过期。
        float wait = 0f;
        while (wait < 0.25f) { wait += Time.fixedDeltaTime; yield return new WaitForFixedUpdate(); }

        int hits = 0;
        lab.PlayerHit += _ => hits++;
        hits = 0;

        // B 以远高于锻造门槛的闭合速度撞向 A。
        b.SetVelocity(Vector2.left * lab.ForgeSpeed * 1.5f);
        float t = 0f;
        while (t < 0.4f) { t += Time.fixedDeltaTime; yield return new WaitForFixedUpdate(); }

        Assert.Greater(hits, 0, "有人带着足够速度推上来时必须算撞击，不能把撞击整个砍掉");
    }
}
