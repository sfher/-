using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class PrototypeSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Prototype.unity";
    const string SpritePath = "Assets/Art/Greybox/White.png";
    const string MaterialPath = "Assets/Settings/PlayerFrictionless.physicsMaterial2D";

    const int GroundLayerIndex = 6;
    const int PlayerLayerIndex = 7;

    [MenuItem("GameJam/Build Prototype Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureLayer(GroundLayerIndex, "Ground");
        EnsureLayer(PlayerLayerIndex, "Player");

        Sprite white = EnsureWhiteSprite();
        PhysicsMaterial2D slick = EnsureFrictionlessMaterial();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        camGo.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData();
        var follow = camGo.AddComponent<CoopCamera>();

        var floor = new GameObject("Floor");
        floor.layer = GroundLayerIndex;
        floor.transform.position = new Vector3(0f, -3f, 0f);
        floor.transform.localScale = new Vector3(9f, 0.6f, 1f);
        var floorSr = floor.AddComponent<SpriteRenderer>();
        floorSr.sprite = white;
        floorSr.color = new Color(0.24f, 0.26f, 0.32f);
        floor.AddComponent<BoxCollider2D>();

        var p1 = CreatePlayer("Player1", new Vector2(-2f, -2f), new Color(0.36f, 0.62f, 1f),
                              PlayerMotor.Scheme.WASD, white, slick);
        var p2 = CreatePlayer("Player2", new Vector2(2f, -2f), new Color(1f, 0.45f, 0.4f),
                              PlayerMotor.Scheme.Arrows, white, slick);

        int groundMask = 1 << GroundLayerIndex;
        foreach (var player in new[] { p1, p2 })
        {
            var so = new SerializedObject(player.GetComponent<PlayerMotor>());
            so.FindProperty("groundMask").intValue = groundMask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var camSo = new SerializedObject(follow);
        camSo.FindProperty("a").objectReferenceValue = p1.transform;
        camSo.FindProperty("b").objectReferenceValue = p2.transform;
        camSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.Refresh();

        Debug.Log($"[Prototype] 场景已生成：{ScenePath}。按 Play 开始；P1 = A/D + Space，P2 = ←/→ + 右Shift。");
    }

    static GameObject CreatePlayer(string name, Vector2 position, Color color, PlayerMotor.Scheme scheme,
                                   Sprite sprite, PhysicsMaterial2D material)
    {
        var go = new GameObject(name);
        go.layer = PlayerLayerIndex;
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 10;

        var box = go.AddComponent<BoxCollider2D>();
        box.sharedMaterial = material;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3.5f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var motor = go.AddComponent<PlayerMotor>();
        var so = new SerializedObject(motor);
        so.FindProperty("scheme").enumValueIndex = (int)scheme;
        so.ApplyModifiedPropertiesWithoutUndo();

        return go;
    }

    static Sprite EnsureWhiteSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (existing != null) return existing;

        Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 4f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    static PhysicsMaterial2D EnsureFrictionlessMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
        if (existing != null) return existing;

        var material = new PhysicsMaterial2D("PlayerFrictionless")
        {
            friction = 0f,
            bounciness = 0f
        };
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    static void EnsureLayer(int index, string layerName)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets.Length == 0) return;

        var so = new SerializedObject(assets[0]);
        var layers = so.FindProperty("layers");
        var entry = layers.GetArrayElementAtIndex(index);
        if (entry.stringValue == layerName) return;

        entry.stringValue = layerName;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
