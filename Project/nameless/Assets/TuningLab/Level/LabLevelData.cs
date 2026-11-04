using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一套关卡数据。坐标和 TuningLab 完全同一套：1 格 = 1 世界单位，
/// 格 (x, y) 占 [x, x+1) × [y, y+1)。不引入第二套网格或第二套尺度。
///
/// 数据只描述"关卡里有什么"，不负责搭场景；搭场景由 <see cref="LabLevel"/> 和
/// TuningLab 完成，两边读的是同一份数据。
/// </summary>
[System.Serializable]
public sealed class LabLevelData
{
    /// <summary>相机 clamp 范围。注意 TuningLab 基准房间的两面墙就在这个范围外 1 格，这是基准行为。</summary>
    public RectInt bounds = new RectInt(-13, -1, 26, 17);
    /// <summary>A 的落点（脚底），不是中心。</summary>
    public Vector2 spawnA = new Vector2(8f, 0f);
    /// <summary>B 的落点（脚底），不是中心。</summary>
    public Vector2 spawnB = new Vector2(10f, 0f);
    /// <summary>自由相机跟随的最小正交尺寸（格，半高）。</summary>
    public float cameraMinSize = TuningLab.DefaultCameraMinSize;
    /// <summary>自由相机跟随的最大正交尺寸（格，半高）。</summary>
    public float cameraMaxSize = TuningLab.DefaultCameraMaxSize;
    public List<Vector2Int> solids = new List<Vector2Int>();
    public List<LabRopeSpec> ropes = new List<LabRopeSpec>();
    /// <summary>钥匙（1×1，拾取后消失）。</summary>
    public List<LabKeySpec> keys = new List<LabKeySpec>();
    /// <summary>门（1×3，任意一把钥匙开任意一扇门）。</summary>
    public List<LabDoorSpec> doors = new List<LabDoorSpec>();
    /// <summary>地刺（16×6 像素，贴格边，一格一根，碰到即死）。</summary>
    public List<LabSpikeSpec> spikes = new List<LabSpikeSpec>();
    /// <summary>冰块（1×1，可编组；红热边接触融一块，v2 碰撞碎整组）。</summary>
    public List<LabIceSpec> ices = new List<LabIceSpec>();
    /// <summary>重生点（3×3 触发区，中心坐标）。</summary>
    public List<LabRespawnPointSpec> respawnPoints = new List<LabRespawnPointSpec>();
    /// <summary>连续关卡分区。每个分区的 bounds 同时是它的相机范围；B 进入哪个分区就用哪个分区。</summary>
    public List<LabLevelPartition> partitions = new List<LabLevelPartition>();

    public bool HasCell(Vector2Int cell) => solids != null && solids.Contains(cell);

    public bool AddCell(Vector2Int cell)
    {
        if (solids == null) solids = new List<Vector2Int>();
        if (solids.Contains(cell)) return false;
        solids.Add(cell);
        return true;
    }

    public bool RemoveCell(Vector2Int cell) => solids != null && solids.Remove(cell);

    public void ClearCells()
    {
        if (solids == null) solids = new List<Vector2Int>();
        else solids.Clear();
    }

    public void ClearRopes()
    {
        if (ropes == null) ropes = new List<LabRopeSpec>();
        else ropes.Clear();
    }

    public void ClearKeys()
    {
        if (keys == null) keys = new List<LabKeySpec>();
        else keys.Clear();
    }

    public void ClearDoors()
    {
        if (doors == null) doors = new List<LabDoorSpec>();
        else doors.Clear();
    }

    public void ClearSpikes()
    {
        if (spikes == null) spikes = new List<LabSpikeSpec>();
        else spikes.Clear();
    }

    public void ClearIces()
    {
        if (ices == null) ices = new List<LabIceSpec>();
        else ices.Clear();
    }

    /// <summary>这一格的刺；没有返回 null。</summary>
    public LabSpikeSpec? FindSpike(Vector2Int cell)
    {
        if (spikes == null) return null;
        for (int i = 0; i < spikes.Count; i++)
            if (spikes[i].cell == cell) return spikes[i];
        return null;
    }

