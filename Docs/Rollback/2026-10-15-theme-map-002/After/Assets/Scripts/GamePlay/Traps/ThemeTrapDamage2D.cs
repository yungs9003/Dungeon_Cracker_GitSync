using Dungeon.Core;
using Dungeon.Player;
using UnityEngine;

namespace Dungeon.Traps
{
    /// <summary>프리팹에 피해 수치를 저장하지 않고 코드로 DB의 함정 레코드를 조회합니다.</summary>
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
            // 2초 동안 DB의 총 원시 피해를 분배하고 타입별 기존 피해 경로를 사용합니다.
            if (amount > 0) TrapDamageEvent.ApplyTypedDamage(target, amount, definition.damage_type);
        }
    }
}
