using UnityEngine;
using Dungeon.Core;

namespace Dungeon.Traps
{
    public class BallLauncher2D : MonoBehaviour, ITrapToggle
    {
        [Header("Ball Prefab")]
        [SerializeField] private RollingBallTrap2D ballPrefab;

        [Header("Spawn Offset (local XY)")]
        [SerializeField] private Vector2 spawnLocalOffset = new Vector2(0f, 0.6f);

        [Header("Grid")]
        [SerializeField] private float cellSize = 1f;

        [Header("Stats")]
        [SerializeField] private float speed = 6f;
        [SerializeField] private float spawnCooldown = 2f;
        [SerializeField] private bool autoSpawn = true;

        [Header("Anti-clipping")]
        [SerializeField] private float forwardPadding = 0.2f;
        [SerializeField] private float checkRadius = 0.25f;
        [SerializeField] private int pushOutIters = 8;
        [SerializeField] private LayerMask overlapMask = ~0;

        private float lastSpawnTime;
        private Collider2D ownerCol;

        public void SetTrapEnabled(bool enabled) => autoSpawn = enabled;

        private void Awake()
        {
            ownerCol = GetComponent<Collider2D>();
        }

        private void Update()
        {
            if (!autoSpawn) return;

            if (Time.time - lastSpawnTime >= spawnCooldown)
            {
                lastSpawnTime = Time.time;
                Spawn();
            }
        }

        public void Spawn()
        {
            if (ballPrefab == null) return;

            // 맵 direction과 기본 Sprite의 위쪽(+Y)을 발사 전방으로 사용한다.
            Vector2 dir = ((Vector2)transform.up).normalized;
            Vector2 spawnPos = (Vector2)transform.TransformPoint(spawnLocalOffset);
            spawnPos += dir * cellSize;
            spawnPos += dir * forwardPadding;

            for (int i = 0; i < pushOutIters; i++)
            {
                if (!Physics2D.OverlapCircle(spawnPos, checkRadius, overlapMask))
                    break;

                spawnPos += dir * (checkRadius * 0.6f);
            }

            RollingBallTrap2D ball = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
            ball.Launch(dir, speed, TrapDamageEvent.ResolveTrapCode(gameObject), ownerCol);
        }
    }
}