    public bool HasSpike(Vector2Int cell) => FindSpike(cell).HasValue;

    /// <summary>放一根刺，或把这一格已有刺改成新方向。方向也相同则什么都不做。</summary>
    public bool PlaceSpike(Vector2Int cell, LabSpikeDir dir)
    {
        if (spikes == null) spikes = new List<LabSpikeSpec>();
        for (int i = 0; i < spikes.Count; i++)
        {
            var spike = spikes[i];
            if (spike.cell != cell) continue;
            if (spike.dir == dir) return false;
            spike.dir = dir;
            spikes[i] = spike;
            return true;
        }

        spikes.Add(new LabSpikeSpec(cell, dir));
        return true;
    }

    public bool RemoveSpike(Vector2Int cell)
    {
        if (spikes == null) return false;
        return spikes.RemoveAll(spike => spike.cell == cell) > 0;
    }

    /// <summary>这一格的冰块；没有返回 null。</summary>
    public LabIceSpec? FindIce(Vector2Int cell)
    {
        if (ices == null) return null;
        for (int i = 0; i < ices.Count; i++)
            if (ices[i].cell == cell) return ices[i];
        return null;
    }

    public bool HasIce(Vector2Int cell) => FindIce(cell).HasValue;

    /// <summary>放一块冰，或把这一格已有冰块改到新组。组号也相同则什么都不做。</summary>
    public bool PlaceIce(Vector2Int cell, int groupId)
    {
        if (ices == null) ices = new List<LabIceSpec>();
        for (int i = 0; i < ices.Count; i++)
        {
            var ice = ices[i];
            if (ice.cell != cell) continue;
            if (ice.groupId == groupId) return false;
            ice.groupId = groupId;
            ices[i] = ice;
            return true;
        }

        ices.Add(new LabIceSpec(cell, groupId));
        return true;
    }

    public bool RemoveIce(Vector2Int cell)
    {
        if (ices == null) return false;
        return ices.RemoveAll(ice => ice.cell == cell) > 0;
    }

    public bool PlaceRespawnPoint(Vector2Int cell)
    {
        if (respawnPoints == null) respawnPoints = new List<LabRespawnPointSpec>();
        var position = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        for (int i = 0; i < respawnPoints.Count; i++)
            if (respawnPoints[i].position == position) return false;
        respawnPoints.Add(new LabRespawnPointSpec(position));
        return true;
    }

    public bool RemoveRespawnPoint(Vector2Int cell)
    {
        if (respawnPoints == null) return false;
        var position = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        return respawnPoints.RemoveAll(point => point.position == position) > 0;
    }

    public bool HasRespawnPoint(Vector2Int cell)
    {
        if (respawnPoints == null) return false;
        var position = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
        for (int i = 0; i < respawnPoints.Count; i++)
            if (respawnPoints[i].position == position) return true;
        return false;
    }

    /// <summary>编辑器新建组用：当前最大组号 + 1；没有冰块时从 1 开始。0 是合法组，不作为默认。</summary>
    public int NextIceGroupId()
    {
        int max = 0;
        if (ices != null)
            for (int i = 0; i < ices.Count; i++)
                if (ices[i].groupId > max) max = ices[i].groupId;
        return max + 1;
    }

    /// <summary>同一组的所有冰块（深拷贝，按格排序），给校验和测试用。</summary>
    public List<LabIceSpec> GroupIces(int groupId)
    {
        var list = new List<LabIceSpec>();
        if (ices == null) return list;
        for (int i = 0; i < ices.Count; i++)
            if (ices[i].groupId == groupId) list.Add(ices[i]);
        list.Sort((a, b) => CompareCells(a.cell, b.cell));
        return list;
    }

    public bool ContainsCell(Vector2Int cell)
    {
        return cell.x >= bounds.x && cell.x < bounds.x + bounds.width
            && cell.y >= bounds.y && cell.y < bounds.y + bounds.height;
    }

    public bool ContainsPoint(Vector2 point)
    {
        return point.x >= bounds.x && point.x < bounds.x + bounds.width
            && point.y >= bounds.y && point.y < bounds.y + bounds.height;
    }

