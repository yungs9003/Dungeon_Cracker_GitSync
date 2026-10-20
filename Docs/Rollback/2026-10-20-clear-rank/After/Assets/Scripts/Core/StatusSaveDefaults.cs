using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dungeon.Core
{
    /// <summary>
    /// 새 게임 세이브의 단일 기본값과 JSON 정규화 규칙입니다.
    /// savefile.json과 코드 fallback이 같은 값을 사용하도록 이 클래스에서 관리합니다.
    /// </summary>
    public static class StatusSaveDefaults
    {
        public const int CurrentSaveVersion = 5;

        public static StatusSaveData Create()
        {
            return new StatusSaveData
            {
                save_version = CurrentSaveVersion,
                user_name = string.Empty,
                gold = 0,
                broken_traps = 0,
                clear_reward_claimed = false,
                last_getting_gold = 0,
                clear_time_seconds = 0,
                hit_count = 0,
                main_stats = CreateMainStats(),
                sub_stats = CreateSubStats(),
                base_main_stats = CreateMainStats(),
                base_sub_stats = CreateSubStats(),
                inventory = new InventoryData
                {
                    item = new List<Dictionary<string, int>>(),
                    equipment = new List<Dictionary<string, int>>(),
                    module = new List<Dictionary<string, int>>()
                },
                equips = new EquipsData
                {
                    item = new List<Dictionary<string, string>>
                    {
                        new() { { "slot1", GameDatabase.NoneKey } },
                        new() { { "slot2", GameDatabase.NoneKey } },
                        new() { { "slot3", GameDatabase.NoneKey } },
                        new() { { "slot4", GameDatabase.NoneKey } },
                        new() { { "slot5", GameDatabase.NoneKey } }
                    },
                    equip = new List<Dictionary<string, string>>
                    {
                        new() { { "head", GameDatabase.NoneKey } },
                        new() { { "body", GameDatabase.NoneKey } },
                        new() { { "legs", GameDatabase.NoneKey } }
                    },
                    module = new List<Dictionary<string, string>>
                    {
                        new() { { "slot1", GameDatabase.NoneKey } },
                        new() { { "slot2", GameDatabase.NoneKey } },
                        new() { { "slot3", GameDatabase.NoneKey } },
                        new() { { "slot4", GameDatabase.NoneKey } },
                        new() { { "slot5", GameDatabase.NoneKey } },
                        new() { { "slot6", GameDatabase.NoneKey } }
                    }
                },
                checkpoint = new Dictionary<string, DungeonCheckpointData> { ["DUNGEON_001"] = new() },
                dungeon_records = new Dictionary<string, DungeonRecordData> { ["DUNGEON_001"] = new() },
                active_run = new ActiveDungeonRunData(),
                dungeon_status = new List<Dictionary<string, string>>(),
                play_time = "0:00",
                play_time_minutes = 0,
                clear_num_info = string.Empty,
                adventure_count = 0,
                achievement = string.Empty,
                player_rank = string.Empty
            };
        }

        public static string Serialize(StatusSaveData data)
        {
            Normalize(data);
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        public static bool TryDeserialize(string json, out StatusSaveData data, out string error)
        {
            data = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON 내용이 비어 있습니다.";
                return false;
            }

            try
            {
                data = JsonConvert.DeserializeObject<StatusSaveData>(
                    DungeonProgressMigration.Upgrade(JObject.Parse(json)).ToString(),
                    new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        ObjectCreationHandling = ObjectCreationHandling.Replace
                    });

                if (data == null)
                {
                    error = "JSON에서 세이브 데이터를 만들 수 없습니다.";
                    return false;
                }

                Normalize(data);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                data = null;
                return false;
            }
        }

        public static bool IsValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                JObject root = JObject.Parse(json);

                // 문법상 {}도 JSON이지만 정상 세이브로 간주하면 백업 복구 기회를 잃습니다.
                // 구형 세이브 호환에 필요한 선택 필드는 허용하고 핵심 컨테이너만 요구합니다.
                if (root["main_stats"] is not JObject ||
                    root["sub_stats"] is not JObject ||
                    root["inventory"] is not JObject ||
                    root["equips"] is not JObject ||
                    root["checkpoint"] is not JObject)
                {
                    return false;
                }

                return TryDeserialize(json, out _, out _);
            }
            catch
            {
                return false;
            }
        }

        public static void Normalize(StatusSaveData data)
        {
            if (data == null)
                return;

            data.save_version = CurrentSaveVersion;
            data.user_name ??= string.Empty;

            data.gold = Math.Max(0, data.gold);
            data.broken_traps = Math.Max(0, data.broken_traps);
            data.last_getting_gold = Math.Max(0, data.last_getting_gold);
            data.clear_time_seconds = Math.Max(0, data.clear_time_seconds);
            data.hit_count = Math.Max(0, data.hit_count);
            data.adventure_count = Math.Max(0, data.adventure_count);

            data.main_stats ??= CreateMainStats();
            data.sub_stats ??= CreateSubStats();
            data.main_stats.max_HP = Math.Max(1, data.main_stats.max_HP);

            // 구형 세이브에 base 값이 없으면 현재 최종 스탯을 기준값으로 승격합니다.
            data.base_main_stats ??= Clone(data.main_stats);
            data.base_sub_stats ??= Clone(data.sub_stats);
            data.base_main_stats.max_HP = Math.Max(1, data.base_main_stats.max_HP);

            data.inventory ??= new InventoryData();
            data.inventory.item ??= new List<Dictionary<string, int>>();
            data.inventory.equipment ??= new List<Dictionary<string, int>>();
            data.inventory.module ??= new List<Dictionary<string, int>>();

            data.equips ??= new EquipsData();
            data.equips.item ??= new List<Dictionary<string, string>>();
            data.equips.equip ??= new List<Dictionary<string, string>>();
            data.equips.module ??= new List<Dictionary<string, string>>();

            EnsureStringSlot(data.equips.item, "slot1");
            EnsureStringSlot(data.equips.item, "slot2");
            EnsureStringSlot(data.equips.item, "slot3");
            EnsureStringSlot(data.equips.item, "slot4");
            EnsureStringSlot(data.equips.item, "slot5");

            EnsureStringSlot(data.equips.equip, "head");
            EnsureStringSlot(data.equips.equip, "body");
            EnsureStringSlot(data.equips.equip, "legs");

            EnsureStringSlot(data.equips.module, "slot1");
            EnsureStringSlot(data.equips.module, "slot2");
            EnsureStringSlot(data.equips.module, "slot3");
            EnsureStringSlot(data.equips.module, "slot4");
            EnsureStringSlot(data.equips.module, "slot5");
            EnsureStringSlot(data.equips.module, "slot6");

            DungeonProgressMigration.Normalize(data);

            data.dungeon_status ??= new List<Dictionary<string, string>>();
            PlayTimeData.Normalize(data);
            data.clear_num_info ??= string.Empty;
            data.achievement ??= string.Empty;
            data.player_rank ??= string.Empty;
        }

        private static MainStats CreateMainStats()
        {
            return new MainStats
            {
                max_HP = 100,
                physical_resist = 0,
                magical_resist = 0
            };
        }

        private static SubStats CreateSubStats()
        {
            return new SubStats
            {
                speed = 10,
                poison_resist = 0,
                electric_resist = 0,
                amber_resist = 0,
                freeze_resist = 0
            };
        }

        private static MainStats Clone(MainStats source)
        {
            source ??= CreateMainStats();
            return new MainStats
            {
                max_HP = source.max_HP,
                physical_resist = source.physical_resist,
                magical_resist = source.magical_resist
            };
        }

        private static SubStats Clone(SubStats source)
        {
            source ??= CreateSubStats();
            return new SubStats
            {
                speed = source.speed,
                poison_resist = source.poison_resist,
                electric_resist = source.electric_resist,
                amber_resist = source.amber_resist,
                freeze_resist = source.freeze_resist
            };
        }

        private static void EnsureStringSlot(
            List<Dictionary<string, string>> list,
            string slotKey)
        {
            Dictionary<string, string> target = null;

            foreach (Dictionary<string, string> map in list)
            {
                if (map == null || !map.ContainsKey(slotKey))
                    continue;

                if (target == null)
                {
                    target = map;
                }
                else
                {
                    map.Remove(slotKey);
                }
            }

            if (target == null)
            {
                list.Add(new Dictionary<string, string>
                {
                    { slotKey, GameDatabase.NoneKey }
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(target[slotKey]))
                target[slotKey] = GameDatabase.NoneKey;
        }
    }
}
