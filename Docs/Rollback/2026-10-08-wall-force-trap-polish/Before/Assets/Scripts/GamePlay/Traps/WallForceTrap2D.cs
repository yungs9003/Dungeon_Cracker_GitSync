using Dungeon.Core;
using Dungeon.Player;
using UnityEngine;

namespace Dungeon.Traps
{
    /// <summary>벽 중심에 배치. 루트의 로컬 +Y가 전방이며, 감지는 플레이어 몸 중심 기준입니다.</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class WallForceTrap2D : MonoBehaviour, ITrapToggle, IPlayerDestructionGate
    {
        public enum ForceMode { Push, Pull }
        private enum Phase { Idle, Reaching, Moving, Retracting, Cooldown, WaitingForExit }

        [Header("Grid / Direction")]
        [SerializeField] private ForceMode mode;
        [SerializeField, Min(0.01f)] private float cellSize = 1f;
        [SerializeField, Min(1)] private int detectionCells = 2;
        [SerializeField, Min(0.01f)] private float laneWidthCells = 1f;
        [SerializeField, Min(0f)] private float pushDistanceCells = 3f;
        [SerializeField, Min(0.5f)] private float pullDestinationCell = 1f;

        [Header("Motion")]
        [SerializeField, Min(0.01f)] private float reachTime = 0.12f;
        [SerializeField, Min(0.01f)] private float moveTime = 0.35f;
        [SerializeField, Min(0.01f)] private float retractTime = 0.18f;
        [SerializeField, Min(0f)] private float cooldown = 0.8f;
        [SerializeField, Range(0.01f, 0.3f)] private float idleTipCells = 0.12f;

        [Header("References")]
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private Transform movingTip;
        [SerializeField] private Transform frontWindow;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private bool startEnabled = true;

        private Phase phase;
        private float elapsed;
        private bool trapEnabled;
        private ForcedMovementReceiver2D receiver;
        private Vector2 startPosition;
        private Vector2 endPosition;
        private Vector3 tipStart;
        private Vector3 tipContact;
        private bool rearmAfterExit;

        public ForceMode Mode => mode;
        public int DetectionCells => detectionCells;
        public bool IsRunning => phase == Phase.Reaching || phase == Phase.Moving || phase == Phase.Retracting;
        public bool CanBeDestroyedByPlayer() => false;

        private void Awake()
        {
            trapEnabled = startEnabled;
            ResetTip();
        }

        private void FixedUpdate() => Tick(Time.fixedDeltaTime);

        private void Tick(float dt)
        {
            if (!trapEnabled) return;
            ResolvePlayer();
            if (phase == Phase.Idle)
            {
                if (playerBody == null || !IsInsideDetection(playerBody.position)) return;
                TryStart();
                return;
            }
            if (phase == Phase.WaitingForExit)
            {
                if (playerBody == null || !IsInsideDetection(playerBody.position)) phase = Phase.Idle;
                return;
            }
            if (phase == Phase.Cooldown)
            {
                elapsed += dt;
                if (elapsed >= cooldown) { elapsed = 0f; phase = Phase.WaitingForExit; }
                return;
            }
            if (receiver == null || !PlayerEffectLock2D.IsProtectedTarget(receiver))
            {
                CancelCycle();
                return;
            }

            elapsed += dt;
            switch (phase)
            {
                case Phase.Reaching:
                    SetTip(Vector3.Lerp(tipStart, tipContact, Ease(elapsed / reachTime)));
                    if (elapsed >= reachTime) { phase = Phase.Moving; elapsed = 0f; }
                    break;
                case Phase.Moving:
                    Vector2 next = Vector2.Lerp(startPosition, endPosition, Ease(elapsed / moveTime));
                    bool reached = receiver.MoveTo(this, next, obstacleMask);
                    SetTipContact(playerBody.position);
                    if (!reached || elapsed >= moveTime)
                    {
                        tipContact = movingTip != null ? movingTip.localPosition : Vector3.zero;
                        phase = Phase.Retracting; elapsed = 0f;
                    }
                    break;
                case Phase.Retracting:
                    SetTip(Vector3.Lerp(tipContact, tipStart, Ease(elapsed / retractTime)));
                    if (elapsed >= retractTime)
                    {
                        receiver.End(this);
                        receiver = null;
                        phase = Phase.Cooldown; elapsed = 0f;
                    }
                    break;
            }
        }

        public bool IsInsideDetection(Vector2 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            float y = local.y / cellSize;
            float x = local.x / cellSize;
            // 90도 회전/큰 맵 좌표의 부동소수 오차로 공유 경계가 옆 칸으로 번지지 않게 합니다.
            const float edgeTolerance = 0.0001f;
            return Mathf.Abs(x) < laneWidthCells * 0.5f - edgeTolerance &&
                y >= 0.5f - edgeTolerance && y < detectionCells + 0.5f - edgeTolerance;
        }

        public Vector2 GetDestination(Vector2 activationPosition)
        {
            if (mode == ForceMode.Push)
                return activationPosition + (Vector2)transform.TransformVector(Vector3.up * (pushDistanceCells * cellSize));
            return transform.TransformPoint(new Vector3(0f, pullDestinationCell * cellSize, 0f));
        }

        private void TryStart()
        {
            if (PlayerEffectLock2D.IsProtectedTarget(playerBody) || CliffTrap2D.IsPlayerFalling(playerBody)) return;
            receiver = playerBody.GetComponent<ForcedMovementReceiver2D>();
            if (receiver == null) receiver = playerBody.gameObject.AddComponent<ForcedMovementReceiver2D>();
            startPosition = playerBody.position;
            if (!receiver.TryBegin(this, GetDestination(startPosition), obstacleMask, out endPosition))
            {
                receiver = null;
                return;
            }
            tipStart = new Vector3(0f, (0.5f + idleTipCells) * cellSize, 0f);
            tipContact = transform.InverseTransformPoint(startPosition);
            tipContact.z = 0f;
            phase = Phase.Reaching;
            elapsed = 0f;
            rearmAfterExit = true;
        }

        private void ResolvePlayer()
        {
            if (playerBody != null) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerBody = player.GetComponent<Rigidbody2D>();
        }

        private static float Ease(float value)
        {
            float u = Mathf.Clamp01(value);
            return u * u * (3f - 2f * u);
        }

        private void SetTipContact(Vector2 position)
        {
            Vector3 local = transform.InverseTransformPoint(position);
            local.z = 0f;
            SetTip(local);
        }

        private void SetTip(Vector3 localPosition)
        {
            if (movingTip != null) movingTip.localPosition = localPosition;
        }

        private void ResetTip()
        {
            tipStart = new Vector3(0f, (0.5f + idleTipCells) * cellSize, 0f);
            SetTip(tipStart);
            // 마스크는 벽 앞쪽 반평면만 노출합니다. SortingGroup으로 다른 프리팹 마스크와 분리합니다.
            if (frontWindow != null)
            {
                float length = (detectionCells + pushDistanceCells + 1f) * cellSize;
                frontWindow.localPosition = new Vector3(0f, 0.5f * cellSize + length * 0.5f, 0f);
                frontWindow.localScale = new Vector3(laneWidthCells * cellSize + 0.5f, length, 1f);
            }
        }

        public void SetTrapEnabled(bool enabled)
        {
            trapEnabled = enabled;
            if (!enabled) CancelCycle();
        }

        private void OnDisable() => CancelCycle();

        private void CancelCycle()
        {
            if (receiver != null) receiver.End(this);
            receiver = null;
            phase = rearmAfterExit ? Phase.WaitingForExit : Phase.Idle;
            elapsed = 0f;
            ResetTip();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            cellSize = Mathf.Max(0.01f, cellSize);
            reachTime = Mathf.Max(0.01f, reachTime);
            moveTime = Mathf.Max(0.01f, moveTime);
            retractTime = Mathf.Max(0.01f, retractTime);
            if (!Application.isPlaying) ResetTip();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = mode == ForceMode.Push ? Color.cyan : new Color(1f, 0.6f, 0f);
            Gizmos.DrawWireCube(new Vector3(0f, (detectionCells + 1) * 0.5f * cellSize, 0f),
                new Vector3(laneWidthCells * cellSize, detectionCells * cellSize, 0f));
        }
#endif
    }
}
