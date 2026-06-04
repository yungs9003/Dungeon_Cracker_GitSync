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

        [JsonProperty("main_stats")]
        public MainStats main_stats = new();

        [JsonProperty("sub_stats")]
        public SubStats sub_stats = new();

        [JsonProperty("item")]
        public List<Dictionary<string, int>> item = new();

        [JsonProperty("equip")]
        public List<Dictionary<string, int>> equip = new();

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
    }
}