using System;
using UnityEngine;
using Dungeon.Player;
using Dungeon.Core;

namespace Dungeon.Map
{
    /// <summary>
    /// 현재 맵 전체에 적용되는 field_effect를 관리합니다.
    ///
    /// 역할:
    /// 1. JsonBlockBuilder2D가 전달한 type/damage를 저장합니다.
    /// 2. burn/ice별 피해 주기를 관리합니다.
    /// 3. ice 필드에서는 플레이어 최종 이동속도를 70%로 낮춥니다.
    /// 4. 필드 피해는 DamageSystem의 전용 함수를 통해 적용하여
    ///    Health.CountDirectHit()을 호출하지 않습니다.
    /// </summary>
    public class FieldEffectManager : MonoBehaviour
    {
        public enum FieldEffectType
        {
            None,
            Burn,
            Ice
        }

        private const string IceSpeedModifierId = "MapFieldEffect.Ice";

        [Header("Player References")]
        [SerializeField] private Transform player;
        [SerializeField]
        [Tooltip("DamageSystem 등 IFieldEffectDamageReceiver를 구현한 컴포넌트입니다.")]
        private MonoBehaviour fieldDamageReceiverSource;

        private IFieldEffectDamageReceiver fieldDamageReceiver;
        [SerializeField] private SpeedController speedController;

        [Header("Burn Field")]
        [Tooltip("burn 필드 피해가 반복되는 간격입니다.")]
        [SerializeField, Min(0.05f)] private float burnTickInterval = 1f;

        [Header("Ice Field")]
        [Tooltip("ice 필드 피해가 반복되는 간격입니다.")]
        [SerializeField, Min(0.05f)] private float iceTickInterval = 1f;

        [Tooltip("ice 필드에서 적용할 최종 이동속도 배율입니다. 0.7이면 30% 감소입니다.")]
        [SerializeField, Range(0f, 1f)] private float iceSpeedMultiplier = 0.7f;

        [Header("Debug")]
        [SerializeField] private bool logFieldEffectChanges = true;

        public FieldEffectType CurrentType { get; private set; } = FieldEffectType.None;
        public int CurrentDamage { get; private set; }
        public float CurrentTickInterval => GetTickInterval(CurrentType);

        private float nextDamageTime;

        /// <summary>
        /// 현재 맵이 burn 필드인지 반환합니다.
        /// </summary>
        public bool IsBurnField()
        {
            return CurrentType == FieldEffectType.Burn;
        }

        /// <summary>
        /// 현재 맵이 ice 필드인지 반환합니다.
        /// </summary>
        public bool IsIceField()
        {
            return CurrentType == FieldEffectType.Ice;
        }

