using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Dungeon.Player;
using Dungeon.Core;

namespace Dungeon.Traps
{
    public class CliffTrap2D : MonoBehaviour
    {
        public enum FallVisualStyle { ShrinkToCenter, LavaSink }
        [Header("Theme (default preserves existing pit)")]
        [SerializeField] private FallVisualStyle fallVisualStyle;
        [SerializeField, Min(.1f)] private float lavaSinkDuration = 2f;
        [Tooltip("최소 하강 거리(월드 단위). 0이면 실제 Visual 높이로 정합니다. 수면은 처음 발밑 높이에 고정됩니다.")]
        [SerializeField, Min(0f)] private float lavaVisualDrop;
        [SerializeField, Min(.4f)] private float lavaDamageInterval = .5f;
        [SerializeField] private Sprite lavaClipSprite;
        private Rigidbody2D lavaBody;
        private bool lavaPreviousSimulated;
        private PlayerController2D lavaController;
        private bool lavaPreviousControl;
        private Transform lavaVisual, lavaVisualParent;
        private int lavaVisualSibling;
        private Vector3 lavaVisualPosition;
        private GameObject lavaGroup;
        private SpriteMask lavaMask;
        private float lavaSurfaceY;
        private SpriteRenderer[] lavaRenderers;
        private SpriteMaskInteraction[] lavaMaskModes;
        private PlayerAnimationDriver2D[] lavaAnimDrivers;
        private bool[] lavaAnimEnabled;
        private const float TileSpacing = 1f;
        private const float NeighborTolerance = 0.05f;
        private const float InternalBoundaryOverlap = 0.01f;
        private const float CandidateDistanceEpsilon = 0.000001f;

        private static readonly HashSet<CliffTrap2D> ActiveTraps = new HashSet<CliffTrap2D>();
        private static readonly HashSet<Rigidbody2D> FallingPlayers = new HashSet<Rigidbody2D>();

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
        private Rigidbody2D lockedPlayer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            ActiveTraps.Clear();
            FallingPlayers.Clear();
        }

        private void Awake()
        {
            if (playerRb == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    playerRb = playerObj.GetComponent<Rigidbody2D>();
            }
        }

        private void OnEnable()
        {
            ActiveTraps.Add(this);
        }

        private void OnDisable()
        {
            ActiveTraps.Remove(this);
            if (lavaBody != null) { StopAllCoroutines(); RestoreLavaState(); isBusy = false; }
            ReleasePlayerFallLock();
        }

        private void FixedUpdate()
        {
            if (isBusy) return;
            if (Time.time < nextAllowedTime) return;
            if (playerRb == null) return;
            if (PlayerEffectLock2D.IsProtectedTarget(playerRb)) return;

            // 플레이어 레이어 체크
            if ((playerLayer.value & (1 << playerRb.gameObject.layer)) == 0)
                return;

            Vector2 playerCenter = playerRb.worldCenterOfMass;

            // 단독 구멍의 기존 중심점 판정은 유지하고,
            // 맞닿은 구멍 방향만 타일 경계까지 확장해 내부의 빈 통로를 없앤다.
            if (!IsInsideDetectionBounds(playerCenter)) return;
            if (!IsPreferredTrapForPlayer(playerCenter, playerRb)) return;

            Respawn2D respawn = playerRb.GetComponent<Respawn2D>();
            if (respawn == null)
                respawn = playerRb.GetComponentInParent<Respawn2D>();

            if (respawn == null) return;

            // 인접한 여러 구멍이 같은 물리 프레임에 감지되어도
            // 한 플레이어에게는 하나의 추락 처리만 실행한다.
            if (!TryAcquirePlayerFallLock(playerRb)) return;

            StartCoroutine(CoCliffFall(respawn, playerRb));
        }

        private IEnumerator CoCliffFall(Respawn2D respawn, Rigidbody2D rb)
        {
            isBusy = true;
            nextAllowedTime = Time.time + retriggerCooldown;

            if (fallVisualStyle == FallVisualStyle.LavaSink)
            {
                yield return CoLavaSink(respawn, rb);
                yield break;
            }

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
            ReleasePlayerFallLock();
        }

        private bool IsInsideDetectionBounds(Vector2 point)
        {
            GetDetectionBounds(out Vector2 min, out Vector2 max);

            return point.x >= min.x && point.x <= max.x &&
                   point.y >= min.y && point.y <= max.y;
        }

