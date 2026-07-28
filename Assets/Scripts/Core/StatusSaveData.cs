using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Dungeon.Core
{
    [Serializable]
    public class StatusSaveData
    {
        [JsonProperty("user_name")]
        public string user_name = "Toluse";

        [JsonProperty("gold")]
        public int gold = 0;

        [JsonProperty("broken_traps")]
        public int broken_traps = 0;

        [JsonProperty("clear_reward_claimed")]
        public bool clear_reward_claimed = false;

        [JsonProperty("last_getting_gold")]
        public int last_getting_gold = 0;

        [JsonProperty("clear_time_seconds")]
        public float clear_time_seconds = 0f;

        [JsonProperty("hit_count")]
        public int hit_count = 0;

        [JsonProperty("main_stats")]
        public MainStats main_stats = new();

        [JsonProperty("sub_stats")]
        public SubStats sub_stats = new();

        // savefile(1).json 기준의 보유 인벤토리입니다.
        // item      : 소비 아이템/일반 아이템 보유 수량
        // equipment : 장비 아이템 보유 수량
        [JsonProperty("inventory")]
        public InventoryData inventory = new();

        // savefile(1).json 기준의 장착/퀵슬롯 정보입니다.
        // item  : 퀵슬롯에 등록된 아이템
        // equip : head/body/legs 장착 장비
        [JsonProperty("equips")]
        public EquipsData equips = new();

        // 기존 코드 호환용입니다.
        // JSON에는 최상위 item/equip으로 저장하지 않고 inventory 내부 값만 사용합니다.
        [JsonIgnore]
        public List<Dictionary<string, int>> item
        {
            get
            {
                inventory ??= new InventoryData();
                inventory.item ??= new List<Dictionary<string, int>>();
                return inventory.item;
            }
            set
            {
                inventory ??= new InventoryData();
                inventory.item = value ?? new List<Dictionary<string, int>>();
            }
        }

        [JsonIgnore]
        public List<Dictionary<string, int>> equip
        {
            get
            {
                inventory ??= new InventoryData();
                inventory.equipment ??= new List<Dictionary<string, int>>();
                return inventory.equipment;
            }
            set
            {
                inventory ??= new InventoryData();
                inventory.equipment = value ?? new List<Dictionary<string, int>>();
            }
        }

        [JsonProperty("checkpoint")]
        public CheckpointData checkpoint = new();

        [JsonProperty("dungeon_status")]
        public List<Dictionary<string, string>> dungeon_status = new();

        [JsonProperty("play_time")]
        public string play_time = "";

        [JsonProperty("clear_num_info")]
        public string clear_num_info = "";

        [JsonProperty("adventure_count")]
        public int adventure_count = 0;

        [JsonProperty("achievement")]
        public string achievement = "";

        [JsonProperty("player_rank")]
        public string player_rank = "";
    }

    [Serializable]
    public class InventoryData
    {
        [JsonProperty("item")]
        public List<Dictionary<string, int>> item = new();

        [JsonProperty("equipment")]
        public List<Dictionary<string, int>> equipment = new();
    }

    [Serializable]
    public class EquipsData
    {
        [JsonProperty("item")]
        public List<Dictionary<string, string>> item = new();

        [JsonProperty("equip")]
        public List<Dictionary<string, string>> equip = new();
    }

    [Serializable]
    public class MainStats
    {
        [JsonProperty("max_HP")]
        public int max_HP = 650;

        [JsonProperty("physical_resist")]
        public int physical_resist = 10;

        [JsonProperty("magical_resist")]
        public int magical_resist = 20;
    }

    [Serializable]
    public class SubStats
    {
        [JsonProperty("speed")]
        public int speed = 10;

        [JsonProperty("poison_resist")]
        public int poison_resist = 0;

        [JsonProperty("electric_resist")]
        public int electric_resist = 0;

        [JsonProperty("amber_resist")]
        public int amber_resist = 0;

        [JsonProperty("freeze_resist")]
        public int freeze_resist = 0;
    }

    [Serializable]
    public class CheckpointData
    {
        [JsonProperty("sceneName")]
        public string sceneName = "";

        [JsonProperty("mapIndex")]
        public int mapIndex = -1;

        [JsonProperty("resumeRequested")]
        public bool resumeRequested = false;

        [JsonProperty("broken_traps_snapshot")]
        public int broken_traps_snapshot = 0;
    }
}
