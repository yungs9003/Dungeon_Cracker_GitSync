using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.Core
{
    /// <summary>
    /// 모듈 보유/장착 데이터를 StatusSaveFile의 안전 저장 경로로 읽고 씁니다.
    /// JSON에는 raw id를, UI와 DB 조회에는 module:id category key를 사용합니다.
    /// </summary>
    public sealed class ModuleSaveRepository
    {
        public const int SlotCount = 6;

        public string CurrentPath { get; private set; }
        public string LastError { get; private set; }
        public StatusSaveData Data => data;

        private StatusSaveFile statusSaveFile;
        private StatusSaveData data;
        private bool usingStatusSaveFile;
        private IStatusSaveStorage fallbackStorage;

        public bool Load()
        {
            LastError = string.Empty;
            CurrentPath = string.Empty;
            fallbackStorage = null;
            statusSaveFile = null;
            data = null;
            usingStatusSaveFile = false;

            if (StatusSaveFile.Instance != null)
            {
                statusSaveFile = StatusSaveFile.Instance;
                data = statusSaveFile.Data;
                usingStatusSaveFile = true;
                CurrentPath = statusSaveFile.GetActiveSavePath();
                EnsureDataNotNull();
                return true;
            }

            try
            {
                fallbackStorage = StatusSaveStorageFactory.Create();
                string generated = StatusSaveDefaults.Serialize(StatusSaveDefaults.Create());

                if (!fallbackStorage.TryLoadOrCreate(
                        StatusSaveDefaults.IsValidJson,
                        generated,
                        out string json))
                {
                    LastError = fallbackStorage.LastError;
                    return false;
                }

                if (!StatusSaveDefaults.TryDeserialize(json, out data, out string error))
                {
                    LastError = $"Savefile 읽기 실패: {fallbackStorage.ActivePath}\n{error}";
                    return false;
                }

                CurrentPath = fallbackStorage.ActivePath;
                EnsureDataNotNull();
                return true;
            }
            catch (Exception e)
            {
                LastError = $"Savefile 읽기 실패: {fallbackStorage?.ActivePath}\n{e}";
                return false;
            }
        }

        public bool Save()
        {
            if (data == null)
            {
                LastError = "저장할 세이브 데이터가 없습니다. Load()를 먼저 호출하세요.";
                return false;
            }

            EnsureDataNotNull();

            try
            {
                if (usingStatusSaveFile && statusSaveFile != null)
                {
                    if (!statusSaveFile.Save())
                    {
                        LastError = "StatusSaveFile 안전 저장에 실패했습니다.";
                        return false;
                    }

                    CurrentPath = statusSaveFile.GetActiveSavePath();
                    return true;
                }

                if (fallbackStorage == null)
                {
                    LastError = "fallback 저장소가 없습니다.";
                    return false;
                }

                string json = StatusSaveDefaults.Serialize(data);
                if (!fallbackStorage.TryWrite(json, StatusSaveDefaults.IsValidJson))
                {
                    LastError = fallbackStorage.LastError;
                    return false;
                }

                CurrentPath = fallbackStorage.ActivePath;
                return true;
            }
            catch (Exception e)
            {
                LastError = $"Savefile 저장 실패: {CurrentPath}\n{e}";
                return false;
            }
        }

        public List<OwnedDbStack> GetOwnedModuleStacks()
        {
            if (!EnsureDataAvailable())
                return new List<OwnedDbStack>();

            var totals = new Dictionary<string, int>();

            foreach (Dictionary<string, int> map in data.inventory.module)
            {
                if (map == null)
                    continue;

                foreach (KeyValuePair<string, int> pair in map)
                {
                    string id = GameDatabase.NormalizeId(pair.Key);
                    int count = Mathf.Max(0, pair.Value);
                    if (id == GameDatabase.NoneKey || count <= 0)
                        continue;

                    string key = GameDatabase.MakeKey(GameDatabase.CategoryModule, id);
                    totals[key] = totals.TryGetValue(key, out int current)
                        ? current + count
                        : count;
                }
            }

            var result = new List<OwnedDbStack>();
            foreach (KeyValuePair<string, int> pair in totals)
                result.Add(new OwnedDbStack(pair.Key, pair.Value));

            result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return result;
        }

        public bool IsOwned(string rawId)
        {
            rawId = GameDatabase.NormalizeId(rawId);
            foreach (OwnedDbStack stack in GetOwnedModuleStacks())
            {
                if (stack.id == rawId && stack.count > 0)
                    return true;
            }

            return false;
        }

        public string GetEquippedId(int slotNumber)
        {
            if (!EnsureDataAvailable() || !IsValidSlot(slotNumber))
                return GameDatabase.NoneKey;

            string slotKey = GetSlotKey(slotNumber);
            foreach (Dictionary<string, string> map in data.equips.module)
            {
                if (map != null && map.TryGetValue(slotKey, out string rawId))
                    return GameDatabase.NormalizeId(rawId);
            }

            return GameDatabase.NoneKey;
        }

        public string GetEquippedKey(int slotNumber)
        {
            string id = GetEquippedId(slotNumber);
            return id == GameDatabase.NoneKey
                ? GameDatabase.NoneKey
                : GameDatabase.MakeKey(GameDatabase.CategoryModule, id);
        }

        public int FindEquippedSlot(string rawId)
        {
            rawId = GameDatabase.NormalizeId(rawId);
            if (rawId == GameDatabase.NoneKey)
                return 0;

            for (int slot = 1; slot <= SlotCount; slot++)
            {
                if (GetEquippedId(slot) == rawId)
                    return slot;
            }

            return 0;
        }

        public int FindFirstEmptySlot()
        {
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                if (GetEquippedId(slot) == GameDatabase.NoneKey)
                    return slot;
            }

            return 0;
        }

        public bool SetEquippedId(int slotNumber, string moduleId)
        {
            LastError = string.Empty;

            if (!EnsureDataAvailable())
            {
                LastError = "세이브 데이터가 없습니다.";
                return false;
            }

            if (!IsValidSlot(slotNumber))
            {
                LastError = $"잘못된 모듈 슬롯 번호입니다: {slotNumber}";
                return false;
            }

            moduleId = GameDatabase.NormalizeId(moduleId);
            if (moduleId != GameDatabase.NoneKey && !IsOwned(moduleId))
            {
                LastError = $"보유하지 않은 모듈은 장착할 수 없습니다: {moduleId}";
                return false;
            }

            // 같은 모듈은 한 슬롯에만 존재하도록 기존 중복을 먼저 제거합니다.
            if (moduleId != GameDatabase.NoneKey)
            {
                for (int slot = 1; slot <= SlotCount; slot++)
                {
                    if (slot != slotNumber && GetEquippedId(slot) == moduleId)
                        SetSlotValue(slot, GameDatabase.NoneKey);
                }
            }

            SetSlotValue(slotNumber, moduleId);
            return true;
        }

        public bool ClearSlot(int slotNumber)
        {
            return SetEquippedId(slotNumber, GameDatabase.NoneKey);
        }

        private void SetSlotValue(int slotNumber, string rawId)
        {
            string slotKey = GetSlotKey(slotNumber);
            Dictionary<string, string> target = null;

            foreach (Dictionary<string, string> map in data.equips.module)
            {
                if (map == null || !map.ContainsKey(slotKey))
                    continue;

                if (target == null)
                    target = map;
                else
                    map.Remove(slotKey);
            }

            if (target == null)
            {
                target = new Dictionary<string, string>();
                data.equips.module.Add(target);
            }

            target[slotKey] = rawId;
        }

        private bool EnsureDataAvailable()
        {
            if (data == null)
                return false;

            EnsureDataNotNull();
            return true;
        }

        private void EnsureDataNotNull()
        {
            data ??= StatusSaveDefaults.Create();
            StatusSaveDefaults.Normalize(data);
        }

        private static bool IsValidSlot(int slotNumber)
        {
            return slotNumber >= 1 && slotNumber <= SlotCount;
        }

        private static string GetSlotKey(int slotNumber)
        {
            return $"slot{slotNumber}";
        }
    }
}
