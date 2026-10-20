#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dungeon.Core;
using UnityEngine;

public class PlayTimeDirectChecks : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null; yield return null;
        var results = new List<string>(); bool failed = false;
        StatusSaveFile save = StatusSaveFile.Instance;
        Action<bool, string> check = (ok, name) => { results.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) failed = true; };
        check(save != null, "direct dungeon startup creates save service");
        check(FindObjectsByType<StatusSaveFile>(FindObjectsSortMode.None).Length == 1, "scene save component and bootstrap leave exactly one service");
        DateTime modified = File.GetLastWriteTimeUtc(save.GetActiveSavePath());
        yield return new WaitForSecondsRealtime(1.3f);
        save.CapturePlayTime();
        check(save.Data.clear_time_seconds > 1, "direct dungeon startup initializes active run and measures time");
        check(save.Data.play_time_remainder_seconds > 1, "direct dungeon startup measures total time");
        check(File.GetLastWriteTimeUtc(save.GetActiveSavePath()) == modified, "direct dungeon has no one-second save");
        float before = save.Data.clear_time_seconds;
        double total = save.Data.play_time_remainder_seconds; Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(.2f); save.CapturePlayTime();
        check(save.Data.clear_time_seconds == before, "direct dungeon pause freezes run time");
        check(save.Data.play_time_remainder_seconds > total + .1, "direct dungeon pause keeps total clock running");
        Time.timeScale = 1;
        typeof(StatusSaveFile).GetMethod("OnApplicationQuit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(save, null);
        StatusSaveDefaults.TryDeserialize(File.ReadAllText(save.GetActiveSavePath()), out var stored, out _);
        check(Math.Abs(stored.play_time_remainder_seconds - save.Data.play_time_remainder_seconds) < .001, "direct dungeon quit persists remainder");
        string report = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../Docs/Rollback/2026-10-20-playtime/direct-scene-checks.txt"));
        File.WriteAllLines(report, results);
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PlayTimeRuntimeReview")).First(t => t != null)
            .GetMethod("Finish").Invoke(null, new object[] { failed ? 1 : 0 });
    }
}
#endif