        private IEnumerator CoLavaSink(Respawn2D respawn, Rigidbody2D rb)
        {
            lavaBody = rb; lavaPreviousSimulated = rb.simulated;
            lavaController = rb.GetComponent<PlayerController2D>();
            lavaPreviousControl = lavaController != null && lavaController.CanControl;
            lavaController?.SetControl(false, true);
            rb.GetComponent<KnockbackReceiver2D>()?.CancelKnockback();
            rb.GetComponent<PlayerIceMovement2D>()?.ResetMotion();
            rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; rb.simulated = false;
            lavaVisual = FindVisualTransform(respawn.transform);
            if (lavaVisual != null)
            {
                lavaVisualParent = lavaVisual.parent; lavaVisualSibling = lavaVisual.GetSiblingIndex();
                lavaVisualPosition = lavaVisual.localPosition;
                lavaAnimDrivers = lavaVisual.GetComponentsInChildren<PlayerAnimationDriver2D>(true);
                lavaAnimEnabled = new bool[lavaAnimDrivers.Length];
                for (int i = 0; i < lavaAnimDrivers.Length; i++) { lavaAnimEnabled[i] = lavaAnimDrivers[i].enabled; lavaAnimDrivers[i].enabled = false; }
                foreach (Animator animator in lavaVisual.GetComponentsInChildren<Animator>(true))
                    foreach (var parameter in animator.parameters)
                        if (parameter.type == AnimatorControllerParameterType.Bool && (parameter.name == "IsMoving" || parameter.name == "Knockback")) animator.SetBool(parameter.nameHash, false);
                lavaRenderers = lavaVisual.GetComponentsInChildren<SpriteRenderer>(true);
                if (TryGetLavaVisualBounds(out Bounds initialBounds)) lavaSurfaceY = initialBounds.min.y;
                if (lavaClipSprite != null && lavaRenderers.Length > 0)
                {
                    lavaMaskModes = new SpriteMaskInteraction[lavaRenderers.Length];
                    for (int i = 0; i < lavaRenderers.Length; i++) { lavaMaskModes[i] = lavaRenderers[i].maskInteraction; lavaRenderers[i].maskInteraction = SpriteMaskInteraction.VisibleInsideMask; }
                    lavaGroup = new GameObject("LavaSinkVisualGroup");
                    lavaGroup.transform.SetParent(respawn.transform, false);
                    var sorting = lavaGroup.AddComponent<UnityEngine.Rendering.SortingGroup>();
                    sorting.sortingLayerID = lavaRenderers[0].sortingLayerID; sorting.sortingOrder = lavaRenderers[0].sortingOrder;
                    lavaVisual.SetParent(lavaGroup.transform, true);
                    lavaMask = new GameObject("RemainingPlayerVisual").AddComponent<SpriteMask>();
                    lavaMask.transform.SetParent(lavaGroup.transform, false);
                    lavaMask.sprite = lavaClipSprite;
                    UpdateLavaMask();
                }
            }
            Vector3 start = lavaVisual != null ? lavaVisual.localPosition : Vector3.zero;
            // 한 Visual 높이만큼 수면을 통과해야 전신이 잠깁니다. 가림량은 별도 시간 곡선으로 계산하지 않습니다.
            float dropDistance = TryGetLavaVisualBounds(out Bounds startBounds)
                ? Mathf.Max(lavaVisualDrop, startBounds.size.y + .02f) : lavaVisualDrop;
            Vector3 drop = lavaVisual != null && lavaVisual.parent != null
                ? lavaVisual.parent.InverseTransformVector(Vector3.down * dropDistance) : Vector3.down * dropDistance;
            Health health = respawn.GetComponent<Health>();
            ThemeTrapDamage2D damage = GetComponent<ThemeTrapDamage2D>();
            float duration = Mathf.Max(.1f, lavaSinkDuration), t = 0f, nextDamage = Mathf.Max(.4f, lavaDamageInterval);
            int requestedDamage = 0;
            try
            {
                while (t < duration && respawn != null && (health == null || health.currentHP > 0))
                {
                    t = Mathf.Min(duration, t + Time.fixedDeltaTime);
                    float progress = t / duration;
                    if (lavaVisual != null) lavaVisual.localPosition = start + drop * progress;
                    UpdateLavaMask();
                    if (t + .0001f >= nextDamage || t >= duration)
                    {
                        damage?.ApplyBudgetStep(health, t / duration, ref requestedDamage);
                        nextDamage += Mathf.Max(.4f, lavaDamageInterval);
                    }
                    yield return new WaitForFixedUpdate();
                }
                if (respawn != null && (health == null || health.currentHP > 0)) respawn.ReturnToLastSafePointFromTrap();
            }
            finally
            {
                RestoreLavaState(); isBusy = false; ReleasePlayerFallLock();
            }
        }