        /// <summary>
        /// JsonBlockBuilder2D가 파싱한 맵 필드 효과를 적용합니다.
        /// playerOverride가 전달되면 해당 플레이어를 기준으로 참조를 다시 연결합니다.
        /// </summary>
        public void ApplyMapEffect(
            FieldEffectData2D fieldEffect,
            Transform playerOverride = null)
        {
            ClearFieldEffect();

            if (playerOverride != null)
                player = playerOverride;

            ResolvePlayerReferences();

            if (fieldEffect == null)
                return;

            FieldEffectType parsedType = ParseType(fieldEffect.type);
            if (parsedType == FieldEffectType.None)
            {
                if (!string.IsNullOrWhiteSpace(fieldEffect.type) &&
                    !string.Equals(fieldEffect.type, "none", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(
                        $"[FieldEffectManager] 알 수 없는 field_effect type입니다: {fieldEffect.type}"
                    );
                }

                return;
            }

            CurrentType = parsedType;
            CurrentDamage = Mathf.Max(0, fieldEffect.damage);
            nextDamageTime = Time.time + CurrentTickInterval;

            if (IsIceField())
                ApplyIceSpeedModifier();

            if (logFieldEffectChanges)
            {
                Debug.Log(
                    $"[FieldEffectManager] Field effect applied. " +
                    $"type={CurrentType}, damage={CurrentDamage}, " +
                    $"tick={CurrentTickInterval:0.###}"
                );
            }
        }

        /// <summary>
        /// 현재 필드 효과와 ice 감속을 모두 해제합니다.
        /// </summary>
        public void ClearFieldEffect()
        {
            if (speedController != null)
                speedController.RemoveMultiplier(IceSpeedModifierId);

            CurrentType = FieldEffectType.None;
            CurrentDamage = 0;
            nextDamageTime = 0f;
        }

        private void Update()
        {
            if (CurrentType == FieldEffectType.None || CurrentDamage <= 0)
                return;

            if (Time.time < nextDamageTime)
                return;

            ResolvePlayerReferences();
            ApplyCurrentFieldDamage();
            nextDamageTime = Time.time + CurrentTickInterval;
        }

        private void ApplyCurrentFieldDamage()
        {
            ResolveFieldDamageReceiver();

            if (fieldDamageReceiver == null)
                return;

            switch (CurrentType)
            {
                case FieldEffectType.Burn:
                    fieldDamageReceiver.ApplyBurnFieldDamage(
                        CurrentDamage
                    );
                    break;

                case FieldEffectType.Ice:
                    fieldDamageReceiver.ApplyIceFieldDamage(
                        CurrentDamage
                    );
                    break;
            }
        }

        private void ResolveFieldDamageReceiver()
        {
            if (fieldDamageReceiver != null)
                return;

            // 인스펙터에 직접 연결된 컴포넌트를 우선 사용합니다.
            if (fieldDamageReceiverSource != null)
            {
                fieldDamageReceiver =
                    fieldDamageReceiverSource
                        as IFieldEffectDamageReceiver;

                if (fieldDamageReceiver != null)
                    return;
            }

            if (player == null)
                return;

            MonoBehaviour[] behaviours =
                player.GetComponentsInChildren<MonoBehaviour>(
                    true
                );

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IFieldEffectDamageReceiver receiver)
                {
                    fieldDamageReceiverSource = behaviour;
                    fieldDamageReceiver = receiver;
                    return;
                }
            }

            MonoBehaviour[] parentBehaviours =
                player.GetComponentsInParent<MonoBehaviour>(
                    true
                );

            foreach (MonoBehaviour behaviour in parentBehaviours)
            {
                if (behaviour is IFieldEffectDamageReceiver receiver)
                {
                    fieldDamageReceiverSource = behaviour;
                    fieldDamageReceiver = receiver;
                    return;
                }
            }
        }
    
        private void ApplyIceSpeedModifier()
        {
            if (speedController == null)
            {
                Debug.LogWarning(
                    "[FieldEffectManager] SpeedController가 없어 ice 감속을 적용하지 못했습니다."
                );
                return;
            }

            speedController.SetMultiplier(
                IceSpeedModifierId,
                Mathf.Clamp01(iceSpeedMultiplier)
            );
        }

        private void ResolvePlayerReferences()
        {
            // PlayerController2D의 구체적인 구현에 의존하지 않고,
            // 현재 플레이어가 이미 사용하는 SpeedController/DamageSystem을 기준으로 찾습니다.
            if (player == null && speedController == null)
                speedController = FindFirstObjectByType<SpeedController>();

            if (player == null && speedController != null)
                player = speedController.transform;

            if (player == null && fieldDamageReceiver == null)
                ResolveFieldDamageReceiver();

            if (player == null && fieldDamageReceiver != null)
                player = fieldDamageReceiverSource.transform;

            if (player == null)
                return;

            if (speedController == null)
            {
                speedController = player.GetComponent<SpeedController>();

                if (speedController == null)
                    speedController = player.GetComponentInChildren<SpeedController>(true);

                if (speedController == null)
                    speedController = player.GetComponentInParent<SpeedController>();
            }
        }
    

        private float GetTickInterval(FieldEffectType type)
        {
            return type switch
            {
                FieldEffectType.Burn => Mathf.Max(0.05f, burnTickInterval),
                FieldEffectType.Ice => Mathf.Max(0.05f, iceTickInterval),
                _ => 0f
            };
        }

        public static FieldEffectType ParseType(string rawType)
        {
            if (string.IsNullOrWhiteSpace(rawType))
                return FieldEffectType.None;

            return rawType.Trim().ToLowerInvariant() switch
            {
                "burn" => FieldEffectType.Burn,
                "fire" => FieldEffectType.Burn,
                "ice" => FieldEffectType.Ice,
                "none" => FieldEffectType.None,
                _ => FieldEffectType.None
            };
        }

        private void OnDisable()
        {
            ClearFieldEffect();
        }

        private void OnDestroy()
        {
            ClearFieldEffect();
        }
    }
}
