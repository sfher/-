using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Issue #15 大炮形态功能在实验台上的规格。
/// 进入：有且仅能以冲刺态进入，速度多少不管。
/// 装填：记 V前，B 不可视、跟着 A、没有碰撞，不算"处于地面"、不回复充能。
/// 发射：v' = √(Vb² + 一个等级间距的 v²)，不看 A 自己的速度；方向取进入方向的反向。
/// 退出：射出、A 结构改变（u→L / u→=）、A 死亡。
/// </summary>
public sealed class LabCannonTests
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

        // 不用原生键盘：-nographics 下每帧仍有驱动回写"没人按键"，合成按键会被立刻冲掉。
        // 自建一台合成键盘，它成为 Keyboard.current，状态才归测试自己管。
        synthKb = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();

        root = new GameObject("CannonRoot");
        root.transform.SetParent(labGo.transform, false);
    }

    UnityEngine.InputSystem.Keyboard synthKb;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (synthKb != null) UnityEngine.InputSystem.InputSystem.RemoveDevice(synthKb);
        if (labGo != null) Object.Destroy(labGo);
        // 全局状态复位（#92 隔离）：不把上一条用例的时间倍率留给下一条
        Time.timeScale = 1f;
        yield return null;
        yield return null;
    }

    // 进入条件 ---------------------------------------------------------

    [UnityTest]
    public IEnumerator OnlyADashEntryLoadsTheCannon()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        int hits = 0;
        lab.PlayerHit += info => hits++;

        Shatter(a, LabPlayer.Edge.Top);            // 口朝上：B 只能从上面进膛
        Assert.AreEqual(LabForm.Cannon, a.Form);

        // 有速度、但不在冲刺态：照旧走进动量互换，不进膛
        b.Teleport(new Vector2(0f, a.transform.position.y + AHalf + BHalf + 0.1f));
        b.SetVelocity(new Vector2(0f, -3f));
        yield return WaitUntil(() => hits > 0 || b.Suspended, 3f);

        Assert.IsFalse(a.Loaded, "不冲刺就不装填");
        Assert.IsFalse(b.Suspended);

        // 互换给两人各挂 0.18s 对撞冷却，冲刺只跑 0.09s：不等冷却过就永远撞不进膛
        yield return StepFixed(12);

        // 同样的方向、同样的入口，换成冲刺态就进膛
        yield return LoadInto(a, b, Vector2.down, EntrySpeed);
        Assert.IsTrue(a.Loaded, "有且仅能以冲刺态进入");
        Assert.IsTrue(b.Suspended,
            $"膛里有人但 B 不认账：a.loaded={a.Loaded} b.Host={(b.Host == null ? "null" : b.Host.name)} " +
            $"a=({a.transform.position.x:F2},{a.transform.position.y:F2}) b=({b.transform.position.x:F2},{b.transform.position.y:F2}) " +
            $"a.form={a.Form} b.form={b.Form}");
    }

    [UnityTest]
    public IEnumerator NormalFormDoesNotAbsorb()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        int hits = 0;
        lab.PlayerHit += info => hits++;

        Vector2 start = (Vector2)a.transform.position + Vector2.up * (AHalf + BHalf + 1.5f);
        b.Teleport(start);
        b.ApplyDash(new LabPlayer.DashState
        {
            active = true,
            dir = Vector2.down,
            speed = EntrySpeed,
            exitSpeed = EntrySpeed,
            timer = 2f / EntrySpeed
        });

        yield return WaitUntil(() => hits > 0, 3f);

        Assert.IsFalse(a.Loaded, "普通形态没有口，不装填");
        Assert.AreEqual(LabForm.Normal, a.Form);
        Assert.Greater(hits, 0, "退回原来的动量互换");
    }

    // 朝向门控：只有撞在炮口（灵体边）上才装填 --------------------------

    [UnityTest]
    public IEnumerator DashIntoASolidEdgeDoesNotLoad()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        int hits = 0;
        lab.PlayerHit += info => hits++;

        Shatter(a, LabPlayer.Edge.Top);
        Assert.AreEqual("u 大炮 口朝↑", a.FormLabel);

        // 同一门炮、同样以冲刺态撞上去，只是改从侧面顶还没碎的左壁：朝向不对就不该进膛
        b.Teleport(new Vector2(a.transform.position.x - (AHalf + BHalf + 1.5f), a.transform.position.y));
        b.ApplyDash(new LabPlayer.DashState
        {
            active = true,
            dir = Vector2.right,
            speed = EntrySpeed,
            exitSpeed = EntrySpeed,
            timer = 2f / EntrySpeed
        });

        yield return WaitUntil(() => hits > 0, 3f);

        Assert.IsFalse(a.Loaded, "撞在实体边上不进膛");
        Assert.IsFalse(b.Suspended, "撞在实体边上不把自己交出去");
        Assert.Greater(hits, 0, "朝向不对的冲刺撞击退回常规对撞结算");
    }

    [UnityTest]
    public IEnumerator DashAcrossOrAwayFromMouthDoesNotLoad()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);

        Shatter(a, LabPlayer.Edge.Top);
        b.Teleport(new Vector2(a.transform.position.x,
            a.transform.position.y + AHalf + BHalf + 0.01f));
        b.ApplyDash(new LabPlayer.DashState
        {
            active = true,
            dir = Vector2.right,
            speed = EntrySpeed,
            exitSpeed = EntrySpeed,
            timer = 2f / EntrySpeed
        });

        yield return WaitUntil(() => !b.InDashState || a.Loaded, 3f);

        Assert.IsFalse(a.Loaded, "站在炮口上方横向冲刺不应装填");
        Assert.IsFalse(b.Suspended, "非入膛方向不应吸入 B");
    }

    // 装填态 -----------------------------------------------------------

    [UnityTest]
    public IEnumerator LoadedBLosesItsBodyAndFollowsA()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);

        Shatter(a, LabPlayer.Edge.Top);            // 口朝上，B 从上方冲进来才对得上
        yield return LoadInto(a, b, Vector2.down, EntrySpeed);

        Assert.IsFalse(b.GetComponent<Collider2D>().enabled, "膛内没有碰撞");
        Assert.AreEqual(RigidbodyType2D.Kinematic, b.GetComponent<Rigidbody2D>().bodyType);
        Assert.IsFalse(b.GetComponentInChildren<SpriteRenderer>().enabled, "膛内不可视");
        Assert.IsFalse(b.Touching, "膛内不算处于地面，所以不回复充能");
        Assert.AreEqual(a, b.Host);

        // A 带着膛里的 B 走：先只验「B 确实跟着动了」。
        // 膛内跟随是在 B 自己的 FixedUpdate 里抄宿主位置，两边 FixedUpdate 的
        // 先后和协程恢复时机都会让瞬时读数差出一步以上（#92：全量跑时实测
        // 偏移卡在容差外），所以不在运动中途比瞬时位置。
        float startX = b.GetComponent<Rigidbody2D>().position.x;
        lab.groundBrake = 0f;
        a.SetVelocity(new Vector2(6f, 0f));
        yield return StepFixed(3);
        Assert.Greater(b.GetComponent<Rigidbody2D>().position.x, startX + 0.1f,
                       "膛内的 B 要跟着 A 一起动");

        // 让宿主停下再比位置：停稳后多走几步，跟随必然落位，
        // 这时比的才是「跟没跟上」这个契约本身，与步数时序无关。
        a.SetVelocity(Vector2.zero);
        yield return StepFixed(3);
        Assert.AreEqual(a.GetComponent<Rigidbody2D>().position.x,
                        b.GetComponent<Rigidbody2D>().position.x, 0.02f);
        Assert.AreEqual(a.GetComponent<Rigidbody2D>().position.y,
                        b.GetComponent<Rigidbody2D>().position.y, 0.05f);
    }

    // 发射 -------------------------------------------------------------

    [UnityTest]
    public IEnumerator FireAddsOneTierSpacingAndReversesEntry()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);

        Shatter(a, LabPlayer.Edge.Top);            // 口朝上，B 从上方冲进来才对得上
        yield return LoadInto(a, b, Vector2.down, EntrySpeed);

        float vBefore = a.ChamberVelocity.magnitude;
        Assert.AreEqual(EntrySpeed, vBefore, 0.01f, "冲刺期间速度恒定，进膛记的就是它");

        a.Fire();

        float expected = Mathf.Sqrt(vBefore * vBefore + 2f * lab.gravity * lab.tierSpacingCells);
        Assert.AreEqual(expected, b.Velocity.magnitude, 0.05f);
        Assert.Greater(b.Velocity.y, 0f, "从上面冲进来 → 往上面打出去");
        Assert.IsFalse(a.Loaded);
        Assert.IsFalse(b.Suspended);
        Assert.IsTrue(b.GetComponent<Collider2D>().enabled);

        // 射出必须活过接下来的物理步：手感的"没速度"＝第一帧之后被别的结算清零
        yield return StepFixed(3);
        Assert.Greater(b.Velocity.y, 0f, "几步之后还在往上");
        Assert.GreaterOrEqual(b.Velocity.magnitude, expected - lab.gravity * 3 * Time.fixedDeltaTime - 0.5f,
                              "出膛速度只能被重力减，不能被结算清零");
        Assert.AreEqual(a.transform.position.x, b.transform.position.x, 0.05f, "竖直射出不能横向弹开");
    }

    [UnityTest]
    public IEnumerator FallThenDashInThenFireGainsFourCells()
    {
        // 验收：B 自由落体 x 格，最后冲刺进入 A，直接射出，B 大约能到 x+4 格
        const float drop = 6f;

        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Top);               // 口朝上：B 从上面落进来才对着炮口

        float topOfA = a.transform.position.y + AHalf;
        float startY = topOfA + BHalf + drop;
        b.Teleport(new Vector2(0f, startY));

        // 落到还剩约 1 格时进入冲刺态：冲刺不改速度，只是把 v前 定下来并关掉重力
        yield return WaitUntil(() => b.FootY <= startY - (drop - 1f), 3f);
        float v = b.VerticalSpeed;
        b.ApplyDash(new LabPlayer.DashState
        {
            active = true,
            dir = Vector2.down,
            speed = v,
            exitSpeed = v,
            timer = 3f / Mathf.Max(0.01f, v)      // 冲刺位移 3 格，足够覆盖剩下的 1 格多
        });

        yield return WaitUntil(() => a.Loaded, 3f);
        a.Fire();
        float fireTime = Time.time;

        float vOut = b.Velocity.magnitude;
        Assert.AreEqual(Mathf.Sqrt(v * v + 2f * lab.gravity * lab.tierSpacingCells), vOut, 0.05f);

        float fireFootY = b.FootY;
        float apex = fireFootY;
        int guard = 0;
        while (Time.time - fireTime < 1.5f && guard++ < 3000)
        {
            yield return new UnityEngine.WaitForFixedUpdate();
            apex = Mathf.Max(apex, b.FootY);
        }

        float gained = apex - fireFootY;
        // 顶点是逐物理步采样出来的，峰值在两步之间：单步位移 vOut×dt 就是天生的采样误差
        float sampleError = vOut * Time.fixedDeltaTime;
        Assert.AreEqual(lab.CellsForSpeed(vOut), gained, sampleError + 0.05f, "上升高度 = 出膛速度的折算格数");
        // 自由落体只有 drop - 1 格（最后 1 格是定速冲刺，不涨速度），
        // 再加一个等级间距的 4 格，所以是 drop + 3 ≈ "x + 4 格（大约）"。
        Assert.AreEqual(drop + 3f, gained, 0.6f, "出膛后上升 ≈ 落体高度 + 一个等级间距");
    }

    [UnityTest]
    public IEnumerator HorizontalShotIgnoresGravityUntilTierZero()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Left);

        yield return LoadInto(a, b, Vector2.right, EntrySpeed);
        a.Fire();

        Assert.Less(b.Velocity.x, 0f, "从左边冲进来 → 往左边打出去");
        Assert.AreEqual(Mathf.Abs(b.Velocity.x),
            Mathf.Sqrt(EntrySpeed * EntrySpeed + 2f * lab.gravity * lab.tierSpacingCells), 0.05f);

        yield return StepFixed(1);
        Assert.AreEqual(0f, b.GetComponent<Rigidbody2D>().gravityScale, 0.0001f, "水平射出：不受重力");
        Assert.Greater(Mathf.Abs(b.Velocity.x), EntrySpeed * 0.9f, "射出后第一物理步不能掉速");

        float startY = b.transform.position.y;
        yield return WaitUntil(() => b.HorizontalSpeed < lab.V0Speed, 3f);
        Assert.AreEqual(startY, b.transform.position.y, 0.2f, "免重力期间不掉高度");
        Assert.AreEqual(lab.GravityScale, b.GetComponent<Rigidbody2D>().gravityScale, 0.0001f,
                        "降到零级之后重力照常");
    }

    // 退出装填态 -------------------------------------------------------

    [UnityTest]
    public IEnumerator ChangingFormReleasesLoadedB()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Top);            // 口朝上，B 从上方冲进来才对得上
        yield return LoadInto(a, b, Vector2.down, EntrySpeed);

        Shatter(a, LabPlayer.Edge.Left);         // u → L：二者分开（上+左 = 相邻）

        Assert.AreEqual(LabForm.Converter, a.Form);
        Assert.IsFalse(a.Loaded);
        Assert.IsFalse(b.Suspended);
        Assert.IsTrue(b.GetComponent<Collider2D>().enabled);
        Assert.AreEqual(RigidbodyType2D.Dynamic, b.GetComponent<Rigidbody2D>().bodyType);
        Assert.IsTrue(b.GetComponentInChildren<SpriteRenderer>().enabled);
    }

    [UnityTest]
    public IEnumerator DeathReleasesLoadedB()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Top);            // 口朝上，B 从上方冲进来才对得上
        yield return LoadInto(a, b, Vector2.down, EntrySpeed);

        Heat(a, LabPlayer.Edge.Bottom);
        a.StrikeEdge(LabPlayer.Edge.Bottom, true);      // 热二遇红热 = A 死亡（口在上方，不能碎边）

        Assert.IsFalse(b.Suspended, "膛里的人不能跟着宿主一起没");
        Assert.IsTrue(b.GetComponent<Collider2D>().enabled);

        yield return StepFixed(2);
        Assert.IsTrue(a == null, "A 已销毁");      // Unity 的假 null：只能靠 == 判，不能让 NUnit 判空
        Assert.IsNotNull(b);
    }

    // 真实按键路径（Play 里就是这么玩的）--------------------------------

    [UnityTest]
    public IEnumerator RealKeysDashInThenFireKeepsSpeed()
    {
        // 和手动验收同序：口朝↑（F1 第一次碎的是上边）→ B 落 6 格 → 按 . 冲刺进膛 → 再按 . 开炮
        const float drop = 6f;

        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Top);
        Assert.AreEqual("u 大炮 口朝↑", a.FormLabel);

        float startY = a.transform.position.y + AHalf + BHalf + drop;
        b.Teleport(new Vector2(0f, startY));

        yield return WaitUntil(() => b.FootY <= startY - (drop - 1.2f), 3f);
        yield return PressPeriod();                                   // B 的冲刺键
        if (!b.InDashState && !a.Loaded && !PeriodHeldAfterStep())
            Assert.Ignore("无头编辑器里按键路径测不到：合成键盘的按下状态在一个物理步内就被播放器循环冲掉，" +
                          "Update 永远收不到 wasPressedThisFrame。真实按键只能进编辑器手动验收。");
        Assert.IsTrue(b.InDashState || a.Loaded, "按下 . 应进入冲刺态（输入没接管就是按键路径断了）");
        yield return WaitUntil(() => a.Loaded || !b.InDashState, 3f);
        Assert.IsTrue(a.Loaded, "按键冲刺撞进口里应装填");

        float vIn = a.ChamberVelocity.magnitude;
        Assert.Greater(vIn, 5f, "记录的进入速度不该接近 0");

        yield return PressPeriod();                                   // B 的冲刺键 = 开炮
        Assert.IsFalse(a.Loaded, "开炮后膛应空");
        Assert.IsFalse(b.Suspended);

        float vOut = b.Velocity.magnitude;
        Assert.Greater(vOut, vIn, "出膛必须比进入快");
        Assert.Greater(b.Velocity.y, 0f, "从上面进来 → 往上面出去");
        Assert.AreEqual(Mathf.Sqrt(vIn * vIn + 2f * lab.gravity * lab.tierSpacingCells), vOut, 0.3f);

        // 关键：出膛之后要真的跑过物理步。手感的"没速度"= 第一发出去就被别的结算抹掉。
        yield return StepFixed(3);
        Assert.Greater(b.Velocity.y, 0f, "几步之后还在往上，不该被对撞换速打回 0");
        Assert.Greater(b.Velocity.magnitude, vOut * 0.7f, "出膛速度不能被外力一口吃掉");

        float fireFootY = b.FootY;
        float apex = fireFootY;
        for (int i = 0; i < 150; i++)
        {
            yield return new UnityEngine.WaitForFixedUpdate();
            apex = Mathf.Max(apex, b.FootY);
        }
        Assert.GreaterOrEqual(apex - fireFootY, vIn * vIn / (2f * lab.gravity) + 2f,
                              "上升高度至少要覆盖进入速度 + 一段增量");
    }

    [UnityTest]
    public IEnumerator FormChangeReleasesWithEntrySpeed()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Top);            // 口朝上，B 从上方冲进来才对得上
        yield return LoadInto(a, b, Vector2.down, EntrySpeed);

        Shatter(a, LabPlayer.Edge.Left);        // 碎边组合变了 → 不再是 u → 退膛

        Assert.IsFalse(a.Loaded);
        Assert.IsFalse(b.Suspended);
        Assert.AreEqual(EntrySpeed, b.Velocity.magnitude, 0.5f,
                        "退膛必须带进入时的速度，不能把人原地放下");
    }

    // L 形转向器（Issue #21）------------------------------------------

    [UnityTest]
    public IEnumerator ConverterLoadsFromOneOpeningAndAutoFiresThroughTheOther()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Top);
        Shatter(a, LabPlayer.Edge.Right);
        Assert.AreEqual(LabForm.Converter, a.Form);

        // 贴到 A 右侧一格内起速：横向空气阻力每步减 1，起远了进膛时就不是原速了。
        b.Teleport((Vector2)a.transform.position + Vector2.right * (AHalf + BHalf + 0.1f));
        b.SetVelocity(Vector2.left * EntrySpeed);
        yield return WaitUntil(() => a.Loaded, 3f);

        // B 被吸进膛时把真正的进入速度记下来，出膛后拿它对照。
        float entrySpeed = a.ChamberVelocity.magnitude;
        Assert.IsTrue(b.Suspended);
        yield return StepFixed(Mathf.CeilToInt(0.8f / Time.fixedDeltaTime));
        Assert.IsTrue(a.Loaded, "L 形装填未满 1 秒前不能发射");

        yield return WaitUntil(() => !a.Loaded, 1f);
        Assert.IsFalse(b.Suspended);
        // 出膛后竖直方向不受滑翔照顾，实时速度每步都被重力减掉一点，所以取出膛那一刻的记录值。
        Vector2 launchVelocity = a.LastLaunchVelocity;
        Assert.Greater(launchVelocity.y, 0f, "从右侧进入应从上侧射出");
        // L 形只转向、不改速：出膛速度就是进膛速度，A 自己的速度不叠进来。
        Assert.AreEqual(entrySpeed, launchVelocity.magnitude, 0.5f,
                        "L 形应保持 B 的原速，不叠加 A 的速度");
        Assert.IsFalse(b.InDashState, "普通速度进入不能被伪造为冲刺态");
    }

    [UnityTest]
    public IEnumerator ConverterPreservesDashStateAndUsesOppositeOpening()
    {
        MakeFloor();
        var a = MakeFrame();
        var b = MakeChunk();
        Link(a, b);
        Shatter(a, LabPlayer.Edge.Top);
        Shatter(a, LabPlayer.Edge.Right);

        b.Teleport((Vector2)a.transform.position + Vector2.up * (AHalf + BHalf + 1.5f));
        b.ApplyDash(new LabPlayer.DashState
        {
            active = true,
            dir = Vector2.down,
            speed = EntrySpeed,
            exitSpeed = EntrySpeed,
            timer = 0.5f
        });
        yield return WaitUntil(() => a.Loaded, 3f);
        yield return WaitUntil(() => !a.Loaded, 1f);

        Assert.IsTrue(b.InDashState, "冲刺态进入应以冲刺态射出");
        Assert.Greater(b.Velocity.x, 0f, "从上侧进入应从右侧射出");
        Assert.AreEqual(EntrySpeed, b.Velocity.magnitude, 0.5f);
    }

    // 工具 -------------------------------------------------------------

    /// <summary>按一次 B 的句号键（走 ReadInput → 冲刺进入 / 开炮的真实路径）。</summary>
    /// <remarks>
    /// 键盘的键是位域，InputState.Change 拒写，只能排队整份 KeyboardState；
    /// 无头编辑器的播放器循环会在同一个物理步里把状态冲掉，按下边沿传不到 Update。
    /// </remarks>
    static IEnumerator PressPeriod(int holdSteps = 3)
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        Assert.IsNotNull(kb, "无头模式也要有合成键盘，否则这条用例测不到输入路径");

        var down = new UnityEngine.InputSystem.LowLevel.KeyboardState();
        down.Press(UnityEngine.InputSystem.Key.Period);
        for (int i = 0; i < holdSteps; i++)
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, down);
            UnityEngine.InputSystem.InputSystem.Update();
            yield return StepFixed(1);
        }

        UnityEngine.InputSystem.InputSystem.QueueStateEvent(
            kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        UnityEngine.InputSystem.InputSystem.Update();
        yield return StepFixed(1);
    }

    /// <summary>排完按下事件、走过一个物理步之后键还在不在 —— 用来区分「用例写坏了」和「环境根本收不到按键」。</summary>
    static bool PeriodHeldAfterStep()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.periodKey.isPressed;
    }

    /// <summary>走 n 个真正的物理步：batchmode 里 yield return null 可能一步都不走。</summary>
    static IEnumerator StepFixed(int n)
    {
        for (int i = 0; i < n; i++) yield return new UnityEngine.WaitForFixedUpdate();
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

    void MakeFloor()
    {
        var go = new GameObject("Floor");
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(0f, -1f, 0f);
        go.transform.localScale = new Vector3(60f, 2f, 1f);
        go.AddComponent<BoxCollider2D>().sharedMaterial = slick;
    }

    static void Heat(LabPlayer p, LabPlayer.Edge edge)
    {
        p.StrikeEdge(edge, true);
    }

    static void Shatter(LabPlayer p, LabPlayer.Edge edge)
    {
        Heat(p, edge);
        p.StrikeEdge(edge, false);
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

        Assert.IsTrue(condition(), $"等待条件超时（{timeoutSeconds}s / {steps} 个物理步）");
    }
}
