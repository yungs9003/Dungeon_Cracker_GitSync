using System.IO;
using UnityEngine;

namespace Dungeon.Core
{
    public static class SaveFileManager
    {
        private static string SavePath
        {
            get
            {
#if UNITY_EDITOR
                return Path.Combine(Application.dataPath, "Resources/Data/savefile.json");
#else
                return Path.Combine(Application.persistentDataPath, "savefile.json");
#endif
            }
        }

        public static void EnsureSaveFileExists()
        {
            string path = SavePath;

            if (File.Exists(path))
                return;

            TextAsset defaultSave = Resources.Load<TextAsset>("Data/savefile");

            if (defaultSave == null)
            {
                Debug.LogError("[SaveFileManager] Resources/Data/savefile.json을 찾을 수 없습니다.");
                return;
            }

            File.WriteAllText(path, defaultSave.text);
            Debug.Log("[SaveFileManager] Save file created: " + path);
        }

        public static void AddDummy1(int amount)
        {
            EnsureSaveFileExists();

            string json = ReadJson();
            if (string.IsNullOrEmpty(json)) return;

            if (!TryReadIntValue(json, "dummy1", out int current))
            {
                Debug.LogWarning("[SaveFileManager] dummy1 값을 찾지 못했습니다.");
                return;
            }

            int next = current + amount;
            json = ReplaceIntValue(json, "dummy1", next);

            WriteJson(json);

            Debug.Log($"[SaveFileManager] dummy1 increased: {current} -> {next}");
        }

        public static void SaveCheckpoint(string sceneName, int mapIndex)
        {
            EnsureSaveFileExists();

            string json = ReadJson();
            if (string.IsNullOrEmpty(json)) return;

            json = ReplaceStringValue(json, "sceneName", sceneName);
            json = ReplaceIntValue(json, "mapIndex", mapIndex);
            json = ReplaceBoolValue(json, "resumeRequested", false);

            WriteJson(json);

            Debug.Log($"[SaveFileManager] Checkpoint saved. Scene={sceneName}, MapIndex={mapIndex}");
        }

        public static void RequestCheckpointResume()
        {
            EnsureSaveFileExists();

            string json = ReadJson();
            if (string.IsNullOrEmpty(json)) return;

            json = ReplaceBoolValue(json, "resumeRequested", true);

            WriteJson(json);

            Debug.Log("[SaveFileManager] Checkpoint resume requested.");
        }

        public static bool HasCheckpointResumeRequest()
        {
            EnsureSaveFileExists();

            string json = ReadJson();
            if (string.IsNullOrEmpty(json)) return false;

            if (!TryReadBoolValue(json, "resumeRequested", out bool requested))
                return false;

            return requested;
        }

        public static void ClearCheckpointResumeRequest()
        {
            EnsureSaveFileExists();

            string json = ReadJson();
            if (string.IsNullOrEmpty(json)) return;

            json = ReplaceBoolValue(json, "resumeRequested", false);

            WriteJson(json);

            Debug.Log("[SaveFileManager] Checkpoint resume request cleared.");
        }

        public static string GetCheckpointSceneName()
        {
            EnsureSaveFileExists();

            string json = ReadJson();
            if (string.IsNullOrEmpty(json)) return "";

            if (!TryReadStringValue(json, "sceneName", out string sceneName))
                return "";

            return sceneName;
        }

        public static int GetCheckpointMapIndex()
        {
            EnsureSaveFileExists();

            string json = ReadJson();
            if (string.IsNullOrEmpty(json)) return -1;

            if (!TryReadIntValue(json, "mapIndex", out int mapIndex))
                return -1;

            return mapIndex;
        }

        private static string ReadJson()
        {
            string path = SavePath;

            if (!File.Exists(path))
            {
                Debug.LogWarning("[SaveFileManager] Save file not found: " + path);
                return "";
            }

            return File.ReadAllText(path);
        }

        private static void WriteJson(string json)
        {
            File.WriteAllText(SavePath, json);
        }

        private static bool TryReadIntValue(string json, string key, out int value)
        {
            value = 0;

            if (!TryFindValueRange(json, key, out int valueStart, out int valueEnd))
                return false;

            string raw = json.Substring(valueStart, valueEnd - valueStart).Trim();

            return int.TryParse(raw, out value);
        }

