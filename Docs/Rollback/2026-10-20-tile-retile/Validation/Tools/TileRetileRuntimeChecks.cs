using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dungeon.Core;
using Dungeon.Map;
using Dungeon.Traps;
using UnityEngine;

public sealed class TileRetileRuntimeChecks : MonoBehaviour
{
    private readonly List<string> checks = new();
    private int failures;
    private JsonBlockBuilder2D builder;
    private readonly string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../Docs/Rollback/2026-10-20-tile-retile/Validation"));
    private void Check(bool condition, string label)
    { checks.Add((condition ? "PASS " : "FAIL ") + label); if (!condition) failures++; }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null;
        Check(StatusSaveFile.Instance != null, "isolated save service initialized");
        new GameObject("Registry").AddComponent<BlockRegistry>();
        builder = new GameObject("TestMapBuilder").AddComponent<JsonBlockBuilder2D>();
        builder.autoBuildOnStart = false;
        builder.player = new GameObject("FixturePlayer").transform;
        foreach (string code in new[] { "101-001", "101-004", "101-007" })
            foreach (string direction in new[] { "00", "01", "11", "10" })
            {
                string label = code + "/" + direction;
                Build("CodexRetileChecks/" + code + "_" + direction);
                var map = builder.LoadedMapData;
                Check(map != null && map.grid.Length == 49, label + " initial build");
                if (map == null) continue;
                var before = JsonUtility.FromJson<MapData2D>(JsonUtility.ToJson(map));
                var solver = new TileRetileSolver2D(TileRetileSolver2D.LoadRules(Resources.Load<TextAsset>("Data/TileRules")), new[]{"101-001","101-004","101-007"});
                var plan = solver.DisarmTrapAndResolve(map, "0303");
                Check(BlockRegistry.Instance.TryGet("0303", out var old), label + " registry before");
                if (old == null) continue;
                var destruction = old.GetComponent<Destructible>();
                Check(destruction != null, label + " actual Destructible present");
                if (destruction == null) continue;
                int broken = StatusSaveFile.Instance.GetBrokenTraps();
                if (code == "101-001" && direction == "00") Capture("fixture-before.png");
                destruction.ExecuteDestruction();
                destruction.ExecuteDestruction();
                Check(StatusSaveFile.Instance.GetBrokenTraps() == broken + 1, label + " count once");
                Check(old.GetComponentsInChildren<Collider2D>(true).All(c => !c.enabled), label + " old collision disabled immediately");
                Check(BlockRegistry.Instance.TryGet("0303", out var floor) && floor != old, label + " registry replacement");
                Check(floor != null && floor.transform.parent == builder.transform && floor.GetComponent<BlockCoord>().Coord == "0303", label + " owned floor and coordinate");
                Check(map.grid.Single(c => c.coord == "0303").code == "000-000", label + " map data floor");
                var wallVariants = Resources.Load<GameObject>("Prefabs/001-001").GetComponent<AssetVariantSprite2D>();
                foreach (var r in plan.Skip(1).Where(r => !r.wallVisualOnly))
                {
                    var cell = map.grid.Single(c => c.coord == r.coord);
                    bool found = BlockRegistry.Instance.TryGet(r.coord, out var wall);
                    Check(found && wall.GetComponent<AssetVariantSprite2D>().Renderer.sprite == wallVariants.GetSpriteForAssetNo(r.newAssetNo), label + " sprite " + r.coord);
                    Check(cell.assetNo == r.newAssetNo && cell.direction == r.newDirection, label + " data " + r.coord);
                    Check(found && Quaternion.Angle(wall.transform.rotation, Rotation(r.newDirection)) < 0.01f, label + " rotation " + r.coord);
                }
                var touched = new HashSet<string>(plan.Select(r => r.coord));
                Check(before.grid.Where(c => !touched.Contains(c.coord)).All(c =>
                { var after = map.grid.Single(a => a.coord == c.coord); return c.code == after.code && c.assetNo == after.assetNo && c.direction == after.direction; }), label + " other cells unchanged");
                yield return null; yield return null;
                Check(old == null, label + " old object removed");
                Check(builder.transform.Cast<Transform>().Count(t => t.name == "000-000_0303") == 1, label + " one floor only");
                if (code == "101-001" && direction == "00") Capture("fixture-after.png");
                builder.BuildFromJson(); yield return null;
                Check(floor == null && BlockRegistry.Instance.TryGet("0303", out var rebuilt) && rebuilt.name == "101-001_0303".Replace("101-001", code), label + " rebuild cleans replacement and restores source");
            }

