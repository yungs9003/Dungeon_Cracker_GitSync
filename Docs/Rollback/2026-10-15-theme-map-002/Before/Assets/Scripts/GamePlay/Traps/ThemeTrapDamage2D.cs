using Dungeon.Core;
using Dungeon.Player;
using UnityEngine;

namespace Dungeon.Traps
{
    /// <summary>임시 프리팹에는 피해 수치를 저장하지 않습니다. 코드/DB 등록 후 연결됩니다.</summary>
    [DisallowMultipleComponent]
    public sealed class ThemeTrapDamage2D : MonoBehaviour
    {
        [Tooltip("미등록 상태는 비워 둡니다. 코드 부여 후 직접 지정하거나 루트 이름의 000-000 코드를 사용합니다.")]
        [SerializeField] private string trapCode;
        public string Code => string.IsNullOrWhiteSpace(trapCode)
            ? TrapDamageEvent.ResolveTrapCode(gameObject) : GameDatabase.NormalizeTrapCode(trapCode);

        public bool TryGetDefinition(out TrapDefinition definition)
        {
            definition = null;
            return !string.IsNullOrEmpty(Code) && GameDatabase.Instance != null &&
                GameDatabase.Instance.TryGetTrap(Code, out definition) && definition != null;
        }

        public bool Hit(Health target)
        {
            if (target == null || PlayerEffectLock2D.IsProtectedTarget(target) || !TryGetDefinition(out _)) return false;
            return TrapDamageEvent.Raise(Code, gameObject, target);
        }

        public void ApplyBudgetStep(Health target, float progress, ref int requestedDamage)
        {
            if (target == null || PlayerEffectLock2D.IsProtectedTarget(target) || !TryGetDefinition(out var definition)) return;
            int cumulative = Mathf.FloorToInt(Mathf.Max(0, definition.damage ?? 0) * Mathf.Clamp01(progress) + .0001f);
            int amount = Mathf.Max(0, cumulative - requestedDamage);
            requestedDamage = cumulative;
            // 2초 동안 DB의 총 원시 피해를 분배합니다. 저항/일반 피격 무적은 기존 Health 규칙을 따릅니다.
            if (amount > 0) target.TakeDamage(amount, ParseType(definition.damage_type));
        }

        private static Dungeon.Core.DamageType ParseType(string value)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "magic": case "magical": return Dungeon.Core.DamageType.Magical;
                case "true": return Dungeon.Core.DamageType.True;
                default: return Dungeon.Core.DamageType.Physical;
            }
        }
    }
}
