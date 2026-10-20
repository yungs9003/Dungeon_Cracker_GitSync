#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dungeon.Core;
using Dungeon.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClearRankChecks : MonoBehaviour
{
    private readonly List<string> checks = new();
    private string Report => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../Docs/Rollback/2026-10-20-clear-rank"));
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        checks.Add("PASS " + message); Debug.Log("[ClearRankCheck] " + message);
    }
    private IEnumerator Start()
    {
        yield return null; yield return null;
        bool failed = false; var routine = Run();
        while (true)
        {
            object current;
            try { if (!routine.MoveNext()) break; current = routine.Current; }
            catch (Exception e) { failed = true; checks.Add("FAIL " + e); Debug.LogError(e); break; }
            yield return current;
        }
        Directory.CreateDirectory(Report); File.WriteAllLines(Path.Combine(Report, "runtime-checks.txt"), checks);
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ClearRankReview")).First(t => t != null)
            .GetMethod("Finish").Invoke(null, new object[] { failed ? 1 : 0 });
    }
    private IEnumerator Run()
    {
        var save = StatusSaveFile.Instance; var ui = FindFirstObjectByType<ClearResultUI>();
        var label = (TMP_Text)Get(ui, "clearTimeText"); var rankImage = (Image)Get(ui, "clearRankImage");
        Check(save != null && ui != null, "actual ClearScene initializes shared save and result UI");
        Check(save.GetActiveSavePath().Contains("CodexPlayTime"), "storage stays inside isolated project");
        Check(label.text == "02:07", "actual ClearScene truncates 127.54 to 02:07");
        Check(save.Data.save_version == 5 && save.Data.clear_time_seconds == 127, "version4 fractional run migrates to integer version5");
        Check(save.Data.checkpoint["DUNGEON_001"].run_time_seconds == 127, "checkpoint run uses whole seconds");
        Check(save.Data.dungeon_records["DUNGEON_001"].best_clear_time_seconds == 127, "first actual best record uses whole seconds");
        Check(save.Data.dungeon_records["DUNGEON_001"].clear_count == 1, "ClearScene commits once");
        string json = File.ReadAllText(save.GetActiveSavePath());
        Check(json.Contains("\"clear_time_seconds\": 127,") && json.Contains("\"run_time_seconds\": 127,") &&
              json.Contains("\"best_clear_time_seconds\": 127,"), "actual file stores all dungeon times as JSON integers");
        File.WriteAllText(Path.Combine(Report, "initial-clear-save.json"), json);
        Check(ui.DisplayedRank == DungeonClearRank.S && rankImage.enabled, "24 hits and 127 seconds produce visible S");
        foreach (char rank in "sabc") Check(Get(ui, rank + "RankSprite") != null, "provided " + char.ToUpper(rank) + " sprite is serialized and imported");
        Check(rankImage.preserveAspect && !rankImage.raycastTarget, "rank image keeps aspect and does not intercept buttons");
        var button = GameObject.Find("RTMButton").GetComponent<RectTransform>();
        Check(!Overlaps(rankImage.rectTransform, button), "rank image does not cover return button");

        int[] times = {127, 180, 180, 240}; int[] hits = {24, 20, 40, 60};
        DungeonClearRank[] ranks = {DungeonClearRank.S, DungeonClearRank.A, DungeonClearRank.B, DungeonClearRank.C};
        for (int i = 0; i < ranks.Length; i++)
        {
            save.Data.clear_time_seconds = times[i]; save.Data.hit_count = hits[i]; ui.Refresh();
            yield return null;
            var expected = (Sprite)Get(ui, ranks[i].ToString().ToLowerInvariant() + "RankSprite");
            Check(ui.DisplayedRank == ranks[i] && rankImage.sprite == expected && rankImage.enabled, "current run selects " + ranks[i] + " image");
            Check(label.text == DungeonTime.Format(times[i]) && !label.text.Contains('.'), "time label stays whole-second for " + ranks[i]);
            Check(save.Data.dungeon_records["DUNGEON_001"].best_clear_time_seconds == 127 && save.Data.dungeon_records["DUNGEON_001"].clear_count == 1,
                "refresh keeps historical best and count while evaluating current " + ranks[i]);
            Capture(Path.Combine(Report, "clear-" + ranks[i] + ".png"));
        }
        save.Data.active_run = new ActiveDungeonRunData(); ui.Refresh();
        Check(ui.DisplayedRank == DungeonClearRank.None && !rankImage.enabled, "no actual completed run hides rank instead of granting S");
        DungeonProgressService.Enter(save.Data, "DUNGEON_002", 4);
        DungeonProgressService.PrepareClear(save.Data, "DUNGEON_002", 4); ui.Refresh();
        Check(DungeonProgressService.HasCompletedActiveRun(save.Data), "second fixture run is actually completed");
        Check(ui.DisplayedRank == DungeonClearRank.None && !rankImage.enabled, "dungeon without DB references shows no invented rank");
        ui.Refresh(); Check(save.Data.dungeon_records["DUNGEON_002"].clear_count == 1, "repeated result refresh remains idempotent");
    }
    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static bool Overlaps(RectTransform a, RectTransform b)
    {
        var ca = new Vector3[4]; var cb = new Vector3[4]; a.GetWorldCorners(ca); b.GetWorldCorners(cb);
        return new Rect(ca[0], ca[2] - ca[0]).Overlaps(new Rect(cb[0], cb[2] - cb[0]));
    }
    private static void Capture(string path)
    {
        var canvas = FindFirstObjectByType<Canvas>(); var camera = FindFirstObjectByType<Camera>();
        var target = new RenderTexture(1920, 1080, 24); target.Create();
        camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Destroy(image); Destroy(target);
    }
}
#endif