        foreach (string code in new[] { "101-014", "101-015" })
        {
            Build("CodexRetileChecks/" + code + "_00");
            BlockRegistry.Instance.TryGet("0303", out var trap);
            int broken = StatusSaveFile.Instance.GetBrokenTraps();
            Check(trap != null && trap.GetComponent<Destructible>() == null, code + " remains non-destructible prefab");
            Check(builder.TryHandleTileDestruction(trap) == TileDestructionHandling.Rejected, code + " DB rejects forced replacement");
            Check(builder.LoadedMapData.grid.Single(c => c.coord == "0303").code == code && StatusSaveFile.Instance.GetBrokenTraps() == broken, code + " no data/count change");
        }

        Build("CodexRetileChecks/rubble"); BlockRegistry.Instance.TryGet("0303", out var rubble);
        var rubbleObstacle = rubble.GetComponentInChildren<Destructible>();
        Check(rubbleObstacle != null, "001-005 existing child obstacle mechanism");
        rubbleObstacle.ExecuteDestruction(); yield return null; yield return null;
        Check(rubbleObstacle == null && rubble != null && BlockRegistry.Instance.TryGet("0303", out var rubbleFloor) && rubbleFloor == rubble && rubble.GetComponentsInChildren<Collider2D>().Length == 0, "001-005 legacy child removal leaves walkable floor and registry intact");

        Build("CodexRetileChecks/floor3");
        BlockRegistry.Instance.TryGet("0303", out var baseFloor);
        string baseJson = JsonUtility.ToJson(new MapData2D { grid = builder.LoadedMapData.grid });
        var upper = builder.transform.Find("3F_101-001_0303");
        upper.GetComponent<Destructible>().ExecuteDestruction(); yield return null; yield return null;
        Check(BlockRegistry.Instance.TryGet("0303", out var stillBase) && stillBase == baseFloor, "3F does not overwrite base registry");
        Check(JsonUtility.ToJson(new MapData2D { grid = builder.LoadedMapData.grid }) == baseJson, "3F keeps base map data");
        Check(builder.LoadedMapData.floor3.Single(c => c.coord == "0303").code == "000-000" && builder.transform.Find("3F_000-000_0303") != null, "3F replacement data and object");
        Check(builder.transform.Find("3F_000-000_0303").GetComponent<SpriteRenderer>().sortingOrder == builder.floor3SortingOrderBase, "3F sorting band preserved");

        // 기존 발견 시스템과 함께 남아 있는 함정의 벽 외형만 바뀌는지 확인한다.
        var vision = new GameObject("Vision").AddComponent<ForwardZoneVisionMask2D>();
        vision.SetDarkAlpha(0); builder.visionSystem = vision;
        Build("CodexRetileChecks/neighbour");
        BlockRegistry.Instance.TryGet("0403", out var neighbour);
        Quaternion attackRotation = neighbour.transform.rotation;
        var neighbourCell = builder.LoadedMapData.grid.Single(c => c.coord == "0403");
        string attackDir = neighbourCell.direction; int attackAsset = neighbourCell.assetNo;
        BlockRegistry.Instance.TryGet("0303", out var centre); centre.GetComponent<Destructible>().ExecuteDestruction();
        var overlay = neighbour.transform.Find("RetiledHiddenWall");
        var hidden = neighbour.GetComponent<HiddenTrapVisual2D>();
        Check(overlay != null, "remaining hidden wall trap has separate wall visual");
        Check(neighbourCell.direction == attackDir && neighbourCell.assetNo == attackAsset && Quaternion.Angle(neighbour.transform.rotation, attackRotation) < 0.01f, "remaining trap attack direction and reveal metadata preserved");
        hidden.ForceReveal();
        Check(overlay.GetComponent<SpriteRenderer>().forceRenderingOff && !neighbour.GetComponent<SpriteRenderer>().forceRenderingOff, "reveal hides overlay and restores trap renderer");
        hidden.ForceHideAgain();
        Check(!overlay.GetComponent<SpriteRenderer>().forceRenderingOff && neighbour.GetComponent<SpriteRenderer>().forceRenderingOff, "hide uses retiled wall again");
        yield return null; yield return null;