    /// <summary>排序 + 去重。编辑器每次改完都调一次，保证存盘顺序稳定、序列化 diff 干净。</summary>
    public void Normalize()
    {
        if (solids == null) solids = new List<Vector2Int>();
        if (ropes == null) ropes = new List<LabRopeSpec>();
        if (keys == null) keys = new List<LabKeySpec>();
        if (doors == null) doors = new List<LabDoorSpec>();
        if (spikes == null) spikes = new List<LabSpikeSpec>();
        if (ices == null) ices = new List<LabIceSpec>();
        if (respawnPoints == null) respawnPoints = new List<LabRespawnPointSpec>();
        if (partitions == null) partitions = new List<LabLevelPartition>();

        // 门色漏写/手填成透明时回落到默认色，否则门在场景里完全看不见。
        for (int i = 0; i < doors.Count; i++)
        {
            var door = doors[i];
            if (door.color.a <= 0.01f)
            {
                door.color = LabDoor.DefaultClosedColor;
                doors[i] = door;
            }
        }

        // 旧场景里没有这两个字段时读出来是 0：回落到 Tuning Lab 默认，避免相机被锁死在 0.1。
        if (cameraMinSize <= 0f) cameraMinSize = TuningLab.DefaultCameraMinSize;
        if (cameraMaxSize <= 0f) cameraMaxSize = TuningLab.DefaultCameraMaxSize;
        cameraMinSize = Mathf.Clamp(cameraMinSize, 0.1f, 128f);
        cameraMaxSize = Mathf.Clamp(cameraMaxSize, cameraMinSize, 128f);

        for (int i = partitions.Count - 1; i >= 0; i--)
        {
            var partition = partitions[i];
            if (partition == null)
            {
                partitions.RemoveAt(i);
                continue;
            }

            partition.bounds = NormalizeRect(partition.bounds);
            if (string.IsNullOrEmpty(partition.name)) partition.name = "Level " + (i + 1);
        }

        var uniqueRespawnPoints = new HashSet<Vector2>();
        for (int i = respawnPoints.Count - 1; i >= 0; i--)
        {
            if (!uniqueRespawnPoints.Add(respawnPoints[i].position))
                respawnPoints.RemoveAt(i);
        }
        respawnPoints.Sort((a, b) =>
        {
            int y = a.position.y.CompareTo(b.position.y);
            return y != 0 ? y : a.position.x.CompareTo(b.position.x);
        });

        solids.Sort(CompareCells);

        int write = 0;
        for (int read = 0; read < solids.Count; read++)
        {
            if (write > 0 && solids[read] == solids[write - 1]) continue;
            solids[write++] = solids[read];
        }
        if (write < solids.Count) solids.RemoveRange(write, solids.Count - write);

        for (int i = ropes.Count - 1; i >= 0; i--)
        {
            var rope = ropes[i];
            if (rope.cells <= 0)
            {
                ropes.RemoveAt(i);
                continue;
            }
            rope.cells = Mathf.Min(rope.cells, 256);
            ropes[i] = rope;
        }

        // 一格只留一根刺：按格排序后丢掉同格重复项。
        spikes.Sort((a, b) => CompareCells(a.cell, b.cell));
        int spikeWrite = 0;
        for (int read = 0; read < spikes.Count; read++)
        {
            if (spikeWrite > 0 && spikes[read].cell == spikes[spikeWrite - 1].cell) continue;
            spikes[spikeWrite++] = spikes[read];
        }
        if (spikeWrite < spikes.Count) spikes.RemoveRange(spikeWrite, spikes.Count - spikeWrite);

        // 一格只留一块冰：按格排序后丢掉同格重复项，组号随保留项。
        ices.Sort((a, b) => CompareCells(a.cell, b.cell));
        int iceWrite = 0;
        for (int read = 0; read < ices.Count; read++)
        {
            if (iceWrite > 0 && ices[read].cell == ices[iceWrite - 1].cell) continue;
            ices[iceWrite++] = ices[read];
        }
        if (iceWrite < ices.Count) ices.RemoveRange(iceWrite, ices.Count - iceWrite);
    }

