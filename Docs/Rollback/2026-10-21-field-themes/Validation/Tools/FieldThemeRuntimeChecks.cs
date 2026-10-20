using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dungeon.Core;
using Dungeon.Map;
using Dungeon.Traps;
using UnityEditor;
using UnityEngine;
using Theme = Dungeon.Map.FieldEffectManager.FieldEffectType;

public sealed class FieldThemeRuntimeChecks : MonoBehaviour
{
    [Serializable] private sealed class Expectations { public Row[] rows; }
    [Serializable] private sealed class Row { public string source, ice, fire; }
    private readonly Dictionary<Sprite, Row> expected = new();
    private readonly List<string> checks = new();
    private int failures;
    private bool finished;
    private JsonBlockBuilder2D builder;
    private FieldEffectManager effect;
    private string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../Docs/Rollback/2026-10-21-field-themes/Validation"));
    private void Check(bool ok, string text) { checks.Add((ok ? "PASS " : "FAIL ") + text); if (!ok) failures++; }
    private Sprite Load(string path) => string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
    private Sprite Expected(Sprite source, string theme)
    {
        if (source == null || !expected.TryGetValue(source, out var row)) return source;
        string path = theme == "ice" ? row.ice : theme == "fire" || theme == "burn" ? row.fire : null;
        return Load(path) ?? source;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null;
        foreach (var row in JsonUtility.FromJson<Expectations>(Resources.Load<TextAsset>("FieldThemeValidation/expectations").text).rows)
        {
            var sprite = Load(row.source);
            Check(sprite != null, "original import " + row.source);
            if (sprite != null) expected.Add(sprite, row);
        }
        CheckCatalog();
        new GameObject("Registry").AddComponent<BlockRegistry>();
        builder = new GameObject("MapBuilder").AddComponent<JsonBlockBuilder2D>();
        builder.autoBuildOnStart = false;
        builder.player = new GameObject("PlayerAnchor").transform;
        builder.player.position = new Vector3(-50, -50, 0);
        effect = new GameObject("FieldEffect").AddComponent<FieldEffectManager>();
        builder.fieldEffectManager = effect;
        builder.visionSystem = new GameObject("Vision").AddComponent<ForwardZoneVisionMask2D>();
        builder.visionSystem.SetDarkAlpha(0);

        foreach (string theme in new[] { "ice", "fire", "burn", "none", "missing", "unknown", "ice" })
        {
            yield return Build(theme);
            Theme parsed = theme == "ice" ? Theme.Ice : theme == "fire" || theme == "burn" ? Theme.Burn : Theme.None;
            Check(builder.CurrentSpriteTheme == parsed && effect.CurrentType == parsed, theme + " builder/effect selection");
            CheckInstances(theme, false);
            foreach (var hidden in builder.GetComponentsInChildren<HiddenTrapVisual2D>()) hidden.ForceReveal();
            CheckInstances(theme, true);
            foreach (var hidden in builder.GetComponentsInChildren<HiddenTrapVisual2D>()) hidden.ForceHideAgain();
            CheckInstances(theme, false);
            yield return null; yield return null;
            Check(builder.transform.Cast<Transform>().Count(t => t.GetComponent<BlockCoord>() != null) == 32, theme + " rebuild removes previous tile instances");
        }
        effect.ApplyMapEffect(new FieldEffectData2D { type = " FIRE ", damage = 2 }, builder.player);
        Check(effect.CurrentType == Theme.Burn && effect.CurrentDamage == 2 && Mathf.Abs(effect.CurrentTickInterval - 1f) < 0.001f,
            "fire preserves authored damage and existing burn tick interval");
        effect.ClearFieldEffect();

        foreach (string theme in new[] { "ice", "fire" })
        {
            foreach (string fixture in new[] { "neighbour", "forceNeighbour" })
            {
                yield return Build(theme + "-" + fixture);
                BlockRegistry.Instance.TryGet("0303", out var centre);
                BlockRegistry.Instance.TryGet("0403", out var neighbour);
                var oldRotation = neighbour.transform.rotation;
                string oldData = JsonUtility.ToJson(builder.LoadedMapData.grid.Single(c => c.coord == "0403"));
                var plan = new TileRetileSolver2D(TileRetileSolver2D.LoadRules(Resources.Load<TextAsset>("Data/TileRules")),
                    new[] { "101-001", "101-004", "101-007" }).DisarmTrapAndResolve(builder.LoadedMapData, "0303");
                int count = StatusSaveFile.Instance.GetBrokenTraps();
                centre.GetComponent<Destructible>().ExecuteDestruction();
                centre.GetComponent<Destructible>().ExecuteDestruction();
                Check(StatusSaveFile.Instance.GetBrokenTraps() == count + 1, theme + fixture + " destruction counted once");
                BlockRegistry.Instance.TryGet("0303", out var floor);
                Check(floor != centre && floor.GetComponent<SpriteRenderer>().sprite == Expected(Load("Assets/Sprites/Tiles/000-000-0.png"), theme), theme + fixture + " replacement floor theme");
                foreach (var result in plan.Skip(1))
                {
                    BlockRegistry.Instance.TryGet(result.coord, out var target);
                    var raw = Resources.Load<GameObject>("Prefabs/001-001").GetComponent<AssetVariantSprite2D>().GetSpriteForAssetNo(result.newAssetNo);
                    var visual = result.wallVisualOnly ? target.transform.Find(fixture == "neighbour" ? "RetiledHiddenWall" : "WallVisual").GetComponent<SpriteRenderer>()
                        : target.GetComponent<AssetVariantSprite2D>().Renderer;
                    Check(visual.sprite == Expected(raw, theme), theme + fixture + " retiled wall " + result.coord);
                }
                Check(Quaternion.Angle(oldRotation, neighbour.transform.rotation) < 0.001f && oldData == JsonUtility.ToJson(builder.LoadedMapData.grid.Single(c => c.coord == "0403")), theme + fixture + " neighbour attack metadata unchanged");
                if (fixture == "neighbour")
                {
                    var hidden = neighbour.GetComponent<HiddenTrapVisual2D>();
                    hidden.ForceReveal();
                    Check(neighbour.transform.Find("RetiledHiddenWall").GetComponent<SpriteRenderer>().forceRenderingOff, theme + " reveal hides retiled overlay");
                    Check(neighbour.GetComponent<AssetVariantSprite2D>().Renderer.sprite == Expected(Load("Assets/Sprites/Tiles/101-001-0.png"), theme), theme + " revealed trap keeps theme");
                    hidden.ForceHideAgain();
                    Check(!neighbour.transform.Find("RetiledHiddenWall").GetComponent<SpriteRenderer>().forceRenderingOff, theme + " hide restores retiled overlay");
                }
                yield return null; yield return null;
            }
            yield return Build(theme + "-floor3");
            BlockRegistry.Instance.TryGet("0303", out var baseFloor);
            builder.transform.Find("3F_101-001_0303").GetComponent<Destructible>().ExecuteDestruction();
            yield return null; yield return null;
            var upperFloor = builder.transform.Find("3F_000-000_0303");
            Check(upperFloor != null && upperFloor.GetComponent<SpriteRenderer>().sprite == Expected(Load("Assets/Sprites/Tiles/000-000-0.png"), theme), theme + " 3F replacement theme");
            Check(BlockRegistry.Instance.TryGet("0303", out var unchanged) && unchanged == baseFloor, theme + " 3F keeps base registry");

            yield return Build(theme);
            var pit = builder.transform.Find("100-000_0004");
            // 현재 낭떠러지 프리팹에 없는 선택형 교체 컴포넌트도 별도 연결해 실제 호출한다.
            var switcher = pit.gameObject.AddComponent<Dungeon.System.ReplaceSelfWithPrefabOnDisable>();
            switcher.SetTrapEnabled(false);
            var switchFloor = builder.transform.Find("000-000_ReplacedFrom_100-000_0004");
            Check(switchFloor != null && switchFloor.GetComponent<SpriteRenderer>().sprite == Expected(Load("Assets/Sprites/Tiles/000-000-0.png"), theme), theme + " switch floor inherits theme");
            yield return null; yield return null;

            yield return Build(theme);
            builder.enableTileRetiling = false;
            var trap = builder.transform.Find("101-001_0506");
            var existing = FindObjectsByType<AssetVariantSprite2D>(FindObjectsSortMode.None).Select(v => v.GetInstanceID()).ToHashSet();
            trap.GetComponent<Destructible>().ExecuteDestruction();
            var legacyFloor = FindObjectsByType<AssetVariantSprite2D>(FindObjectsSortMode.None).Single(v => !existing.Contains(v.GetInstanceID()));
            Check(legacyFloor.Renderer.sprite == Expected(Load("Assets/Sprites/Tiles/000-000-0.png"), theme), theme + " legacy disarm floor inherits theme");
            Destroy(legacyFloor.gameObject);
            builder.enableTileRetiling = true;
            yield return null; yield return null;

            yield return Build(theme + "-map002");
            foreach (var hidden in builder.GetComponentsInChildren<HiddenTrapVisual2D>()) hidden.ForceReveal();
            Capture(theme + "-map002-before.png");
            builder.transform.Find("101-001_0501").GetComponent<Destructible>().ExecuteDestruction();
            yield return null; yield return null;
            Capture(theme + "-map002-after.png");
            Check(builder.LoadedMapData.grid.Single(c => c.coord == "0501").code == "000-000", theme + " actual map002 disarm succeeds");
        }
        Finish();
    }
    private IEnumerator Build(string name)
    {
        builder.BuildFromJsonPath("FieldThemeValidation/" + name);
        foreach (var behaviour in builder.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is ITrapToggle && !(behaviour is Dungeon.System.ReplaceSelfWithPrefabOnDisable)) behaviour.enabled = false;
        // 기존 Builder의 Destroy는 프레임 말에 완료된다. 이전 맵과 새 맵을 함께 검사하지 않는다.
        yield return null;
        yield return null;
    }
    private void CheckCatalog()
    {
        var catalog = Resources.Load<MapSpriteThemeCatalog>("Data/MapSpriteThemes");
        Check(catalog != null, "catalog is a runtime Resources dependency");
        foreach (var pair in expected)
        {
            foreach (var theme in new[] { "ice", "fire", "none" })
            {
                var chosen = catalog.Resolve(pair.Key, theme == "ice" ? Theme.Ice : theme == "fire" ? Theme.Burn : Theme.None);
                Check(chosen == Expected(pair.Key, theme), "catalog " + theme + " " + pair.Value.source);
                Check(catalog.Resolve(chosen, Theme.None) == pair.Key, "catalog restores original " + theme + " " + pair.Value.source);
                Check(chosen.pixelsPerUnit == pair.Key.pixelsPerUnit && chosen.rect.size == pair.Key.rect.size && chosen.pivot == pair.Key.pivot, "sprite geometry preserved " + theme + " " + pair.Value.source);
            }
        }
        Check(FieldEffectManager.ParseType(" FIRE ") == Theme.Burn && FieldEffectManager.ParseType("burn") == Theme.Burn
            && FieldEffectManager.ParseType(null) == Theme.None && FieldEffectManager.ParseType("nonsense") == Theme.None, "shared type parser aliases/normalization");
        var copy = Instantiate(catalog);
        var missing = Load("Assets/Sprites/Tiles/001-001-8.png");
        Check(copy.Resolve(missing, Theme.Ice) == missing, "future image starts with original fallback");
        var serialized = new SerializedObject(copy);
        var entries = serialized.FindProperty("entries");
        var future = Load("Assets/Sprites/FieldThemes/Ice/001-001-7.png");
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("original").objectReferenceValue == missing)
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("ice").objectReferenceValue = future;
        serialized.ApplyModifiedProperties();
        Check(copy.Resolve(missing, Theme.Ice) == future, "filling empty Inspector slot works without code changes");
        Check(catalog.Resolve(missing, Theme.Ice) == missing, "future slot check leaves shipped catalog unchanged");
        Destroy(copy);
    }
    private void CheckInstances(string theme, bool revealed)
    {
        foreach (Transform tile in builder.transform)
        {
            string code = tile.name.StartsWith("3F_") ? tile.name.Substring(3, 7) : tile.name.Substring(0, 7);
            var prefab = Resources.Load<GameObject>("Prefabs/" + code);
            foreach (var renderer in tile.GetComponentsInChildren<SpriteRenderer>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(renderer.transform, tile);
                var raw = (path.Length == 0 ? prefab.transform : prefab.transform.Find(path)).GetComponent<SpriteRenderer>().sprite;
                var variant = tile.GetComponentsInChildren<AssetVariantSprite2D>(true).FirstOrDefault(v => v.Renderer == renderer);
                if (variant != null && variant.CurrentAssetNo >= 0) raw = variant.GetSpriteForAssetNo(variant.CurrentAssetNo);
                Check(renderer.sprite == Expected(raw, theme), theme + " " + (revealed ? "revealed " : "hidden ") + tile.name + "/" + path);
            }
        }
    }
    private void Capture(string file)
    {
        var go = new GameObject("CaptureCamera"); var camera = go.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 5.5f; camera.transform.position = new Vector3(5, 3, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
        var rt = new RenderTexture(1200, 1000, 24); rt.Create(); camera.targetTexture = rt;
        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Output, file), image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Destroy(rt); Destroy(image); Destroy(go);
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Exception) return;
        Check(false, "UNHANDLED " + message + "\n" + stack);
        EditorApplication.delayCall += Finish;
    }
    private void Finish()
    {
        if (finished) return; finished = true;
        Application.logMessageReceived -= OnLog;
        File.WriteAllLines(Path.Combine(Output, "runtime-checks.txt"), checks.Concat(new[] { $"TOTAL {checks.Count}, FAILED {failures}" }));
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
