using UnityEngine;

/// <summary>
/// 把 <see cref="LabLevelData"/> 搭成真实场景物体：实心格按矩形合并后生成 BoxCollider2D，
/// 绳子生成 LabVerticalRope。
///
/// 编辑器里由关卡编辑器调用 <see cref="RebuildPreview"/>，物体只用于预览、不存盘；
/// Play 时由 TuningLab.BuildRoom 调用 <see cref="BuildRuntime"/>，走的是同一条生成路径。
/// 所以"编辑器里看到什么"和"Play 时撞到什么"是同一份几何。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LabLevel : MonoBehaviour
{
    public const string GeneratedRootName = "LabLevel_Generated";

    [SerializeField, HideInInspector] LabLevelData data = new LabLevelData();

    public LabLevelData Data
    {
        get
        {
            if (data == null) data = new LabLevelData();
            return data;
        }
    }

    public Rect CameraBounds =>
        new Rect(Data.bounds.x, Data.bounds.y, Data.bounds.width, Data.bounds.height);

    public int SolidCellCount => Data.solids != null ? Data.solids.Count : 0;
    public int MergedRectCount => Data.MergedRects().Count;
    public int RopeCount => Data.ropes != null ? Data.ropes.Count : 0;
    public int KeyCount => Data.keys != null ? Data.keys.Count : 0;
    public int DoorCount => Data.doors != null ? Data.doors.Count : 0;
    public int SpikeCount => Data.spikes != null ? Data.spikes.Count : 0;
    public int IceCount => Data.ices != null ? Data.ices.Count : 0;
    public int RespawnPointCount => Data.respawnPoints != null ? Data.respawnPoints.Count : 0;

    Transform generatedRoot;
    // 运行时生成的钥匙/门/冰；地图重置时要关回去、把捡走的钥匙长回来、把碎掉的冰恢复。
    readonly System.Collections.Generic.List<LabKey> runtimeKeys = new System.Collections.Generic.List<LabKey>();
    readonly System.Collections.Generic.List<LabDoor> runtimeDoors = new System.Collections.Generic.List<LabDoor>();
    readonly System.Collections.Generic.List<LabIce> runtimeIces = new System.Collections.Generic.List<LabIce>();
    TuningLab resetLab;

    void OnEnable()
    {
        if (!Application.isPlaying) RebuildPreview();
    }

    void OnDisable()
    {
        if (!Application.isPlaying) ClearGenerated();
    }

    void OnDestroy()
    {
        if (!Application.isPlaying) ClearGenerated();
    }

    /// <summary>用 TuningLab 的内置房间覆盖当前关卡。编辑器的"从 Tuning Lab 重置"走这里。</summary>
    public void ApplyBaseline()
    {
        data = TuningLab.CaptureBaseline();
        if (!Application.isPlaying) RebuildPreview();
    }

    public void CopyFrom(LabLevelData source)
    {
        Data.CopyFrom(source);
        if (!Application.isPlaying) RebuildPreview();
    }

    /// <summary>编辑器预览。只在非 Play 下跑，生成的物体带 DontSaveInEditor，不会被存进场景。</summary>
    public void RebuildPreview()
    {
        if (Application.isPlaying) return;
        ClearGenerated();
        Build(transform, null, true);
    }

    /// <summary>Play 时由 TuningLab 调用。material 是 TuningLab 那块摩擦 0 的 LabSlick。</summary>
    public void BuildRuntime(Transform parent, PhysicsMaterial2D material)
    {
        ClearGenerated();
        Build(parent, material, false);

        var lab = parent != null ? parent.GetComponentInParent<TuningLab>() : null;
        if (lab == null) return;
        resetLab = lab;
        lab.MapReset += ResetKeyDoors;
        lab.MapReset += ResetIces;
    }

    /// <summary>地图重置（复活 / 传送）时门关回去，被捡走的钥匙重新长出来。</summary>
    void ResetKeyDoors()
    {
        for (int i = 0; i < runtimeDoors.Count; i++)
            if (runtimeDoors[i] != null) runtimeDoors[i].ResetDoor();

        Transform root = generatedRoot;
        if (root == null) return;
        for (int i = 0; i < runtimeKeys.Count && i < Data.keys.Count; i++)
        {
            if (runtimeKeys[i] != null) continue;
            runtimeKeys[i] = CreateKey(root, Data.keys[i]);
        }
    }

    /// <summary>地图重置（复活 / 传送）时把所有被破坏的冰块恢复。</summary>
    void ResetIces()
    {
        for (int i = 0; i < runtimeIces.Count; i++)
            if (runtimeIces[i] != null) runtimeIces[i].ResetIce();
    }

    void Build(Transform parent, PhysicsMaterial2D material, bool preview)
    {
        if (data == null) data = new LabLevelData();

        var root = new GameObject(GeneratedRootName);
        root.transform.SetParent(parent, false);
        // 预览物体不存场景；Play 时的运行时物体本来就不会被存。
        if (preview) root.hideFlags = HideFlags.DontSaveInEditor;
        generatedRoot = root.transform;

        data.Normalize();
        var rects = data.MergedRects();
        for (int i = 0; i < rects.Count; i++) AddSolid(root.transform, rects[i], material);

        for (int i = 0; i < data.ropes.Count; i++) AddRope(root.transform, data.ropes[i], material);
        for (int i = 0; i < data.keys.Count; i++) AddKey(root.transform, data.keys[i], preview);
        for (int i = 0; i < data.doors.Count; i++) AddDoor(root.transform, data.doors[i], preview);
        for (int i = 0; i < data.spikes.Count; i++) AddSpike(root.transform, data.spikes[i]);
        for (int i = 0; i < data.ices.Count; i++) AddIce(root.transform, data.ices[i], material, preview);
        for (int i = 0; i < data.respawnPoints.Count; i++)
            AddRespawnPoint(root.transform, data.respawnPoints[i]);
    }

    void AddSolid(Transform parent, RectInt rect, PhysicsMaterial2D material)
    {
        var go = new GameObject("Solid");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(
            rect.x + rect.width * 0.5f,
            rect.y + rect.height * 0.5f,
            0f);
        go.transform.localScale = new Vector3(rect.width, rect.height, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = TuningLab.WhiteSprite;
        sr.color = TuningLab.SolidColor;
        sr.sortingOrder = 0;

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = Vector2.one;
        if (material != null) bc.sharedMaterial = material;
    }

    void AddRope(Transform parent, LabRopeSpec spec, PhysicsMaterial2D material)
    {
        var go = new GameObject(spec.orientation == LabRopeOrientation.Horizontal
            ? "HorizontalRope" : "VerticalRope");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.center;

        var bc = go.AddComponent<BoxCollider2D>();
        if (material != null) bc.sharedMaterial = material;

        if (spec.orientation == LabRopeOrientation.Horizontal)
        {
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 180f;
            bc.usedByEffector = true;
            var rope = go.AddComponent<LabHorizontalRope>();
            rope.cells = Mathf.Max(1, spec.cells);
            rope.ApplyShape();
        }
        else
        {
            var rope = go.AddComponent<LabVerticalRope>();
            rope.cells = Mathf.Max(1, spec.cells);
            rope.chainId = spec.chainId;
            rope.ApplyShape();
        }
    }

    /// <summary>把一格里的刺摆进场景：物件落在 1×0.375 刺带自己的中心，碰撞箱不留余量。</summary>
    void AddSpike(Transform parent, LabSpikeSpec spec)
    {
        var go = new GameObject("Spike");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.StripCenter;

        var spike = go.AddComponent<LabSpike>();
        spike.cell = spec.cell;
        spike.dir = spec.dir;
        spike.ApplyShape();
    }

    /// <summary>把一格冰块摆进场景：物件落在格中心，碰撞箱正好 1×1。</summary>
    void AddIce(Transform parent, LabIceSpec spec, PhysicsMaterial2D material, bool preview)
    {
        var go = new GameObject("Ice");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.Center;

        var ice = go.AddComponent<LabIce>();
        ice.cell = spec.cell;
        ice.groupId = spec.groupId;

        var body = go.GetComponent<BoxCollider2D>();
        if (body != null && material != null) body.sharedMaterial = material;

        ice.ApplyShape();
        if (!preview) runtimeIces.Add(ice);
    }

    static void AddRespawnPoint(Transform parent, LabRespawnPointSpec spec)
    {
        var go = new GameObject("RespawnPoint");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.position;
        go.AddComponent<LabRespawnPoint>();
    }

    void AddKey(Transform parent, LabKeySpec spec, bool preview)
    {
        var key = CreateKey(parent, spec);
        if (!preview) runtimeKeys.Add(key);
    }

    static LabKey CreateKey(Transform parent, LabKeySpec spec)
    {
        var go = new GameObject("Key");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.position;
        var key = go.AddComponent<LabKey>();
        key.ApplyShape();
        return key;
    }

    void AddDoor(Transform parent, LabDoorSpec spec, bool preview)
    {
        var go = new GameObject("Door");
        go.transform.SetParent(parent, false);
        go.transform.position = spec.position;
        var door = go.AddComponent<LabDoor>();
        door.SetClosedColor(spec.color);
        door.ApplyShape();
        if (!preview) runtimeDoors.Add(door);
    }

    public void ClearGenerated()
    {
        UnbindMapReset();
        runtimeKeys.Clear();
        runtimeDoors.Clear();
        runtimeIces.Clear();

        Transform existing = generatedRoot;
        if (existing == null && transform != null) existing = transform.Find(GeneratedRootName);
        generatedRoot = null;
        if (existing == null) return;

        bool preview = (existing.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0;
        if (Application.isPlaying && !preview) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);
    }

    void UnbindMapReset()
    {
        if (resetLab == null) return;
        resetLab.MapReset -= ResetKeyDoors;
        resetLab.MapReset -= ResetIces;
        resetLab = null;
    }

    [ContextMenu("重建关卡预览")]
    void RebuildPreviewMenu()
    {
        if (!Application.isPlaying) RebuildPreview();
    }
}