        private bool TryGetLavaVisualBounds(out Bounds bounds)
        {
            bounds = default;
            if (lavaRenderers == null) return false;
            bool found = false;
            foreach (SpriteRenderer renderer in lavaRenderers)
            {
                if (renderer == null || renderer.sprite == null || !renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private void UpdateLavaMask()
        {
            if (lavaMask == null) return;
            bool found = TryGetLavaVisualBounds(out Bounds bounds);
            float bottom = Mathf.Max(lavaSurfaceY, bounds.min.y);
            float height = Mathf.Max(0f, bounds.max.y - bottom);
            lavaMask.enabled = found && height > .0001f;
            if (!lavaMask.enabled) return;

            // 고정된 수면 아래로 내려간 부분만 가립니다. Visual이 0.1 내려가면 가림 높이도 0.1 증가합니다.
            lavaMask.transform.position = new Vector3(bounds.center.x, bottom + height * .5f, bounds.center.z);
            lavaMask.transform.rotation = Quaternion.identity;
            Vector3 parentScale = lavaMask.transform.parent.lossyScale;
            Vector2 spriteSize = lavaMask.sprite.bounds.size;
            lavaMask.transform.localScale = new Vector3(
                (bounds.size.x + .02f) / Mathf.Max(.0001f, Mathf.Abs(parentScale.x) * spriteSize.x),
                height / Mathf.Max(.0001f, Mathf.Abs(parentScale.y) * spriteSize.y), 1f);
        }

        private void RestoreLavaState()
        {
            if (lavaVisual != null)
            {
                lavaVisual.SetParent(lavaVisualParent, true);
                lavaVisual.SetSiblingIndex(lavaVisualSibling);
                lavaVisual.localPosition = lavaVisualPosition;
            }
            if (lavaRenderers != null && lavaMaskModes != null)
                for (int i = 0; i < lavaRenderers.Length; i++) if (lavaRenderers[i] != null) lavaRenderers[i].maskInteraction = lavaMaskModes[i];
            if (lavaAnimDrivers != null && lavaAnimEnabled != null)
                for (int i = 0; i < lavaAnimDrivers.Length; i++) if (lavaAnimDrivers[i] != null) lavaAnimDrivers[i].enabled = lavaAnimEnabled[i];
            if (lavaGroup != null) { if (Application.isPlaying) Destroy(lavaGroup); else DestroyImmediate(lavaGroup); }
            if (lavaBody != null) { lavaBody.linearVelocity = Vector2.zero; lavaBody.angularVelocity = 0f; lavaBody.simulated = lavaPreviousSimulated; lavaBody.GetComponent<PlayerIceMovement2D>()?.ResetMotion(); }
            lavaController?.SetControl(lavaPreviousControl, true);
            lavaBody = null; lavaController = null; lavaVisual = null; lavaGroup = null; lavaMask = null;
            lavaRenderers = null; lavaMaskModes = null; lavaAnimDrivers = null; lavaAnimEnabled = null;
        }

        private void GetDetectionBounds(out Vector2 min, out Vector2 max)
        {
            Vector2 center = GetTileCenter();
            Vector2 half = triggerBoxSize * 0.5f;

            min = center - half;
            max = center + half;

            float internalHalfSize = TileSpacing * 0.5f + InternalBoundaryOverlap;

            if (HasNeighbor(Vector2.left))
                min.x = Mathf.Min(min.x, center.x - internalHalfSize);

            if (HasNeighbor(Vector2.right))
                max.x = Mathf.Max(max.x, center.x + internalHalfSize);

            if (HasNeighbor(Vector2.down))
                min.y = Mathf.Min(min.y, center.y - internalHalfSize);

            if (HasNeighbor(Vector2.up))
                max.y = Mathf.Max(max.y, center.y + internalHalfSize);
        }

        private bool HasNeighbor(Vector2 direction)
        {
            Vector2 expectedCenter = GetTileCenter() + direction * TileSpacing;

            foreach (CliffTrap2D trap in ActiveTraps)
            {
                if (trap == null || trap == this || !trap.isActiveAndEnabled)
                    continue;

                Vector2 otherCenter = trap.GetTileCenter();
                if (Mathf.Abs(otherCenter.x - expectedCenter.x) <= NeighborTolerance &&
                    Mathf.Abs(otherCenter.y - expectedCenter.y) <= NeighborTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPreferredTrapForPlayer(Vector2 playerCenter, Rigidbody2D rb)
        {
            float ownDistance = (playerCenter - GetTileCenter()).sqrMagnitude;
            int ownInstanceId = GetInstanceID();

            foreach (CliffTrap2D trap in ActiveTraps)
            {
                if (trap == null || trap == this || !trap.isActiveAndEnabled)
                    continue;

                if (trap.playerRb != rb || !trap.IsInsideDetectionBounds(playerCenter))
                    continue;

                float otherDistance = (playerCenter - trap.GetTileCenter()).sqrMagnitude;
                if (otherDistance < ownDistance - CandidateDistanceEpsilon)
                    return false;

                if (Mathf.Abs(otherDistance - ownDistance) <= CandidateDistanceEpsilon &&
                    trap.GetInstanceID() < ownInstanceId)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsPlayerFalling(Rigidbody2D rb) => rb != null && FallingPlayers.Contains(rb);

        private bool TryAcquirePlayerFallLock(Rigidbody2D rb)
        {
            if (PlayerEffectLock2D.IsProtectedTarget(rb)) return false;
            FallingPlayers.RemoveWhere(candidate => candidate == null);

            if (!FallingPlayers.Add(rb))
                return false;

            lockedPlayer = rb;
            return true;
        }

        private void ReleasePlayerFallLock()
        {
            if (ReferenceEquals(lockedPlayer, null))
                return;

            FallingPlayers.Remove(lockedPlayer);
            lockedPlayer = null;
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
            GetDetectionBounds(out Vector2 min, out Vector2 max);
            Vector2 center = (min + max) * 0.5f;
            Vector2 size = max - min;

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, size);
        }
#endif
    }
}
