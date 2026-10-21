using System;
using System.Collections.Generic;
using System.Linq;
using Dungeon.Core;
using UnityEngine;

namespace Dungeon.UI.Guild
{
    public enum GuildAchievementState { Locked, Verifiable, Certified }

    [Serializable]
    public sealed class GuildAchievementDefinition
    {
        public string id;
        [TextArea(1, 3)] public string condition;
        public string rewardLabel = "600G";
        [Tooltip("SaveData 연결 전 화면 시연용 상태입니다.")]
        public GuildAchievementState previewState;
    }

    [Serializable]
    public sealed class GuildDungeonDefinition
    {
        [Tooltip("향후 저장 기록과 연결할 고정 ID. 표시 번호나 이름과 분리합니다.")]
        public string id;
        public int caseNumber;
        public string dungeonName;
        public GuildAchievementDefinition[] achievements = Array.Empty<GuildAchievementDefinition>();
    }

    [CreateAssetMenu(menuName = "Dungeon/UI/Guild Catalog")]
    public sealed class GuildCatalog : ScriptableObject
    {
        [TextArea(2, 5)] public string[] comments = Array.Empty<string>();
        public string initialDungeonId;
        public GuildDungeonDefinition[] dungeons = Array.Empty<GuildDungeonDefinition>();

        public GuildDungeonDefinition FindDungeon(string id)
        {
            foreach (var dungeon in dungeons)
                if (dungeon != null && string.Equals(dungeon.id, id, StringComparison.Ordinal)) return dungeon;
            return null;
        }

        /// <summary>시연용 에셋을 수정하지 않고 실제 DB의 던전/기준값으로 런타임 목록을 만듭니다.</summary>
        public static GuildCatalog FromDatabase(GuildCatalog artwork)
        {
            var result = CreateInstance<GuildCatalog>();
            result.comments = artwork != null ? artwork.comments : Array.Empty<string>();
            result.initialDungeonId = artwork != null ? artwork.initialDungeonId : "";
            var dungeons = new List<GuildDungeonDefinition>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dungeon in GameDatabase.GetDungeonDefinitions().Where(d => d != null).OrderBy(d => d.number))
            {
                string scene = DungeonProgressService.SceneKey(dungeon.sceneName);
                if (string.IsNullOrWhiteSpace(scene) || !seen.Add(scene)) continue;
                var achievements = new List<GuildAchievementDefinition>();
                foreach (DungeonAchievementKind kind in Enum.GetValues(typeof(DungeonAchievementKind)))
                    achievements.Add(new GuildAchievementDefinition
                    {
                        id = DungeonAchievementService.Id(kind), rewardLabel = $"{dungeon.achievement_reward_gold}G",
                        condition = kind switch
                        {
                            DungeonAchievementKind.Hits => $"피격 {dungeon.rank_reference_hit_count}회 이하로 클리어",
                            DungeonAchievementKind.Time => $"{DungeonTime.Format(dungeon.rank_reference_time_seconds)} 이내에 클리어",
                            _ => dungeon.destructible_trap_count < 0 ? "파괴 가능 함정 집계 필요" :
                                $"함정 60% 이상 해체 ({DungeonAchievementService.RequiredDisarms(dungeon.destructible_trap_count)}/{dungeon.destructible_trap_count}개)"
                        }
                    });
                dungeons.Add(new GuildDungeonDefinition
                {
                    id = scene, caseNumber = dungeon.number, dungeonName = dungeon.name,
                    achievements = achievements.ToArray()
                });
            }
            result.dungeons = dungeons.ToArray();
            return result;
        }
    }
}
