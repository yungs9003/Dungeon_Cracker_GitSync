using System;
using System.Collections.Generic;
using Dungeon.Core;
using Dungeon.Player;
using UnityEngine;

namespace Dungeon.Traps
{
    /// <summary>
    /// 함정 코드가 발생시키는 공통 피격 이벤트입니다.
    /// 개별 함정은 피해 수치를 갖지 않으며, 이 이벤트가 db.json의 traps 레코드를 조회해
    /// 피해 타입/피해량/넉백 거리를 플레이어에게 적용합니다.
    /// </summary>
    public static class TrapDamageEvent
    {
        public const float DefaultKnockbackDuration = 0.12f;

        public static event Action<TrapDamageRequest> Raised;

        private static readonly HashSet<string> warnedKeys = new();

        public static bool Raise(GameObject source, Health target)
        {
            return Raise(ResolveTrapCode(source), source, target);
        }

        public static bool Raise(string trapCode, GameObject source, Health target)
        {
            if (PlayerEffectLock2D.IsProtectedTarget(target)) return false;
            trapCode = GameDatabase.NormalizeTrapCode(trapCode);

            var request = new TrapDamageRequest(trapCode, source, target);
            Raised?.Invoke(request);

            if (target == null)
                return false;

            if (string.IsNullOrEmpty(trapCode))
            {
                WarnOnce("missing-code", "[TrapDamageEvent] 함정 프리팹 코드를 찾지 못해 피해 이벤트를 적용하지 않았습니다.");
                return false;
            }

            GameDatabase database = GameDatabase.Instance;
            if (database == null)
            {
                WarnOnce("missing-database", "[TrapDamageEvent] GameDatabase.Instance가 없어 함정 피해를 조회하지 못했습니다.");
                return false;
            }

            if (!database.TryGetTrap(trapCode, out TrapDefinition definition) || definition == null)
            {
                WarnOnce($"unknown:{trapCode}", $"[TrapDamageEvent] db.json traps에서 함정 코드 '{trapCode}'를 찾지 못했습니다.");
                return false;
            }

            if (definition.damage.HasValue && definition.damage.Value > 0)
                ApplyTypedDamage(target, definition.damage.Value, definition.damage_type);

            if (definition.knockback_distance.HasValue && definition.knockback_distance.Value > 0f)
                ApplyReverseMovementKnockback(target, definition.knockback_distance.Value);

            return true;
        }

        /// <summary>단발 및 용암 분할 피해가 같은 DB 피해 타입 규칙을 사용합니다.</summary>
        public static void ApplyTypedDamage(Health target, int amount, string rawType)
        {
            if (target == null || amount <= 0 || PlayerEffectLock2D.IsProtectedTarget(target)) return;
            string type = rawType?.Trim().ToLowerInvariant();
            bool isBurn = type == "burn" || type == "화상";
            bool isIce = type == "ice" || type == "냉기";
            if (!isBurn && !isIce)
            {
                target.TakeDamage(amount, ParseDamageType(rawType));
                return;
            }

            // 기존 필드의 화상/냉기 피해 및 장비 처리 경로를 재사용합니다.
            var receiver = target.GetComponentInParent(typeof(IFieldEffectDamageReceiver)) as IFieldEffectDamageReceiver;
            if (receiver == null) target.ApplyDamageDirect(amount);
            else if (isBurn) receiver.ApplyBurnFieldDamage(amount);
            else receiver.ApplyIceFieldDamage(amount);
        }

        public static string ResolveTrapCode(GameObject source)
        {
            Transform current = source != null ? source.transform : null;

            while (current != null)
            {
                string code = ExtractTrapCode(current.name);
                if (!string.IsNullOrEmpty(code))
                    return code;

                current = current.parent;
            }

            return string.Empty;
        }

        public static string ExtractTrapCode(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return string.Empty;

            string value = objectName.Trim();
            for (int start = 0; start <= value.Length - 7; start++)
            {
                if (value[start + 3] != '-')
                    continue;

                bool isCode = true;
                for (int i = 0; i < 7; i++)
                {
                    if (i == 3)
                        continue;

                    if (!char.IsDigit(value[start + i]))
                    {
                        isCode = false;
                        break;
                    }
                }

                if (isCode)
                    return GameDatabase.NormalizeTrapCode(value.Substring(start, 7));
            }

            return string.Empty;
        }

        private static Dungeon.Core.DamageType ParseDamageType(string rawType)
        {
            switch (rawType?.Trim().ToLowerInvariant())
            {
                case "magic":
                case "magical":
                    return Dungeon.Core.DamageType.Magical;
                case "true":
                    return Dungeon.Core.DamageType.True;
                default:
                    return Dungeon.Core.DamageType.Physical;
            }
        }

        private static void ApplyReverseMovementKnockback(Health target, float distance)
        {
            KnockbackReceiver2D knockback2D = target.GetComponentInParent<KnockbackReceiver2D>();
            if (knockback2D != null)
            {
                PlayerMoveDirectionTracker2D tracker2D = target.GetComponentInParent<PlayerMoveDirectionTracker2D>();
                if (tracker2D != null && tracker2D.LastMoveDir.sqrMagnitude > 0.000001f)
                    knockback2D.AddKnockback(-tracker2D.LastMoveDir, distance, DefaultKnockbackDuration);

                return;
            }

            KnockbackReceiver knockback3D = target.GetComponentInParent<KnockbackReceiver>();
            if (knockback3D == null)
                return;

            PlayerMoveDirectionTracker tracker3D = target.GetComponentInParent<PlayerMoveDirectionTracker>();
            if (tracker3D != null && tracker3D.LastMoveDir.sqrMagnitude > 0.000001f)
                knockback3D.AddKnockback(-tracker3D.LastMoveDir, distance, DefaultKnockbackDuration);
        }

        private static void WarnOnce(string key, string message)
        {
            if (warnedKeys.Add(key))
                Debug.LogWarning(message);
        }
    }

    public readonly struct TrapDamageRequest
    {
        public TrapDamageRequest(string trapCode, GameObject source, Health target)
        {
            TrapCode = trapCode;
            Source = source;
            Target = target;
        }

        public string TrapCode { get; }
        public GameObject Source { get; }
        public Health Target { get; }
    }
}