    public void CopyFrom(LabLevelData other)
    {
        if (other == null)
        {
            bounds = new RectInt(-13, -1, 26, 17);
            spawnA = new Vector2(8f, 0f);
            spawnB = new Vector2(10f, 0f);
            cameraMinSize = TuningLab.DefaultCameraMinSize;
            cameraMaxSize = TuningLab.DefaultCameraMaxSize;
            solids = new List<Vector2Int>();
            ropes = new List<LabRopeSpec>();
            keys = new List<LabKeySpec>();
            doors = new List<LabDoorSpec>();
            spikes = new List<LabSpikeSpec>();
            ices = new List<LabIceSpec>();
            respawnPoints = new List<LabRespawnPointSpec>();
            partitions = new List<LabLevelPartition>();
            return;
        }

        bounds = other.bounds;
        spawnA = other.spawnA;
        spawnB = other.spawnB;
        cameraMinSize = other.cameraMinSize;
        cameraMaxSize = other.cameraMaxSize;
        solids = new List<Vector2Int>(other.solids ?? new List<Vector2Int>());
        ropes = new List<LabRopeSpec>(other.ropes ?? new List<LabRopeSpec>());
        keys = new List<LabKeySpec>(other.keys ?? new List<LabKeySpec>());
        doors = new List<LabDoorSpec>(other.doors ?? new List<LabDoorSpec>());
        spikes = new List<LabSpikeSpec>(other.spikes ?? new List<LabSpikeSpec>());
        ices = new List<LabIceSpec>(other.ices ?? new List<LabIceSpec>());
        respawnPoints = new List<LabRespawnPointSpec>(
            other.respawnPoints ?? new List<LabRespawnPointSpec>());
        partitions = new List<LabLevelPartition>();
        if (other.partitions != null)
        {
            foreach (var partition in other.partitions)
            {
                if (partition == null) continue;
                partitions.Add(new LabLevelPartition
                {
                    name = partition.name,
                    bounds = partition.bounds
                });
            }
        }
        Normalize();
    }

    public LabLevelData Clone()
    {
        var copy = new LabLevelData();
        copy.CopyFrom(this);
        return copy;
    }

    public static Rect ToRect(RectInt rect)
        => new Rect(rect.x, rect.y, rect.width, rect.height);

    public static RectInt NormalizeRect(RectInt rect)
    {
        if (rect.width < 0)
        {
            rect.x += rect.width;
            rect.width = -rect.width;
        }
        if (rect.height < 0)
        {
            rect.y += rect.height;
            rect.height = -rect.height;
        }
        if (rect.width <= 0) rect.width = 1;
        if (rect.height <= 0) rect.height = 1;
        return rect;
    }

    public static bool Overlaps(RectInt a, RectInt b)
    {
        return a.x < b.x + b.width && a.x + a.width > b.x
            && a.y < b.y + b.height && a.y + a.height > b.y;
    }

    /// <summary>这个 bounds 是否和任何已有分区重叠；ignoreIndex 用于移动/改大小自己。</summary>
    public bool OverlapsAny(RectInt bounds, int ignoreIndex = -1)
    {
        if (partitions == null) return false;
        bounds = NormalizeRect(bounds);

        for (int i = 0; i < partitions.Count; i++)
        {
            if (i == ignoreIndex) continue;
            var partition = partitions[i];
            if (partition != null && Overlaps(bounds, partition.bounds)) return true;
        }
        return false;
    }

    /// <summary>B 的判定：点在哪个分区的 bounds 里；没有返回 -1。</summary>
    public int FindPartition(Vector2 point)
    {
        if (partitions == null) return -1;
        for (int i = 0; i < partitions.Count; i++)
        {
            var partition = partitions[i];
            if (partition != null && ToRect(partition.bounds).Contains(point)) return i;
        }
        return -1;
    }

    public LabLevelPartition AddPartition(RectInt bounds, string name = null)
    {
        if (partitions == null) partitions = new List<LabLevelPartition>();
        bounds = NormalizeRect(bounds);
        if (OverlapsAny(bounds)) return null;

        var partition = new LabLevelPartition
        {
            name = string.IsNullOrEmpty(name) ? "Level " + (partitions.Count + 1) : name,
            bounds = bounds
        };
        partitions.Add(partition);
        return partition;
    }

