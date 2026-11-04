using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一格冰块：1×1 的蓝色实体方块，可以站立和滑行。
///
/// 破坏有两条路，都由玩家侧结算：
///   组内任意一块沿接触法线被 v2 撞击 → 整组一起碎（优先于红热）；
///   红热边（Hot1 / Hot2）的非承重接触 → 只融这一块，边回普通；
///   冰格落在玩家脚下 footprint 内的接触不融，避免融掉脚下承重块（含不同分组交界）。
///
/// 破坏时不销毁对象，只关碰撞和渲染，这样静态组表、地图重置和调试读数都还能找到它。
/// 编辑器预览不登记到 <see cref="All"/>，避免预览物件残留。
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public sealed class LabIce : MonoBehaviour
{
    public static readonly Color DefaultColor = new Color(0.42f, 0.75f, 1f);

    /// <summary>Play 时生成的所有冰块，按 groupId 编组查询。</summary>
    public static readonly List<LabIce> All = new List<LabIce>();

    public Vector2Int cell;
    public int groupId;
    public Color color = DefaultColor;

    public bool Broken { get; private set; }

    BoxCollider2D body;
    SpriteRenderer spriteRenderer;

    void Awake() => ApplyShape();

    void OnEnable()
    {
        if (Application.isPlaying && !All.Contains(this)) All.Add(this);
    }

    void OnDisable() => All.Remove(this);

    /// <summary>
    /// 把格子和组号应用到碰撞箱和视觉。编辑器预览也直接调它，不依赖 Awake。
    /// </summary>
    public void ApplyShape()
    {
        if (body == null) body = GetComponent<BoxCollider2D>();
        if (body != null)
        {
            body.isTrigger = false;
            body.size = Vector2.one;
            body.offset = Vector2.zero;
            body.enabled = !Broken;
        }

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = TuningLab.WhiteSprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = 1;
            spriteRenderer.enabled = !Broken;
        }
    }

    /// <summary>只碎这一块；所在组的其它冰块不动。</summary>
    public void BreakOne()
    {
        if (Broken) return;
        Broken = true;
        if (body != null) body.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    /// <summary>同一 groupId 的所有冰块一起碎。0 也是普通组。</summary>
    public void BreakGroup()
    {
        var members = GroupOf(groupId);
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null) members[i].BreakOne();
    }

    /// <summary>地图重置 / 复活时恢复：碰撞和渲染都回来。</summary>
    public void ResetIce()
    {
        Broken = false;
        ApplyShape();
    }

    /// <summary>当前 Play 里同一组的冰块；返回新列表，避免遍历时被回调修改。</summary>
    public static List<LabIce> GroupOf(int groupId)
    {
        var list = new List<LabIce>();
        for (int i = 0; i < All.Count; i++)
        {
            var ice = All[i];
            if (ice != null && ice.groupId == groupId) list.Add(ice);
        }
        return list;
    }
}
