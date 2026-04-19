using System.Collections;
using UnityEngine;
using Dungeon.Core;
using Dungeon.Player;

namespace Dungeon.Traps
{
    public class CliffTrap2D : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private LayerMask playerLayer;

        [Header("Tile / Detection")]
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private Vector2 tileCenterOffset = Vector2.zero;
        [SerializeField] private Vector2 triggerBoxSize = new Vector2(0.7f, 0.7f);

        [Header("Fall Motion")]
        [SerializeField] private float pullDuration = 0.12f;
        [SerializeField] private AnimationCurve pullEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Damage")]
        [SerializeField] private int damage = 1;

        [Header("Player Control Lock")]
        [Tooltip("낭떠러지에 빠질 때 잠시 꺼둘 플레이어 조작 스크립트들")]
        [SerializeField] private MonoBehaviour[] controlScriptsToDisable;

        [Header("Visual")]
        [Tooltip("플레이어 하위의 Visual Transform. 비워두면 이름이 Visual인 자식을 찾음")]
        [SerializeField] private string visualChildName = "Visual";

        [Header("Anti Spam")]
        [SerializeField] private float retriggerCooldown = 0.2f;

        private bool isBusy;
        private float nextAllowedTime;

        private void FixedUpdate()
        {
            if (isBusy) return;
            if (Time.time < nextAllowedTime) return;

            Vector2 center = GetTileCenter();
            Collider2D hit = Physics2D.OverlapBox(center, triggerBoxSize, 0f, playerLayer);
            if (hit == null) return;

            Respawn2D respawn = hit.GetComponentInParent<Respawn2D>();
            Rigidbody2D rb = hit.GetComponentInParent<Rigidbody2D>();
            if (respawn == null || rb == null) return;

            StartCoroutine(CoPitFall(respawn, rb));
        }

        private IEnumerator CoPitFall(Respawn2D respawn, Rigidbody2D rb)
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

            // 데미지 + 마지막 안전 지점 복귀
            bool respawned = respawn.ReturnToLastSafePointFromTrap(damage);

            // 혹시 데미지로 죽어서 start point로 복귀해도 외형은 복구
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

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, new Vector3(tileSize, tileSize, 0f));

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, triggerBoxSize);
        }
#endif
    }
}