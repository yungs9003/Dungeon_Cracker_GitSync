using UnityEngine;

namespace Dungeon.Core
{
    /// <summary>강제 이동 한 건이 소유하는 보호 상태. 일반 피격 무적 시간과 독립적입니다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerEffectLock2D : MonoBehaviour
    {
        private Rigidbody2D body;
        private Object owner;
        private bool held;
        private bool previousSimulated;

        public bool IsProtected => held;

        public static bool IsProtectedTarget(Component target)
        {
            if (target == null) return false;
            var gate = target.GetComponentInParent<PlayerEffectLock2D>();
            return gate != null && gate.IsProtected;
        }

        public bool TryAcquire(Object requestOwner)
        {
            if (!isActiveAndEnabled || requestOwner == null || held) return false;
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body == null || !body.simulated) return false;

            owner = requestOwner;
            previousSimulated = body.simulated;
            held = true;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            // 이 Rigidbody에 연결된 자식 Collider까지 물리/Trigger/Overlap 대상에서 제외합니다.
            body.simulated = false;
            return true;
        }

        public bool IsOwnedBy(Object requestOwner)
        {
            return held && ReferenceEquals(owner, requestOwner);
        }

        public void Release(Object requestOwner)
        {
            if (IsOwnedBy(requestOwner)) Restore();
        }

        private void LateUpdate()
        {
            // 소유 함정이 예외적으로 먼저 파괴된 경우에도 영구 잠금을 남기지 않습니다.
            if (held && owner == null) Restore();
        }

        private void OnDisable() => Restore();

        private void Restore()
        {
            if (!held) return;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = previousSimulated;
            }
            held = false;
            owner = null;
        }
    }
}
