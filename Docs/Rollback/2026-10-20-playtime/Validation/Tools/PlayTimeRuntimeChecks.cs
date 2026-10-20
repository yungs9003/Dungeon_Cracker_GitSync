#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dungeon.Core;
using Dungeon.Manager;
using Dungeon.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayTimeRuntimeChecks : MonoBehaviour
{
    private static PlayTimeRuntimeChecks instance;
    private readonly List<string> results = new();
    private StatusSaveFile save;
    private CountingStorage storage;
    private bool failed;
    private string Report => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../Docs/Rollback/2026-10-20-playtime"));
    private void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this; DontDestroyOnLoad(gameObject);
    }
    private IEnumerator Start()
    {
        if (instance != this) yield break;
        var routine = Run();
        while (true)
        {
            object current;
            try { if (!routine.MoveNext()) break; current = routine.Current; }
            catch (Exception error) { failed = true; results.Add("FAIL " + error); Debug.LogError(error); break; }
            yield return current;
        }
        Directory.CreateDirectory(Report);
        File.WriteAllLines(Path.Combine(Report, "runtime-checks.txt"), results);
        if (save != null) File.WriteAllText(Path.Combine(Report, "runtime-final-save.json"), StatusSaveDefaults.Serialize(save.Data));
        Time.timeScale = 1;
        var helper = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PlayTimeRuntimeReview")).First(t => t != null);
        helper.GetMethod("Finish").Invoke(null, new object[] { failed ? 1 : 0 });
    }
    private void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        results.Add("PASS " + label); Debug.Log("[PlayTimeCheck] " + label);
    }
    private IEnumerator Run()
    {
        save = StatusSaveFile.Instance;
        Check(save != null, "MainMenu bootstrap creates save service");
        int saveId = save.GetInstanceID();
        Check(save.Data.play_time_minutes == 0, "fresh generated save starts at zero minutes");
        var field = typeof(StatusSaveFile).GetField("storage", BindingFlags.Instance | BindingFlags.NonPublic);
        storage = new CountingStorage((IStatusSaveStorage)field.GetValue(save)); field.SetValue(save, storage);
        Check(save.GetActiveSavePath().Contains("CodexPlayTime"), "test storage path stays inside isolated project");

        var labelGo = new GameObject("PlayTimeValue", typeof(RectTransform)); labelGo.transform.SetParent(transform);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        var view = labelGo.AddComponent<AdventureRecordDataView>();
        view.Configure(label, null, null, null, null, null, null, null, null, null); view.Refresh();
        Check(label.text == "0 : 00", "Adventure Record shows hours and minutes from numeric value");
        double before = save.Data.play_time_remainder_seconds;
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(1.2f);
        Check(save.Data.play_time_remainder_seconds > before + 1, "total time increases while timeScale is zero in MainMenu");
        Check(storage.writes == 0, "no per-second file write in MainMenu");
        Time.timeScale = 1;
        Debug.Log("[PlayTimeCheck] Waiting for a real sixty-second minute boundary.");
        double deadline = Time.realtimeSinceStartupAsDouble + 65;
        while (save.Data.play_time_minutes < 1 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        yield return null;
        Check(save.Data.play_time_minutes == 1, "real elapsed minute increments saved numeric minutes");
        Check(storage.writes == 1, "exactly one timer write at real minute boundary");
        Check(label.text == "0 : 01", "open Adventure Record refreshes on minute save notification");
        Check(StatusSaveDefaults.TryDeserialize(File.ReadAllText(save.GetActiveSavePath()), out var persisted, out _) && persisted.play_time_minutes == 1,
            "minute value is persisted to the isolated file");

        var manager = SaveManager.GetOrCreate();
        Check(manager.EnterDungeon("DUNGEON_001"), "actual SaveManager enters first dungeon");
        yield return null; yield return null;
        Check(save.GetInstanceID() == saveId, "save singleton persists across scene changes");
        int writes = storage.writes;
        yield return new WaitForSecondsRealtime(.4f);
        save.CapturePlayTime(); float first = save.Data.clear_time_seconds;
        Check(first > .2f, "dungeon time starts after first entry");
        Check(storage.writes == writes, "dungeon frames do not perform one-second saves");
        Time.timeScale = 0; save.CapturePlayTime(); first = save.Data.clear_time_seconds;
        before = save.Data.play_time_remainder_seconds;
        yield return new WaitForSecondsRealtime(.35f);
        save.CapturePlayTime();
        Check(Math.Abs(save.Data.clear_time_seconds - first) < .001f, "in-game pause stops dungeon time");
        Check(save.Data.play_time_remainder_seconds > before + .2, "total time continues during dungeon pause");
        Time.timeScale = 1;
        writes = storage.writes;
        Check(manager.SetCheckpoint("DUNGEON_001", 2, "CheckpointScene", 5), "actual checkpoint transaction saves timing");
        float checkpointTime = save.Data.clear_time_seconds;
        SceneManager.LoadScene("CheckpointScene"); yield return null; yield return null;
        Check(storage.writes == writes + 1, "checkpoint transition uses one combined timing and state write");
        yield return new WaitForSecondsRealtime(.4f);
        Check(Math.Abs(save.Data.clear_time_seconds - checkpointTime) < .001f, "checkpoint waiting freezes dungeon time");
        Check(StatusSaveDefaults.TryDeserialize(File.ReadAllText(save.GetActiveSavePath()), out persisted, out _) &&
            Math.Abs(persisted.checkpoint["DUNGEON_001"].run_time_seconds - checkpointTime) < .001f,
            "checkpoint file contains precise run seconds");
        writes = storage.writes; Invoke(save, "OnApplicationPause", true);
        yield return new WaitForSecondsRealtime(.2f); Invoke(save, "OnApplicationPause", false);
        Check(storage.writes == writes + 1, "application suspension takes one save snapshot");
        Check(manager.ExitCheckpointToSavedNextMap(), "checkpoint exit returns to saved next map");
        yield return null; yield return null;
        Check(MapSequenceManager2D.Instance.CurrentIndex == 2, "checkpoint resume preserves map index two");
        Check(SceneManager.GetActiveScene().GetRootGameObjects().Any(g => g.name == "GameManager" && g.GetComponent<BoxCollider2D>() != null),
            "duplicate legacy tracker removes only itself and preserves the scene GameManager");
        yield return new WaitForSecondsRealtime(.3f); save.CapturePlayTime();
        Check(save.Data.clear_time_seconds > checkpointTime + .15f, "dungeon time resumes from checkpoint seconds");
        Check(save.Save(), "explicit save persists current timing without another timer write");
        first = save.Data.clear_time_seconds; save.Load();
        Check(Math.Abs(save.Data.clear_time_seconds - first) < .001f, "save reload preserves accumulated dungeon seconds");
        Check(manager.CompleteDungeon("DUNGEON_001", 5, "ClearScene"), "normal clear transition prepares exact final time");
        yield return null; yield return null;
        float result = save.Data.clear_time_seconds;
        Check(save.Data.dungeon_records["DUNGEON_001"].clear_count == 1, "ClearScene commits exactly one clear");
        Check(Math.Abs(save.Data.dungeon_records["DUNGEON_001"].best_clear_time_seconds - result) < .001f, "first clear stores actual time as best");
        var clear = FindFirstObjectByType<ClearResultUI>();
        var clearLabel = (TMP_Text)typeof(ClearResultUI).GetField("clearTimeText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clear);
        var expectedText = (string)Invoke(clear, "FormatTime", result);
        Check(clearLabel.text == expectedText, "ClearScene displays this run time");
        before = save.Data.play_time_remainder_seconds;
        yield return new WaitForSecondsRealtime(.4f);
        Check(save.Data.clear_time_seconds == result, "ClearScene freezes dungeon result time");
        Check(save.Data.play_time_remainder_seconds > before + .2, "total time continues on ClearScene");
        writes = storage.writes; clear.Refresh();
        Check(save.Data.dungeon_records["DUNGEON_001"].clear_count == 1 && storage.writes == writes, "result refresh neither recommits time nor duplicates rewards");

        clear.LeaveClearScene(); yield return null;
        Check(manager.EnterDungeon("DUNGEON_001"), "replay starts a new run after completed clear"); yield return null; yield return null;
        yield return new WaitForSecondsRealtime(.2f);
        Check(manager.SetCheckpoint("DUNGEON_001", 2, "CheckpointScene", 5), "replay checkpoint saves new run");
        first = save.Data.clear_time_seconds;
        SceneManager.LoadScene("CheckpointScene"); yield return null;
        Check(manager.EnterDungeon("DUNGEON_002"), "switch to second dungeon"); yield return null; yield return null;
        yield return new WaitForSecondsRealtime(.2f);
        Check(manager.SetCheckpoint("DUNGEON_002", 1, "CheckpointScene", 4), "second dungeon stores separate seconds");
        float second = save.Data.clear_time_seconds;
        SceneManager.LoadScene("CheckpointScene"); yield return null;
        Check(manager.EnterDungeon("DUNGEON_001"), "return to first dungeon selects its checkpoint"); yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name == "CheckpointScene", "returning dungeon resumes through checkpoint scene");
        Check(Math.Abs(save.Data.clear_time_seconds - first) < .001f && Math.Abs(save.Data.checkpoint["DUNGEON_002"].run_time_seconds - second) < .001f,
            "two dungeon clocks remain independent");
        Check(save.Data.dungeon_records["DUNGEON_001"].best_clear_time_seconds == result, "unfinished replay does not alter best time");

        writes = storage.writes; Invoke(save, "OnApplicationQuit"); Invoke(save, "OnApplicationQuit");
        Check(storage.writes == writes + 1, "normal quit captures remainder once despite repeated callback");
        Check(StatusSaveDefaults.TryDeserialize(File.ReadAllText(save.GetActiveSavePath()), out persisted, out _) &&
            Math.Abs(persisted.play_time_remainder_seconds - save.Data.play_time_remainder_seconds) < .001,
            "normal quit persists subminute remainder");
    }
    private static object Invoke(object target, string method, params object[] values) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, values);

    private class CountingStorage : IStatusSaveStorage
    {
        private readonly IStatusSaveStorage inner; public int writes;
        public CountingStorage(IStatusSaveStorage inner) { this.inner = inner; }
        public string ActivePath => inner.ActivePath; public string DefaultPath => inner.DefaultPath;
        public string BackupPath => inner.BackupPath; public string TemporaryPath => inner.TemporaryPath;
        public string LastError => inner.LastError; public string LastRecoveryAction => inner.LastRecoveryAction;
        public bool TryLoadOrCreate(Func<string, bool> validator, string initial, out string json) => inner.TryLoadOrCreate(validator, initial, out json);
        public bool TryWrite(string json, Func<string, bool> validator) { if (!inner.TryWrite(json, validator)) return false; writes++; return true; }
    }
}
#endif
