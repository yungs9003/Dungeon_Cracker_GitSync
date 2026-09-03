using System.Collections;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Traps
{
    public class CliffTrap2D : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private Rigidbody2D playerRb;

        [Header("Tile / Detection")]
        [SerializeField] private Vector2 tileCenterOffset = Vector2.zero;
        [SerializeField] private Vector2 triggerBoxSize = new Vector2(0.7f, 0.7f);

        [Header("Fall Motion")]
        [SerializeField] private float pullDuration = 0.12f;
        [SerializeField] private AnimationCurve pullEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Player Control Lock")]
        [Tooltip("낭떠러지에 빠질 때 잠시 꺼둘 플레이어 조작 스크립트들")]
        [SerializeField] private MonoBehaviour[] controlScriptsToDisable;

        [Header("Visual")]
        [Tooltip("플레이어 하위의 Visual Transform. 비워두면 이름이 Visual인 자식을 찾음")]
        [SerializeField] private string visualChildName = "Visual";

        [Header("Anti Spam")]
        [SerializeField] private float retriggerCooldown = 0.25f;

        private bool isBusy;
        private float nextAllowedTime;

        private void Awake()
        {
            if (playerRb == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    playerRb = playerObj.GetComponent<Rigidbody2D>();
            }
        }

        private void FixedUpdate()
        {
            if (isBusy) return;
            if (Time.time < nextAllowedTime) return;
            if (playerRb == null) return;

            // 플레이어 레이어 체크
            if ((playerLayer.value & (1 << playerRb.gameObject.layer)) == 0)
                return;

            Vector2 cliffCenter = GetTileCenter();
            Vector2 playerCenter = playerRb.worldCenterOfMass;

            Vector2 delta = playerCenter - cliffCenter;

            // ✅ 플레이어 "중심점"이 0.7 x 0.7 범위 안에 들어왔을 때만 발동
            bool inside =
                Mathf.Abs(delta.x) <= triggerBoxSize.x * 0.5f &&
                Mathf.Abs(delta.y) <= triggerBoxSize.y * 0.5f;

            if (!inside) return;

            Respawn2D respawn = playerRb.GetComponent<Respawn2D>();
            if (respawn == null)
                respawn = playerRb.GetComponentInParent<Respawn2D>();

            if (respawn == null) return;

            StartCoroutine(CoCliffFall(respawn, playerRb));
        }

        private IEnumerator CoCliffFall(Respawn2D respawn, Rigidbody2D rb)
        {
            isBusy = true;
            nextAllowedTime = Time.time + retriggerCooldown;

            GameObject player = respawn.gameObject;
            Transform playerTr = player.transform;
            Transform visual = FindVisualTransform(playerTr);

            Vector3 visualStartScale = visual != null ? visual.localScale : Vector3.one;
            Vector2 startPos = rb.position;
            Vector2 endPos = GetTileCenter();

            SetControlsEnabled(false);

            // 이동 중 외력 제거
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            var kb = player.GetComponent<KnockbackReceiver2D>();
            if (kb != null)
                kb.CancelKnockback();

            float t = 0f;
            while (t < pullDuration)
            {
                t += Time.fixedDeltaTime;
                float u = Mathf.Clamp01(t / pullDuration);
                float e = pullEase != null ? pullEase.Evaluate(u) : u;

                Vector2 pos = Vector2.Lerp(startPos, endPos, e);
                rb.position = pos;
                playerTr.position = new Vector3(pos.x, pos.y, playerTr.position.z);

                if (visual != null)
                    visual.localScale = Vector3.LerpUnclamped(visualStartScale, Vector3.zero, e);

                yield return new WaitForFixedUpdate();
            }

            rb.position = endPos;
            playerTr.position = new Vector3(endPos.x, endPos.y, playerTr.position.z);

            if (visual != null)
                visual.localScale = Vector3.zero;

            Health health = respawn.GetComponent<Health>();
            TrapDamageEvent.Raise(gameObject, health);

            // 피해로 죽으면 Health.onDied가 씬/체크포인트 복귀를 처리합니다.
            if (health == null || health.currentHP > 0)
                respawn.ReturnToLastSafePointFromTrap();

            yield return null;

            if (visual != null)
                visual.localScale = visualStartScale;

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            SetControlsEnabled(true);
            isBusy = false;
        }

        private Vector2 GetTileCenter()
        {
            return (Vector2)transform.position + tileCenterOffset;
        }

        private Transform FindVisualTransform(Transform root)
        {
            Transform found = root.Find(visualChildName);
            if (found != null) return found;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == visualChildName)
                    return child;
            }

            return null;
        }

        private void SetControlsEnabled(bool enabled)
        {
            if (controlScriptsToDisable == null) return;

            for (int i = 0; i < controlScriptsToDisable.Length; i++)
            {
                if (controlScriptsToDisable[i] != null)
                    controlScriptsToDisable[i].enabled = enabled;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 center = GetTileCenter();

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, triggerBoxSize);
        }
#endif
    }
}