        Build("CodexRetileChecks/forceNeighbour");
        BlockRegistry.Instance.TryGet("0403", out var forceTrap);
        Quaternion forceRotation = forceTrap.transform.rotation;
        BlockRegistry.Instance.TryGet("0303", out centre); centre.GetComponent<Destructible>().ExecuteDestruction();
        Check(Quaternion.Angle(forceTrap.transform.rotation,forceRotation)<0.01f && forceTrap.transform.Find("WallVisual")!=null, "always-visible wall force trap keeps attack root");
        yield return null; yield return null;

        foreach (string path in new[] { "Maps/DUNGEON_001/map_001", "Maps/DUNGEON_001/map_002" })
        {
            var text = Resources.Load<TextAsset>(path); string source = text.text;
            var sourceMap = JsonUtility.FromJson<MapData2D>(source.Replace("\"3F\"", "\"floor3\""));
            Build(path); yield return null;
            Check(JsonUtility.ToJson(builder.LoadedMapData) == JsonUtility.ToJson(sourceMap), path + " initial generation preserves authored map fields");
            var target = builder.LoadedMapData.grid.FirstOrDefault(c => c.code == "101-001");
            if (target != null && BlockRegistry.Instance.TryGet(target.coord, out var actualTrap))
            {
                actualTrap.GetComponent<Destructible>().ExecuteDestruction(); yield return null; yield return null;
                Check(target.code == "000-000" && BlockRegistry.Instance.TryGet(target.coord, out var actualFloor) && actualFloor.name.StartsWith("000-000_"), path + " actual map destruction integration");
                if (path.EndsWith("map_001")) Capture("actual-map-after.png");
            }
            Check(text.text == source, path + " resource JSON unchanged");
        }
        Finish();
    }
    private void Build(string path)
    {
        builder.BuildFromJsonPath(path);
        foreach (var toggle in builder.GetComponentsInChildren<MonoBehaviour>(true).OfType<ITrapToggle>()) toggle.SetTrapEnabled(false);
    }
    private static Quaternion Rotation(string dir) => Quaternion.Euler(0, 0, dir == "01" ? -90 : dir == "10" ? 90 : dir == "11" ? 180 : 0);
    private void Capture(string name)
    {
        var go = new GameObject("CaptureCamera"); var camera = go.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 3.7f; camera.transform.position = new Vector3(3, 3, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
        var rt = new RenderTexture(900, 900, 24); rt.Create(); camera.targetTexture = rt;
        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Destroy(rt); Destroy(image); Destroy(go);
    }
    private void OnLog(string text, string stack, LogType type)
    {
        if (type != LogType.Exception) return;
        checks.Add("FAIL UNHANDLED EXCEPTION " + text + "\n" + stack); failures++;
        UnityEditor.EditorApplication.delayCall += Finish;
    }
    private void Finish()
    {
        Application.logMessageReceived -= OnLog;
        File.WriteAllLines(Path.Combine(output,"runtime-checks.txt"),checks.Concat(new[]{ $"TOTAL {checks.Count}, FAILED {failures}" }));
        UnityEditor.EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
