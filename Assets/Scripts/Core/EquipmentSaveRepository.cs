using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.Core
{
    /// <summary>
    /// EquipmentPageUI가 사용하는 세이브 접근 계층입니다.
    ///
    /// 이전 버전은 savefile JSON을 직접 읽고 직접 덮어썼습니다.
    /// 하지만 현재 프로젝트의 실제 세이브 주인은 StatusSaveFile.Instance.Data이므로,
    /// 장착 정보도 반드시 StatusSaveFile의 메모리 데이터를 수정한 뒤 StatusSaveFile.Save()로 저장해야 합니다.
    ///
    /// savefile 저장 규칙:
    /// - inventory.equipment에는 raw id("000") 저장
    /// - equips.equip.head/body/legs에도 raw id("000") 저장
    /// - UI/DB 조회 단계에서만 "equip:000" 같은 category key로 변환
    /// </summary>
    public class EquipmentSaveRepository
    {
        public string CurrentPath { get; private set; }
        public string LastError { get; private set; }

        // EquipmentStatCalculator가 같은 세이브 데이터를 기준으로 계산할 수 있도록 공개합니다.
        public StatusSaveData Data => data;

        private StatusSaveFile statusSaveFile;
        private StatusSaveData data;
        private bool usingStatusSaveFile;

        // StatusSaveFile이 없는 UI 단독 테스트도 동일한 저장 백엔드를 사용합니다.
        private IStatusSaveStorage fallbackStorage;

        public bool Load()
        {
            LastError = string.Empty;
            CurrentPath = string.Empty;
            fallbackStorage = null;
            statusSaveFile = null;
            data = null;
            usingStatusSaveFile = false;

            // 게임 실행 중에는 이 경로가 정답입니다.
            // StatusSaveFile이 들고 있는 메모리 데이터와 파일을 서로 엇갈리게 만들지 않기 위해 우선 사용합니다.
            if (StatusSaveFile.Instance != null)
            {
                statusSaveFile = StatusSaveFile.Instance;
                data = statusSaveFile.Data;
                usingStatusSaveFile = true;
                CurrentPath = statusSaveFile.GetActiveSavePath();

                EnsureDataNotNull();
                return true;
            }

            // UI 단독 테스트에서도 Editor/Player에 맞는 같은 백엔드만 사용합니다.
            try
            {
                fallbackStorage = StatusSaveStorageFactory.Create();
                string generatedDefaultJson = StatusSaveDefaults.Serialize(StatusSaveDefaults.Create());

                if (!fallbackStorage.TryLoadOrCreate(
                        StatusSaveDefaults.IsValidJson,
                        generatedDefaultJson,
                        out string json))
                {
                    LastError = fallbackStorage.LastError;
                    return false;
                }

                if (!StatusSaveDefaults.TryDeserialize(json, out data, out string deserializeError))
                {
                    LastError = $"Savefile 읽기 실패: {fallbackStorage.ActivePath}\n{deserializeError}";
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

        public List<OwnedDbStack> GetOwnedEquipmentStacks()
        {
            if (!EnsureDataAvailable())
                return new List<OwnedDbStack>();

            return BuildOwnedStacks(data.inventory.equipment, GameDatabase.CategoryEquip);
        }

        public List<OwnedDbStack> GetOwnedItemStacks()
        {
            if (!EnsureDataAvailable())
                return new List<OwnedDbStack>();

            return BuildOwnedStacks(data.inventory.item, GameDatabase.CategoryItem);
        }

        public string GetEquippedId(string slot)
        {
            if (!EnsureDataAvailable())
                return GameDatabase.NoneKey;

            slot = GameDatabase.NormalizeSlot(slot);
            if (string.IsNullOrWhiteSpace(slot))
                return GameDatabase.NoneKey;

            foreach (var dict in data.equips.equip)
            {
                if (dict == null)
                    continue;

                if (dict.TryGetValue(slot, out string rawId))
                    return GameDatabase.NormalizeId(rawId);
            }

            return GameDatabase.NoneKey;
        }

        public string GetEquippedKey(string slot)
        {
            string rawId = GetEquippedId(slot);
            if (string.IsNullOrWhiteSpace(rawId) || rawId == GameDatabase.NoneKey)
                return GameDatabase.NoneKey;

            return GameDatabase.MakeKey(GameDatabase.CategoryEquip, rawId);
        }

        public bool SetEquippedId(string slot, string equipmentId)
        {
            if (!EnsureDataAvailable())
            {
                LastError = "세이브 데이터가 없습니다.";
                return false;
            }

            slot = GameDatabase.NormalizeSlot(slot);
            equipmentId = GameDatabase.NormalizeId(equipmentId);

            if (string.IsNullOrWhiteSpace(slot))
            {
                LastError = "slot 값이 비어 있습니다.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(equipmentId))
                equipmentId = GameDatabase.NoneKey;

            for (int i = 0; i < data.equips.equip.Count; i++)
            {
                Dictionary<string, string> dict = data.equips.equip[i];
                if (dict == null)
                    continue;

                if (dict.ContainsKey(slot))
                {
                    dict[slot] = equipmentId;
                    return true;
                }
            }

            // savefile에 해당 슬롯 항목이 없으면 새로 추가합니다.
            data.equips.equip.Add(new Dictionary<string, string>
            {
                { slot, equipmentId }
            });

            return true;
        }

        public bool SetEquippedKey(string slot, string equipmentKey)
        {
            if (!GameDatabase.TryParseKey(equipmentKey, out string category, out string rawId))
            {
                LastError = $"잘못된 장비 key입니다: {equipmentKey}";
                return false;
            }

            if (category != GameDatabase.CategoryEquip)
            {
                LastError = $"장비 슬롯에는 equip category만 장착할 수 있습니다. 입력 key: {equipmentKey}";
                return false;
            }

            return SetEquippedId(slot, rawId);
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

        private static List<OwnedDbStack> BuildOwnedStacks(List<Dictionary<string, int>> source, string category)
        {
            var totals = new Dictionary<string, int>();

            if (source != null)
            {
                foreach (var dict in source)
                {
                    if (dict == null)
                        continue;

                    foreach (var pair in dict)
                    {
                        string rawId = GameDatabase.NormalizeId(pair.Key);
                        if (string.IsNullOrWhiteSpace(rawId) || rawId == GameDatabase.NoneKey)
                            continue;

                        int count = Mathf.Max(0, pair.Value);
                        if (count <= 0)
                            continue;

                        string key = GameDatabase.MakeKey(category, rawId);
                        if (totals.ContainsKey(key))
                            totals[key] += count;
                        else
                            totals[key] = count;
                    }
                }
            }

            var result = new List<OwnedDbStack>();
            foreach (var pair in totals)
                result.Add(new OwnedDbStack(pair.Key, pair.Value));

            return result;
        }

    }

    public readonly struct OwnedDbStack
    {
        public readonly string key;
        public readonly string category;
        public readonly string id;
        public readonly int count;

        public OwnedDbStack(string key, int count)
        {
            this.key = GameDatabase.NormalizeKey(key);

            if (GameDatabase.TryParseKey(this.key, out string parsedCategory, out string parsedId))
            {
                category = parsedCategory;
                id = parsedId;
            }
            else
            {
                category = string.Empty;
                id = string.Empty;
            }

            this.count = count;
        }
    }
}