    /// <summary>只平移 bounds 和里面的物件，不做重叠检查。调用方必须先验证目标位置。</summary>
    public void TranslatePartition(int index, Vector2Int delta, bool moveContents)
    {
        if (partitions == null || index < 0 || index >= partitions.Count) return;
        var partition = partitions[index];
        if (partition == null || delta == Vector2Int.zero) return;

        Rect oldRect = ToRect(partition.bounds);
        partition.bounds = new RectInt(
            partition.bounds.x + delta.x,
            partition.bounds.y + delta.y,
            partition.bounds.width,
            partition.bounds.height);

        if (!moveContents) return;

        var shift = new Vector2(delta.x, delta.y);

        if (solids != null)
        {
            for (int i = 0; i < solids.Count; i++)
            {
                var cell = solids[i];
                if (oldRect.Contains(new Vector2(cell.x + 0.5f, cell.y + 0.5f)))
                    solids[i] = cell + delta;
            }
        }

        if (ropes != null)
        {
            for (int i = 0; i < ropes.Count; i++)
            {
                var rope = ropes[i];
                if (!oldRect.Contains(rope.center)) continue;
                rope.center += shift;
                ropes[i] = rope;
            }
        }

        if (keys != null)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (!oldRect.Contains(key.position)) continue;
                key.position += shift;
                keys[i] = key;
            }
        }

        if (doors != null)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                if (!oldRect.Contains(door.position)) continue;
                door.position += shift;
                doors[i] = door;
            }
        }

        if (spikes != null)
        {
            for (int i = 0; i < spikes.Count; i++)
            {
                var spike = spikes[i];
                if (!oldRect.Contains(new Vector2(spike.cell.x + 0.5f, spike.cell.y + 0.5f))) continue;
                spike.cell += delta;
                spikes[i] = spike;
            }
        }

        if (ices != null)
        {
            for (int i = 0; i < ices.Count; i++)
            {
                var ice = ices[i];
                if (!oldRect.Contains(new Vector2(ice.cell.x + 0.5f, ice.cell.y + 0.5f))) continue;
                ice.cell += delta;
                ices[i] = ice;
            }

            if (respawnPoints != null)
            {
                for (int i = 0; i < respawnPoints.Count; i++)
                {
                    var point = respawnPoints[i];
                    if (!oldRect.Contains(point.position)) continue;
                    point.position += shift;
                    respawnPoints[i] = point;
                }
            }
        }

        if (oldRect.Contains(spawnA)) spawnA += shift;
        if (oldRect.Contains(spawnB)) spawnB += shift;
    }

    /// <summary>移动分区。目标位置和其它分区重叠时直接拒绝，不会移动里面的物件。</summary>
    public bool MovePartition(int index, Vector2Int delta, bool moveContents)
    {
        if (partitions == null || index < 0 || index >= partitions.Count) return false;
        var partition = partitions[index];
        if (partition == null || delta == Vector2Int.zero) return false;

        var next = new RectInt(
            partition.bounds.x + delta.x,
            partition.bounds.y + delta.y,
            partition.bounds.width,
            partition.bounds.height);
        if (OverlapsAny(next, index)) return false;

        TranslatePartition(index, delta, moveContents);
        return true;
    }

    /// <summary>改分区大小。和其它分区重叠时直接拒绝。</summary>
    public bool ResizePartition(int index, RectInt bounds)
    {
        if (partitions == null || index < 0 || index >= partitions.Count) return false;
        var partition = partitions[index];
        if (partition == null) return false;

        bounds = NormalizeRect(bounds);
        if (OverlapsAny(bounds, index)) return false;

        partition.bounds = bounds;
        return true;
    }

    /// <summary>删除分区。deleteContents = true 时清掉分区里的地形和绳；出生点保留。</summary>
    public bool DeletePartition(int index, bool deleteContents)
    {
        if (partitions == null || index < 0 || index >= partitions.Count) return false;

        var partition = partitions[index];
        if (partition != null && deleteContents)
        {
            Rect rect = ToRect(partition.bounds);
            if (solids != null)
                solids.RemoveAll(cell => rect.Contains(new Vector2(cell.x + 0.5f, cell.y + 0.5f)));
            if (ropes != null)
                ropes.RemoveAll(rope => rect.Contains(rope.center));
            if (keys != null)
                keys.RemoveAll(key => rect.Contains(key.position));
            if (doors != null)
                doors.RemoveAll(door => rect.Contains(door.position));
            if (spikes != null)
                spikes.RemoveAll(spike => rect.Contains(new Vector2(spike.cell.x + 0.5f, spike.cell.y + 0.5f)));
            if (ices != null)
                ices.RemoveAll(ice => rect.Contains(new Vector2(ice.cell.x + 0.5f, ice.cell.y + 0.5f)));
            if (respawnPoints != null)
                respawnPoints.RemoveAll(point => rect.Contains(point.position));
        }

        partitions.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// 把实心格合并成尽量少的矩形，降低 Collider 数量，也保证运行时生成出来的
    /// 就是 TuningLab 那种"一整块平台一个 BoxCollider2D"。
    /// 贪心：从最下、最左的格开始，先向右扩，再整行向上扩。
    /// </summary>
    public List<RectInt> MergedRects()
    {
        var rects = new List<RectInt>();
        var set = new HashSet<Vector2Int>();
        if (solids != null)
            for (int i = 0; i < solids.Count; i++) set.Add(solids[i]);

        while (set.Count > 0)
        {
            Vector2Int start = new Vector2Int(int.MaxValue, int.MaxValue);
            foreach (var cell in set)
            {
                if (cell.y < start.y || (cell.y == start.y && cell.x < start.x))
                    start = cell;
            }

            int width = 1;
            while (set.Contains(new Vector2Int(start.x + width, start.y))) width++;

            int height = 1;
            while (true)
            {
                bool fullRow = true;
                int row = start.y + height;
                for (int x = start.x; x < start.x + width; x++)
                {
                    if (set.Contains(new Vector2Int(x, row))) continue;
                    fullRow = false;
                    break;
                }
                if (!fullRow) break;
                height++;
            }

            rects.Add(new RectInt(start.x, start.y, width, height));
            for (int y = start.y; y < start.y + height; y++)
                for (int x = start.x; x < start.x + width; x++)
                    set.Remove(new Vector2Int(x, y));
        }

        return rects;
    }

    /// <summary>
    /// 把 TuningLab.BuildRoom 里的"中心 + 尺寸"矩形写成格。
    /// 必须完整对齐 1 格，否则返回 false（4 倍场地就是故意不对齐的，不能拿来当关卡基准）。
    /// </summary>
    public bool AddAlignedRect(Vector2 center, Vector2 size, float tolerance = 0.001f)
    {
        float xMin = center.x - size.x * 0.5f;
        float xMax = center.x + size.x * 0.5f;
        float yMin = center.y - size.y * 0.5f;
        float yMax = center.y + size.y * 0.5f;

        int x0 = Mathf.RoundToInt(xMin);
        int x1 = Mathf.RoundToInt(xMax);
        int y0 = Mathf.RoundToInt(yMin);
        int y1 = Mathf.RoundToInt(yMax);

        if (x1 <= x0 || y1 <= y0) return false;
        if (Mathf.Abs(xMin - x0) > tolerance || Mathf.Abs(xMax - x1) > tolerance) return false;
        if (Mathf.Abs(yMin - y0) > tolerance || Mathf.Abs(yMax - y1) > tolerance) return false;

        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                AddCell(new Vector2Int(x, y));
        return true;
    }

    /// <summary>地形和绳子的格子范围；没有内容时退回相机范围。给"跳到关卡"用。</summary>
    public RectInt ContentBounds()
    {
        bool any = false;
        int x0 = 0, y0 = 0, x1 = 0, y1 = 0;

        if (solids != null)
        {
            for (int i = 0; i < solids.Count; i++)
            {
                var cell = solids[i];
                int cx0 = cell.x, cx1 = cell.x + 1;
                int cy0 = cell.y, cy1 = cell.y + 1;
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        if (ropes != null)
        {
            for (int i = 0; i < ropes.Count; i++)
            {
                var rope = ropes[i];
                float halfWidth = rope.orientation == LabRopeOrientation.Horizontal
                    ? rope.cells * 0.5f : 0.1875f;
                float halfHeight = rope.orientation == LabRopeOrientation.Horizontal
                    ? 0.125f : rope.cells * 0.5f;
                int cx0 = Mathf.FloorToInt(rope.center.x - halfWidth);
                int cx1 = Mathf.CeilToInt(rope.center.x + halfWidth);
                int cy0 = Mathf.FloorToInt(rope.center.y - halfHeight);
                int cy1 = Mathf.CeilToInt(rope.center.y + halfHeight);
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        if (ices != null)
        {
            for (int i = 0; i < ices.Count; i++)
            {
                var ice = ices[i];
                int cx0 = ice.cell.x, cx1 = ice.cell.x + 1;
                int cy0 = ice.cell.y, cy1 = ice.cell.y + 1;
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }

            }
        }

        if (respawnPoints != null)
        {
            for (int i = 0; i < respawnPoints.Count; i++)
            {
                var point = respawnPoints[i];
                int cx0 = Mathf.FloorToInt(point.position.x - 1.5f);
                int cx1 = Mathf.CeilToInt(point.position.x + 1.5f);
                int cy0 = Mathf.FloorToInt(point.position.y - 1.5f);
                int cy1 = Mathf.CeilToInt(point.position.y + 1.5f);
                if (!any) { x0 = cx0; y0 = cy0; x1 = cx1; y1 = cy1; any = true; }
                else
                {
                    x0 = Mathf.Min(x0, cx0); y0 = Mathf.Min(y0, cy0);
                    x1 = Mathf.Max(x1, cx1); y1 = Mathf.Max(y1, cy1);
                }
            }
        }

        return any ? new RectInt(x0, y0, x1 - x0, y1 - y0) : bounds;
    }

    static int CompareCells(Vector2Int a, Vector2Int b)
    {
        int y = a.y.CompareTo(b.y);
        return y != 0 ? y : a.x.CompareTo(b.x);
    }
}

[System.Serializable]
public enum LabRopeOrientation
{
    Vertical = 0,
    Horizontal = 1
}

[System.Serializable]
public struct LabRopeSpec
{
    /// <summary>绳中心（世界坐标）。宽 6 像素、每格 16 像素，所以 x 是连续值。</summary>
    public Vector2 center;
    /// <summary>绳长（格）。</summary>
    public int cells;
    public int chainId;
    public LabRopeOrientation orientation;

    public float Bottom => center.y - (orientation == LabRopeOrientation.Horizontal ? 0.125f : cells * 0.5f);
    public float Top => center.y + (orientation == LabRopeOrientation.Horizontal ? 0.125f : cells * 0.5f);

    public LabRopeSpec(Vector2 center, int cells, int chainId = 0,
        LabRopeOrientation orientation = LabRopeOrientation.Vertical)
    {
        this.center = center;
        this.cells = cells;
        this.chainId = chainId;
        this.orientation = orientation;
    }
}

/// <summary>
/// 一把钥匙。1×1，位置是中心；编辑器按格中心摆放，所以 x/y 都是格 + 0.5。
/// </summary>
[System.Serializable]
public struct LabKeySpec
{
    public Vector2 position;

    public LabKeySpec(Vector2 position)
    {
        this.position = position;
    }

    /// <summary>钥匙盖住的格子（1×1）。</summary>
    public RectInt Cells() => new RectInt(
        Mathf.FloorToInt(position.x),
        Mathf.FloorToInt(position.y),
        1, 1);
}

/// <summary>
/// 一扇门。1×3，位置是中心；任意一把钥匙都能开任意一扇门，颜色只用于区分/辨认。
/// </summary>
[System.Serializable]
public struct LabDoorSpec
{
    public const int CellHeight = 3;

    public Vector2 position;
    public Color color;

    public LabDoorSpec(Vector2 position, Color color)
    {
        this.position = position;
        this.color = color;
    }

    public static LabDoorSpec At(Vector2 position) => new LabDoorSpec(position, LabDoor.DefaultClosedColor);

    /// <summary>门盖住的格子：宽 1、高 <see cref="CellHeight"/>，按中心算。</summary>
    public RectInt Cells() => new RectInt(
        Mathf.FloorToInt(position.x),
        Mathf.FloorToInt(position.y - CellHeight * 0.5f),
        1, CellHeight);
}

/// <summary>地刺的朝向：尖刺指向这个方向，底座贴在方向反面的那条格边上。
/// 地刺（朝上、立在地板顶上那格）= <see cref="Up"/>。</summary>
public enum LabSpikeDir
{
    Up = 0,
    Down = 1,
    Left = 2,
    Right = 3
}

/// <summary>
/// 一格一根地刺。外形 16×6 像素 = 1 × 0.375 世界单位，长边紧贴格边。
/// 碰撞箱 = 视觉外接矩形，一点不让：齿之间的凹陷也算碰到。
/// </summary>
[System.Serializable]
public struct LabSpikeSpec
{
    /// <summary>刺的厚度（世界单位）= 6 美术像素 / 16。</summary>
    public const float Thickness = 6f / TuningLab.CellPixels;

    public Vector2Int cell;
    public LabSpikeDir dir;

    public LabSpikeSpec(Vector2Int cell, LabSpikeDir dir)
    {
        this.cell = cell;
        this.dir = dir;
    }

    /// <summary>尖刺指向的单位轴。</summary>
    public Vector2 Facing
    {
        get
        {
            if (dir == LabSpikeDir.Up) return Vector2.up;
            if (dir == LabSpikeDir.Down) return Vector2.down;
            if (dir == LabSpikeDir.Left) return Vector2.left;
            return Vector2.right;
        }
    }

    /// <summary>刺自己那条 1×0.375 带的中心，也是生成物件的落点。</summary>
    public Vector2 StripCenter
        => new Vector2(cell.x + 0.5f, cell.y + 0.5f) - Facing * (0.5f - Thickness * 0.5f);

    /// <summary>碰撞尺寸：横着的刺宽 1 高 0.375，竖着的反之。</summary>
    public Vector2 StripSize
        => dir == LabSpikeDir.Up || dir == LabSpikeDir.Down
            ? new Vector2(1f, Thickness)
            : new Vector2(Thickness, 1f);

    /// <summary>底座踩在哪一格——那一格必须是实心，否则刺悬空。</summary>
    public Vector2Int BaseCell
        => cell - new Vector2Int(Mathf.RoundToInt(Facing.x), Mathf.RoundToInt(Facing.y));
}

/// <summary>
/// 一块冰：1×1 格，实体碰撞，可以站立/滑行。
/// groupId 沿用绳子的整数编组模式；0 也是普通组，不是“未编组”。
/// </summary>
[System.Serializable]
public struct LabIceSpec
{
    public Vector2Int cell;
    public int groupId;

    public LabIceSpec(Vector2Int cell, int groupId)
    {
        this.cell = cell;
        this.groupId = groupId;
    }

    /// <summary>物件落点（格中心）。</summary>
    public Vector2 Center => new Vector2(cell.x + 0.5f, cell.y + 0.5f);

    /// <summary>冰块盖住的格子（1×1）。</summary>
    public RectInt Cells() => new RectInt(cell.x, cell.y, 1, 1);
}

[System.Serializable]
public struct LabRespawnPointSpec
{
    public Vector2 position;

    public LabRespawnPointSpec(Vector2 position)
    {
        this.position = position;
    }

    public RectInt Cells() => new RectInt(
        Mathf.FloorToInt(position.x - 1.5f),
        Mathf.FloorToInt(position.y - 1.5f),
        3, 3);
}

/// <summary>
/// 连续关卡分区。bounds 就是这一关的相机 clamp 范围：
/// B 进入哪个分区的 bounds，相机就用哪个分区的大小。移动分区时可以一起移动里面的物件。
/// </summary>
[System.Serializable]
public sealed class LabLevelPartition
{
    public string name = "Level";
    public RectInt bounds = new RectInt(0, 0, 16, 12);
}
