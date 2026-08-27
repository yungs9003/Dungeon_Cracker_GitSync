using System.Collections.Generic;
using UnityEngine;
using Dungeon.Core;

namespace Dungeon.Player
{
    /// <summary>
    /// 플레이어 이동속도를 관리합니다.
    /// 기존 AddBonus 기반 가감속과, 출처별 배율 효과를 함께 지원합니다.
    /// </summary>
    public class SpeedController : MonoBehaviour
    {
        [SerializeField] private float baseSpeed = 5f;

        private float bonus = 0f;
        private readonly Dictionary<string, float> multipliers = new();

        public float CurrentSpeed
        {
            get
            {
                float additiveSpeed = Mathf.Max(0f, baseSpeed + bonus);
                return Mathf.Max(0f, additiveSpeed * GetEquipmentSpeedMultiplier() * GetCombinedMultiplier());
            }
        }

        public void SetBaseSpeed(float speed)
        {
            baseSpeed = Mathf.Max(0f, speed);
        }

        /// <summary>
        /// 기존 슬라임 등의 가산형 속도 효과와 호환됩니다.
        /// </summary>
        public void AddBonus(float delta)
        {
            bonus += delta;
        }

        /// <summary>
        /// 특정 시스템이 사용하는 속도 배율을 등록하거나 갱신합니다.
        /// 예: multiplier=0.7이면 최종 속도가 30% 감소합니다.
        /// 같은 sourceId로 다시 호출하면 중복 적용되지 않고 값만 교체됩니다.
        /// </summary>
        public void SetMultiplier(string sourceId, float multiplier)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return;

            multipliers[sourceId] = Mathf.Max(0f, multiplier);
        }

        /// <summary>
        /// 특정 시스템이 등록했던 속도 배율을 제거합니다.
        /// </summary>
        public void RemoveMultiplier(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return;

            multipliers.Remove(sourceId);
        }

        public bool HasMultiplier(string sourceId)
        {
            return !string.IsNullOrWhiteSpace(sourceId) &&
                   multipliers.ContainsKey(sourceId);
        }

        private static float GetEquipmentSpeedMultiplier()
        {
            StatusSaveData data = StatusSaveFile.Instance != null
                ? StatusSaveFile.Instance.Data
                : null;

            if (data == null || data.base_sub_stats == null || data.sub_stats == null)
                return 1f;

            if (data.base_sub_stats.speed <= 0)
                return 1f;

            return Mathf.Max(0f, (float)data.sub_stats.speed / data.base_sub_stats.speed);
        }

        private float GetCombinedMultiplier()
        {
            float result = 1f;

            foreach (float multiplier in multipliers.Values)
                result *= multiplier;

            return Mathf.Max(0f, result);
        }
    }
}
