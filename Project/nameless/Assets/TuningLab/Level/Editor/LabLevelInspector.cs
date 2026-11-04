#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// LabLevel 的 Inspector 只显示摘要和入口按钮。格子列表、绳列表由关卡编辑器维护，
/// 避免有人在 Inspector 里手改出一份和预览对不上的数据。
/// </summary>
[CustomEditor(typeof(LabLevel))]
public sealed class LabLevelInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var level = (LabLevel)target;
        var data = level.Data;

        EditorGUILayout.HelpBox(
            "关卡数据由 GameJam → Tuning Lab Level Editor 维护。\n" +
            "这里的场地、绳、钥匙、门、出生点都会在 Play 时按 Tuning Lab 的同一套脚本生成。",
            MessageType.Info);

        EditorGUILayout.LabelField("实心格", level.SolidCellCount.ToString());
        EditorGUILayout.LabelField("合并后碰撞矩形", level.MergedRectCount.ToString());
        EditorGUILayout.LabelField("竖绳", level.RopeCount.ToString());
        EditorGUILayout.LabelField("钥匙 / 门", level.KeyCount + " / " + level.DoorCount);
        EditorGUILayout.LabelField("地刺", level.SpikeCount + "（16×6，碰到即死）");
        EditorGUILayout.LabelField("冰块", level.IceCount + "（1×1，可编组）");
        EditorGUILayout.LabelField("重生点", level.RespawnPointCount + "（3×3，接触触发）");
        EditorGUILayout.LabelField("相机范围", data.bounds.ToString());
        EditorGUILayout.LabelField("相机跟随 size",
            data.cameraMinSize.ToString("0.##") + " – " + data.cameraMaxSize.ToString("0.##") + " 格");
        EditorGUILayout.LabelField("A 落点", data.spawnA.ToString());
        EditorGUILayout.LabelField("B 落点", data.spawnB.ToString());

        EditorGUILayout.Space();
        if (GUILayout.Button("打开 Tuning Lab 关卡编辑器"))
            LabLevelEditorWindow.Open();

        if (GUILayout.Button("重建场景预览"))
        {
            if (Application.isPlaying)
            {
                Debug.Log("[LabLevel] Play 中由 TuningLab 生成场地，不需要重建预览。");
                return;
            }
            level.RebuildPreview();
            SceneView.RepaintAll();
        }
    }
}
#endif
