using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.Core
{
    /// <summary>
    /// 현재 장착 장비를 기준으로 최종 스탯을 다시 계산합니다.
    ///
    /// 원칙:
    /// - base_main_stats/base_sub_stats는 장비 보정 전의 기준 스탯입니다.
    /// - main_stats/sub_stats는 기준 스탯 + 장비 보정이 반영된 최종 스탯입니다.
    /// - 장착/해제/교체 때마다 기준 스탯에서 다시 계산하므로 보너스가 중복 누적되지 않습니다.
    /// </summary>
    public static class EquipmentStatCalculator
    {
        public static bool Recalculate(GameDatabase database = null, bool saveImmediately = false)
        {
            StatusSaveFile saveFile = StatusSaveFile.Instance;
            if (saveFile == null || saveFile.Data == null)
            {
                Debug.LogWarning("[EquipmentStatCalculator] StatusSaveFile.Instance가 없어 스탯을 계산할 수 없습니다.");
                return false;
            }

            bool result = Recalculate(saveFile.Data, database);

            if (result && saveImmediately)
                saveFile.Save();

            return result;
        }

        public static bool Recalculate(StatusSaveData data, GameDatabase database = null)
        {
            if (data == null)
            {
                Debug.LogWarning("[EquipmentStatCalculator] StatusSaveData가 없습니다.");
                return false;
            }

            database ??= GameDatabase.Instance;
            if (database == null)
            {
                Debug.LogWarning("[EquipmentStatCalculator] GameDatabase가 없습니다.");
                return false;
            }

            EnsureData(data);
            EnsureBaseStats(data);

            MainStats finalMain = CloneMainStats(data.base_main_stats);
            SubStats finalSub = CloneSubStats(data.base_sub_stats);

            float maxHpMul = 1f;
            float physicalResistMul = 1f;
            float magicalResistMul = 1f;

            float speedMul = 1f;
            float poisonResistMul = 1f;
            float electricResistMul = 1f;
            float amberResistMul = 1f;
            float freezeResistMul = 1f;

            foreach (string rawId in GetEquippedRawIds(data))
            {
                if (string.IsNullOrWhiteSpace(rawId) || rawId == GameDatabase.NoneKey)
                    continue;

                if (!database.TryGetEquipment(rawId, out EquipmentDefinition equip) || equip == null)
                {
                    Debug.LogWarning($"[EquipmentStatCalculator] db.json에서 장착 장비 id '{rawId}'를 찾지 못했습니다.");
                    continue;
                }

                AddMainBonus(finalMain, equip.main_stats);
                AddSubBonus(finalSub, equip.sub_stats);

                MultiplyIfActive(ref maxHpMul, equip.main_stats_multiplier != null ? equip.main_stats_multiplier.max_HP : 0f);
                MultiplyIfActive(ref physicalResistMul, equip.main_stats_multiplier != null ? equip.main_stats_multiplier.physical_resist : 0f);
                MultiplyIfActive(ref magicalResistMul, equip.main_stats_multiplier != null ? equip.main_stats_multiplier.magical_resist : 0f);

                MultiplyIfActive(ref speedMul, equip.sub_stats_multiplier != null ? equip.sub_stats_multiplier.speed : 0f);
                MultiplyIfActive(ref poisonResistMul, equip.sub_stats_multiplier != null ? equip.sub_stats_multiplier.poison_resist : 0f);
                MultiplyIfActive(ref electricResistMul, equip.sub_stats_multiplier != null ? equip.sub_stats_multiplier.electric_resist : 0f);
                MultiplyIfActive(ref amberResistMul, equip.sub_stats_multiplier != null ? equip.sub_stats_multiplier.amber_resist : 0f);
                MultiplyIfActive(ref freezeResistMul, equip.sub_stats_multiplier != null ? equip.sub_stats_multiplier.freeze_resist : 0f);
            }

            finalMain.max_HP = Mathf.Max(1, Mathf.RoundToInt(finalMain.max_HP * maxHpMul));
            finalMain.physical_resist = Mathf.RoundToInt(finalMain.physical_resist * physicalResistMul);
            finalMain.magical_resist = Mathf.RoundToInt(finalMain.magical_resist * magicalResistMul);

            finalSub.speed = Mathf.RoundToInt(finalSub.speed * speedMul);
            finalSub.poison_resist = Mathf.RoundToInt(finalSub.poison_resist * poisonResistMul);
            finalSub.electric_resist = Mathf.RoundToInt(finalSub.electric_resist * electricResistMul);
            finalSub.amber_resist = Mathf.RoundToInt(finalSub.amber_resist * amberResistMul);
            finalSub.freeze_resist = Mathf.RoundToInt(finalSub.freeze_resist * freezeResistMul);

            data.main_stats = finalMain;
            data.sub_stats = finalSub;

            Debug.Log(
                "[EquipmentStatCalculator] Recalculated stats. " +
                $"maxHP={data.main_stats.max_HP}, physical={data.main_stats.physical_resist}, magical={data.main_stats.magical_resist}, " +
                $"speed={data.sub_stats.speed}, poison={data.sub_stats.poison_resist}, electric={data.sub_stats.electric_resist}, " +
                $"amber={data.sub_stats.amber_resist}, freeze={data.sub_stats.freeze_resist}"
            );

            return true;
        }

        private static void EnsureData(StatusSaveData data)
        {
            data.main_stats ??= new MainStats();
            data.sub_stats ??= new SubStats();

            data.inventory ??= new InventoryData();
            data.inventory.item ??= new List<Dictionary<string, int>>();
            data.inventory.equipment ??= new List<Dictionary<string, int>>();

            data.equips ??= new EquipsData();
            data.equips.item ??= new List<Dictionary<string, string>>();
            data.equips.equip ??= new List<Dictionary<string, string>>();
        }

        private static void EnsureBaseStats(StatusSaveData data)
        {
            if (data.base_main_stats == null)
                data.base_main_stats = CloneMainStats(data.main_stats);

            if (data.base_sub_stats == null)
                data.base_sub_stats = CloneSubStats(data.sub_stats);
        }

        private static IEnumerable<string> GetEquippedRawIds(StatusSaveData data)
        {
            if (data.equips == null || data.equips.equip == null)
                yield break;

            var emitted = new HashSet<string>();

            foreach (var dict in data.equips.equip)
            {
                if (dict == null)
                    continue;

                foreach (var pair in dict)
                {
                    string rawId = GameDatabase.NormalizeId(pair.Value);
                    if (string.IsNullOrWhiteSpace(rawId) || rawId == GameDatabase.NoneKey)
                        continue;

                    if (emitted.Add(rawId))
                        yield return rawId;
                }
            }
        }

        private static MainStats CloneMainStats(MainStats source)
        {
            source ??= new MainStats();
            return new MainStats
            {
                max_HP = source.max_HP,
                physical_resist = source.physical_resist,
                magical_resist = source.magical_resist
            };
        }

        private static SubStats CloneSubStats(SubStats source)
        {
            source ??= new SubStats();
            return new SubStats
            {
                speed = source.speed,
                poison_resist = source.poison_resist,
                electric_resist = source.electric_resist,
                amber_resist = source.amber_resist,
                freeze_resist = source.freeze_resist
            };
        }

        private static void AddMainBonus(MainStats target, EquipmentMainStatBonus bonus)
        {
            if (target == null || bonus == null)
                return;

            target.max_HP += bonus.max_HP;
            target.physical_resist += bonus.physical_resist;
            target.magical_resist += bonus.magical_resist;
        }

        private static void AddSubBonus(SubStats target, EquipmentSubStatBonus bonus)
        {
            if (target == null || bonus == null)
                return;

            target.speed += bonus.speed;
            target.poison_resist += bonus.poison_resist;
            target.electric_resist += bonus.electric_resist;
            target.amber_resist += bonus.amber_resist;
            target.freeze_resist += bonus.freeze_resist;
        }

        private static void MultiplyIfActive(ref float accumulator, float multiplier)
        {
            if (Mathf.Approximately(multiplier, 0f) || Mathf.Approximately(multiplier, 1f))
                return;

            accumulator *= multiplier;
        }
    }
}