        private static bool TryReadBoolValue(string json, string key, out bool value)
        {
            value = false;

            if (!TryFindValueRange(json, key, out int valueStart, out int valueEnd))
                return false;

            string raw = json.Substring(valueStart, valueEnd - valueStart).Trim();

            return bool.TryParse(raw, out value);
        }

        private static bool TryReadStringValue(string json, string key, out string value)
        {
            value = "";

            string target = $"\"{key}\"";
            int keyIndex = json.IndexOf(target);

            if (keyIndex < 0)
                return false;

            int colonIndex = json.IndexOf(':', keyIndex);
            if (colonIndex < 0)
                return false;

            int quoteStart = json.IndexOf('"', colonIndex + 1);
            if (quoteStart < 0)
                return false;

            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0)
                return false;

            value = json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            return true;
        }

        private static string ReplaceIntValue(string json, string key, int newValue)
        {
            if (!TryFindValueRange(json, key, out int valueStart, out int valueEnd))
            {
                Debug.LogWarning($"[SaveFileManager] {key} 값을 찾지 못했습니다.");
                return json;
            }

            return json.Substring(0, valueStart) +
                   newValue +
                   json.Substring(valueEnd);
        }

        private static string ReplaceBoolValue(string json, string key, bool newValue)
        {
            if (!TryFindValueRange(json, key, out int valueStart, out int valueEnd))
            {
                Debug.LogWarning($"[SaveFileManager] {key} 값을 찾지 못했습니다.");
                return json;
            }

            string valueText = newValue ? "true" : "false";

            return json.Substring(0, valueStart) +
                   valueText +
                   json.Substring(valueEnd);
        }

        private static string ReplaceStringValue(string json, string key, string newValue)
        {
            string target = $"\"{key}\"";
            int keyIndex = json.IndexOf(target);

            if (keyIndex < 0)
            {
                Debug.LogWarning($"[SaveFileManager] {key} 값을 찾지 못했습니다.");
                return json;
            }

            int colonIndex = json.IndexOf(':', keyIndex);
            if (colonIndex < 0)
                return json;

            int quoteStart = json.IndexOf('"', colonIndex + 1);
            if (quoteStart < 0)
                return json;

            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0)
                return json;

            return json.Substring(0, quoteStart + 1) +
                   newValue +
                   json.Substring(quoteEnd);
        }

        private static bool TryFindValueRange(string json, string key, out int valueStart, out int valueEnd)
        {
            valueStart = -1;
            valueEnd = -1;

            string target = $"\"{key}\"";
            int keyIndex = json.IndexOf(target);

            if (keyIndex < 0)
                return false;

            int colonIndex = json.IndexOf(':', keyIndex);
            if (colonIndex < 0)
                return false;

            valueStart = colonIndex + 1;

            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            valueEnd = valueStart;

            while (valueEnd < json.Length &&
                   json[valueEnd] != ',' &&
                   json[valueEnd] != '}' &&
                   json[valueEnd] != '\n' &&
                   json[valueEnd] != '\r')
            {
                valueEnd++;
            }

            return valueStart >= 0 && valueEnd > valueStart;
        }
        public static void AddGold(int amount)
{
    EnsureSaveFileExists();

    string json = ReadJson();
    if (string.IsNullOrEmpty(json)) return;

    if (!TryReadIntValue(json, "gold", out int current))
    {
        // gold가 ""처럼 비어 있으면 0으로 취급
        current = 0;
    }

    int next = current + amount;
    json = ReplaceIntValue(json, "gold", next);

    WriteJson(json);

    Debug.Log($"[SaveFileManager] gold increased: {current} -> {next}");
}

public static int GetGold()
{
    EnsureSaveFileExists();

    string json = ReadJson();
    if (string.IsNullOrEmpty(json)) return 0;

    if (!TryReadIntValue(json, "gold", out int gold))
        return 0;

    return gold;
}

public static int GetDummy1()
{
    EnsureSaveFileExists();

    string json = ReadJson();
    if (string.IsNullOrEmpty(json)) return 0;

    if (!TryReadIntValue(json, "dummy1", out int dummy1))
        return 0;

    return dummy1;
}
    }
}