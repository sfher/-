using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LabRespawnPointTests
{
    GameObject labGo;
    TuningLab lab;
    Vector2 previousGravity;

    [SetUp]
    public void SetUp()
    {
        previousGravity = Physics2D.gravity;
        labGo = new GameObject("RespawnPointLab");
        labGo.SetActive(false);
        lab = labGo.AddComponent<TuningLab>();
        labGo.SetActive(true);
        Physics2D.gravity = Vector2.zero;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (labGo != null) Object.Destroy(labGo);
        yield return null;
        yield return null;
        Physics2D.gravity = previousGravity;
    }

    [UnityTest]
    public IEnumerator OnePlayerEnteringMovesBothToFixedSlots()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        var checkpoint = MakeCheckpoint(checkpointPosition);
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);

        Assert.IsTrue(checkpoint.IsTouching(lab.PlayerA), "重生点没有记录进入的 A");
        Assert.AreSame(checkpoint, lab.ActiveRespawnPoint);
        Assert.AreEqual(checkpointPosition.x - 1f, lab.PlayerA.transform.position.x, 0.001f,
            "A 应固定在重生点左侧 1 格");
        Assert.AreEqual(checkpointPosition.y, lab.PlayerA.transform.position.y, 0.001f);
        Assert.AreEqual(checkpointPosition.x + 1f, lab.PlayerB.transform.position.x, 0.001f,
            "B 应固定在重生点右侧 1 格");
        Assert.AreEqual(checkpointPosition.y, lab.PlayerB.transform.position.y, 0.001f);
    }

    [UnityTest]
    public IEnumerator PlayerTeleportedIntoCheckpointDoesNotTrigger()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        var checkpoint = MakeCheckpoint(checkpointPosition);
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);
        int resetCount = 0;
        lab.MapReset += () => resetCount++;

        lab.PlayerA.Teleport(checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(0, resetCount, "被传送/复活直接放进区域不算“进入”");
        Assert.IsNull(lab.ActiveRespawnPoint);

        // 先离开，再走进来，才算一次完整进入。
        lab.PlayerA.Teleport(Outside(checkpointPosition));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(1, resetCount, "离开后重新进入应触发");
        Assert.AreSame(checkpoint, lab.ActiveRespawnPoint);
    }

    [UnityTest]
    public IEnumerator PlayersAlreadyInsideWhenPointStartsDoNotTrigger()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeKinematic(lab.PlayerA, lab.PlayerB);
        lab.PlayerA.Teleport(checkpointPosition);
        lab.PlayerB.Teleport(checkpointPosition + Vector2.right);
        yield return new WaitForFixedUpdate();

        int resetCount = 0;
        lab.MapReset += () => resetCount++;
        var checkpoint = MakeCheckpoint(checkpointPosition);
        yield return null;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(0, resetCount, "启动时就在区域里不算进入");
        Assert.IsNull(lab.ActiveRespawnPoint);

        yield return MoveBothOutside(checkpointPosition);
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(1, resetCount, "全员离开后再进入应重新触发");
        Assert.AreSame(checkpoint, lab.ActiveRespawnPoint);
    }

    [UnityTest]
    public IEnumerator EnteringCheckpointResetsMapButKeepsForms()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        var checkpoint = MakeCheckpoint(checkpointPosition);
        yield return null;

        MakeKinematic(lab.PlayerA, lab.PlayerB);
        lab.PlayerA.Teleport(Outside(checkpointPosition));
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(MakeKey().TryCollect(lab.PlayerA));
        Assert.IsTrue(MakeKey().TryCollect(lab.PlayerB));
        lab.PlayerA.SetEdge(LabPlayer.Edge.Left, LabPlayer.EdgeState.Hot2);
        lab.PlayerB.SetEdge(LabPlayer.Edge.Top, LabPlayer.EdgeState.Spirit);
        lab.PlayerA.SetVelocity(Vector2.right * 4f);
        lab.PlayerB.SetVelocity(Vector2.left * 3f);

        int resetCount = 0;
        lab.MapReset += () => resetCount++;
        MoveBody(lab.PlayerA, checkpointPosition);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(1, resetCount, "每次进入只应重置地图一次");
        Assert.AreEqual(0, lab.PlayerA.KeyCount);
        Assert.AreEqual(0, lab.PlayerB.KeyCount);
        Assert.AreEqual(Vector2.zero, lab.PlayerA.Velocity);
        Assert.AreEqual(Vector2.zero, lab.PlayerB.Velocity);
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, lab.PlayerA.GetEdge(LabPlayer.Edge.Left),
            "活着进点应保留 A 的形态");
        Assert.AreEqual(LabPlayer.EdgeState.Spirit, lab.PlayerB.GetEdge(LabPlayer.Edge.Top),
            "活着进点应保留 B 的边框状态");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerA.GetEdge(LabPlayer.Edge.Top));
        Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerB.GetEdge(LabPlayer.Edge.Left));
        Assert.AreSame(checkpoint, lab.ActiveRespawnPoint);
    }

    [UnityTest]
    public IEnumerator BothPlayersEnteringTogetherTreatItAsOneTrigger()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        MakeKinematic(lab.PlayerA, lab.PlayerB);
        lab.PlayerA.Teleport(Outside(checkpointPosition) + Vector2.left);
        lab.PlayerB.Teleport(Outside(checkpointPosition) + Vector2.right);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        int resetCount = 0;
        lab.MapReset += () => resetCount++;

        MoveBody(lab.PlayerA, checkpointPosition + Vector2.left);
        MoveBody(lab.PlayerB, checkpointPosition + Vector2.right);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(1, resetCount, "两人同时进入仍只视为一次触发");
        Assert.AreEqual(checkpointPosition.x - 1f, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(checkpointPosition.x + 1f, lab.PlayerB.transform.position.x, 0.001f);
    }

    [UnityTest]
    public IEnumerator CheckpointTriggersAgainOnlyAfterEveryoneLeaves()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        MakeKinematic(lab.PlayerA, lab.PlayerB);
        int resetCount = 0;
        lab.MapReset += () => resetCount++;
        yield return Activate(checkpointPosition);
        Assert.AreEqual(1, resetCount);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(1, resetCount, "两人仍在触发器内时不得重复重置");

        yield return MoveBothOutside(checkpointPosition);
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(2, resetCount, "全部离开后重新进入应再次触发");
    }

    [UnityTest]
    public IEnumerator SingleDeathWaitsForTeammate()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);
        yield return MoveBothOutside(checkpointPosition);

        int resetCount = 0;
        lab.MapReset += () => resetCount++;
        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsFalse(lab.PlayerA.IsAlive, "单人死亡应保持死亡等待");
        Assert.IsTrue(lab.PlayerB.IsAlive);
        Assert.AreEqual(0, resetCount, "队友还活着时不重置地图");
        Assert.IsFalse(lab.PlayerA.GetComponent<Collider2D>().enabled, "死亡等待时碰撞箱关闭");
        Assert.AreEqual(RigidbodyType2D.Kinematic,
            lab.PlayerA.GetComponent<Rigidbody2D>().bodyType, "死亡等待时冻结刚体");
        Assert.IsFalse(lab.PlayerA.GetComponentInChildren<SpriteRenderer>().enabled,
            "死亡等待时隐藏");
    }

    [UnityTest]
    public IEnumerator BothDeathsRespawnTogetherAtFixedSlots()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);
        yield return MoveBothOutside(checkpointPosition);

        // 复活位置要在**复活那一刻**读：#98 —— 这个用例的场地没有地板，玩家
        // 复活后（WakeFromDeath 把 Kinematic 改回 Dynamic）会立刻自由落体，
        // 重力换算下来约 50 格/秒²，等两个物理步就已经掉了 0.3 格。
        // 之前的断言在几帧之后才读位置，量到的其实是"掉到哪了"，不是"落在哪"。
        // Kill() 到复活是同步走完的（RespawnRecordedPlayers 当场调用 ReviveAt），
        // 所以 Kill() 一返回就读，拿到的就是落点。
        int resetCount = 0;
        lab.MapReset += () => resetCount++;

        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(0, resetCount, "只有一个死亡时不触发重生");
        Assert.IsFalse(lab.PlayerA.IsAlive);

        lab.PlayerB.Kill();
        Vector2 slotA = lab.PlayerA.transform.position;
        Vector2 slotB = lab.PlayerB.transform.position;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(lab.PlayerA.IsAlive);
        Assert.IsTrue(lab.PlayerB.IsAlive);
        Assert.AreEqual(1, resetCount, "两人都死亡后只重置一次地图");
        Assert.AreEqual(checkpointPosition.x - 1f, slotA.x, 0.001f, "A 落在激活点左侧一格");
        Assert.AreEqual(checkpointPosition.x + 1f, slotB.x, 0.001f, "B 落在激活点右侧一格");
        Assert.AreEqual(checkpointPosition.y, slotA.y, 0.001f, "A 的落点高度就是激活点高度");
        Assert.AreEqual(checkpointPosition.y, slotB.y, 0.001f, "B 的落点高度就是激活点高度");
    }

    [UnityTest]
    public IEnumerator BothDeathsRespawnWithFormsRecordedAtActivation()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);

        lab.PlayerA.Teleport(Outside(checkpointPosition));
        lab.PlayerB.Teleport(Outside(checkpointPosition) + Vector2.right * 3f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // 激活瞬间 A：上/下灵体 + 左热二；B 普通。
        lab.PlayerA.SetEdge(LabPlayer.Edge.Top, LabPlayer.EdgeState.Spirit);
        lab.PlayerA.SetEdge(LabPlayer.Edge.Bottom, LabPlayer.EdgeState.Spirit);
        lab.PlayerA.SetEdge(LabPlayer.Edge.Left, LabPlayer.EdgeState.Hot2);
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // 激活后把形态改掉：两人都死时应该用激活时的快照，而不是当前形态。
        lab.PlayerA.SetEdge(LabPlayer.Edge.Top, LabPlayer.EdgeState.Normal);
        lab.PlayerA.SetEdge(LabPlayer.Edge.Bottom, LabPlayer.EdgeState.Normal);
        lab.PlayerA.SetEdge(LabPlayer.Edge.Left, LabPlayer.EdgeState.Normal);
        lab.PlayerA.SetEdge(LabPlayer.Edge.Right, LabPlayer.EdgeState.Spirit);
        yield return new WaitForFixedUpdate();

        yield return MoveBothOutside(checkpointPosition);
        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        lab.PlayerB.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(lab.PlayerA.IsAlive);
        Assert.IsTrue(lab.PlayerB.IsAlive);
        Assert.AreEqual(LabPlayer.EdgeState.Spirit, lab.PlayerA.GetEdge(LabPlayer.Edge.Top),
            "A 应恢复激活时记录的上边");
        Assert.AreEqual(LabPlayer.EdgeState.Spirit, lab.PlayerA.GetEdge(LabPlayer.Edge.Bottom),
            "A 应恢复激活时记录的下边");
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, lab.PlayerA.GetEdge(LabPlayer.Edge.Left),
            "A 应恢复激活时记录的左边");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerA.GetEdge(LabPlayer.Edge.Right),
            "A 的右边在激活时是普通边");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerB.GetEdge(LabPlayer.Edge.Top),
            "B 记录的是全新状态");
    }

    [UnityTest]
    public IEnumerator OneDeadActivationRevivesFreshAndRefreshesRecord()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);

        // 第一次两人存活激活：记录 A 下边灵体。
        lab.PlayerA.Teleport(Outside(checkpointPosition));
        lab.PlayerB.Teleport(Outside(checkpointPosition) + Vector2.right * 3f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        lab.PlayerA.SetEdge(LabPlayer.Edge.Bottom, LabPlayer.EdgeState.Spirit);
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return MoveBothOutside(checkpointPosition);

        // B 死；A 换一套形态后进点，记录应刷新成 A 当前形态 + B 全新状态。
        lab.PlayerB.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        lab.PlayerA.SetEdge(LabPlayer.Edge.Bottom, LabPlayer.EdgeState.Normal);
        lab.PlayerA.SetEdge(LabPlayer.Edge.Top, LabPlayer.EdgeState.Spirit);
        lab.PlayerA.SetEdge(LabPlayer.Edge.Left, LabPlayer.EdgeState.Hot2);
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(lab.PlayerB.IsAlive, "存活者进点应复活队友");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerB.GetEdge(LabPlayer.Edge.Top),
            "死的人应以全新状态复活");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerB.GetEdge(LabPlayer.Edge.Bottom));
        Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerB.GetEdge(LabPlayer.Edge.Left));
        Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerB.GetEdge(LabPlayer.Edge.Right));
        Assert.AreEqual(LabPlayer.EdgeState.Spirit, lab.PlayerA.GetEdge(LabPlayer.Edge.Top),
            "活着进点应保留当前形态");
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, lab.PlayerA.GetEdge(LabPlayer.Edge.Left));

        // 离开后两人都死：A 应拿到刷新后的记录，而不是第一次的下边灵体。
        yield return MoveBothOutside(checkpointPosition);
        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        lab.PlayerB.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(lab.PlayerA.IsAlive);
        Assert.AreEqual(LabPlayer.EdgeState.Spirit, lab.PlayerA.GetEdge(LabPlayer.Edge.Top),
            "记录应在第二次激活时刷新");
        Assert.AreEqual(LabPlayer.EdgeState.Hot2, lab.PlayerA.GetEdge(LabPlayer.Edge.Left));
        Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerA.GetEdge(LabPlayer.Edge.Bottom),
            "第一次记录的下边灵体不应被复活出来");
        Assert.AreEqual(LabPlayer.EdgeState.Normal, lab.PlayerB.GetEdge(LabPlayer.Edge.Top));
    }

    [UnityTest]
    public IEnumerator SurvivingPlayerEnteringCheckpointRevivesDeadTeammate()
    {
        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;

        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);
        yield return MoveBothOutside(checkpointPosition);

        lab.PlayerB.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsFalse(lab.PlayerB.IsAlive);

        int resetCount = 0;
        lab.MapReset += () => resetCount++;
        MoveBody(lab.PlayerA, checkpointPosition);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(lab.PlayerB.IsAlive, "存活者进入重生点应复活队友");
        Assert.AreEqual(1, resetCount);
        Assert.AreEqual(checkpointPosition.x - 1f, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(checkpointPosition.x + 1f, lab.PlayerB.transform.position.x, 0.001f);
    }

    [UnityTest]
    public IEnumerator BothDeathsWithoutCheckpointRespawnAtSpawn()
    {
        lab.deathWhenNoCheckpoint = TuningLab.DeathFallback.RespawnAtSpawn;
        var spawnA = (Vector2)lab.PlayerA.transform.position;
        var spawnB = (Vector2)lab.PlayerB.transform.position;

        int resetCount = 0;
        lab.MapReset += () => resetCount++;

        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(0, resetCount, "单人死亡不重开本关");
        Assert.IsFalse(lab.PlayerA.IsAlive);
        Assert.IsTrue(lab.PlayerB.IsAlive);

        lab.PlayerB.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(1, resetCount, "两人都死亡后只重开一次");
        Assert.IsTrue(lab.PlayerA.IsAlive);
        Assert.IsTrue(lab.PlayerB.IsAlive);
        Assert.AreEqual(spawnA.x, lab.PlayerA.transform.position.x, 0.001f);
        Assert.AreEqual(spawnA.y, lab.PlayerA.transform.position.y, 0.001f);
        Assert.AreEqual(spawnB.x, lab.PlayerB.transform.position.x, 0.001f);
        Assert.AreEqual(spawnB.y, lab.PlayerB.transform.position.y, 0.001f);
    }

    [UnityTest]
    public IEnumerator BothDeathsWithoutCheckpointDestroyBothPlayers()
    {
        lab.deathWhenNoCheckpoint = TuningLab.DeathFallback.DestroyPlayer;

        lab.PlayerA.Kill();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsTrue(lab.PlayerB.IsAlive, "单人死亡只隐藏等待");

        lab.PlayerB.Kill();
        yield return null;
        yield return null;

        Assert.IsTrue(lab.PlayerA == null, "两人都死亡后按 DestroyPlayer 销毁");
        Assert.IsTrue(lab.PlayerB == null);
    }

    [UnityTest]
    public IEnumerator EnteringCheckpointRestoresPickedLevelKey()
    {
        var levelGo = new GameObject("Level");
        levelGo.transform.SetParent(labGo.transform, false);
        var level = levelGo.AddComponent<LabLevel>();
        var data = new LabLevelData();
        data.keys.Add(new LabKeySpec(new Vector2(3f, 3f)));
        level.CopyFrom(data);
        level.BuildRuntime(levelGo.transform, null);

        var key = level.GetComponentInChildren<LabKey>();
        Assert.IsNotNull(key);
        Assert.IsTrue(key.TryCollect(lab.PlayerA));
        yield return null;
        Assert.IsNull(level.GetComponentInChildren<LabKey>(), "拾取后世界里的钥匙应消失");

        var checkpointPosition = new Vector2(0f, 5f);
        MakeCheckpoint(checkpointPosition);
        yield return null;
        MakeKinematic(lab.PlayerA, lab.PlayerB);
        yield return Activate(checkpointPosition);

        Assert.IsNotNull(level.GetComponentInChildren<LabKey>(), "触发重生点后钥匙应刷新");
        Assert.AreEqual(0, lab.PlayerA.KeyCount);
    }

    /// <summary>把 A 放在区域外，等重生点采样一次，再从外面走进中心触发。</summary>
    IEnumerator Activate(Vector2 center)
    {
        lab.PlayerA.Teleport(Outside(center));
        lab.PlayerB.Teleport(Outside(center) + Vector2.right * 3f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        MoveBody(lab.PlayerA, center);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
    }

    IEnumerator MoveBothOutside(Vector2 center)
    {
        MoveBody(lab.PlayerA, Outside(center));
        MoveBody(lab.PlayerB, Outside(center) + Vector2.right * 3f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
    }

    static void MakeKinematic(LabPlayer a, LabPlayer b)
    {
        a.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        b.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
    }

    /// <summary>直接挪刚体，不走 Teleport —— 这样才算一次真正的“走进去”。</summary>
    static void MoveBody(LabPlayer player, Vector2 position)
    {
        var body = player.GetComponent<Rigidbody2D>();
        body.position = position;
        player.transform.position = position;
        Physics2D.SyncTransforms();
    }

    /// <summary>重生点外、离 3×3 边缘还有半格的位置。</summary>
    static Vector2 Outside(Vector2 center) => center + Vector2.down * 2.2f;

    LabRespawnPoint MakeCheckpoint(Vector2 position)
    {
        var go = new GameObject("Checkpoint");
        go.transform.SetParent(labGo.transform, false);
        go.transform.position = position;
        var point = go.AddComponent<LabRespawnPoint>();
        point.ApplyShape();
        return point;
    }

    LabKey MakeKey()
    {
        var go = new GameObject("Key");
        go.transform.SetParent(labGo.transform, false);
        return go.AddComponent<LabKey>();
    }
}
