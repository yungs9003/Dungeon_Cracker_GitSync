using UnityEngine;

namespace Dungeon.Traps
{
    public class BallEffectSpawner2D : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Collider2D ballCollider;
        [SerializeField] private GameObject afterimagePrefab;

        [Header("Spawn")]
        [SerializeField] private float minSpeedToSpawn = 0.2f;
        [SerializeField] private float distanceBetweenMarks = 0.18f;
        [SerializeField] private float gapBehindBall = 0.05f;

        [Header("Rotation")]
        [SerializeField] private bool rotateToMoveDirection = true;
        [SerializeField] private float angleOffset = -90f;
        // 기본 이펙트 이미지가 아래(-Y)를 보는 방향일 때 추천값

        [Header("Scale")]
        [SerializeField] private bool matchEffectLengthToBallSize = true;
        [SerializeField] private float thicknessMultiplier = 1f;

        private Vector3 lastSpawnPos;
        private bool hasSpawnPos;

        private void Awake()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody2D>();

            if (ballCollider == null)
                ballCollider = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            hasSpawnPos = false;
        }

        private void Update()
        {
            if (rb == null || afterimagePrefab == null)
                return;

            Vector2 vel = rb.linearVelocity;
            float speed = vel.magnitude;

            if (speed < minSpeedToSpawn)
            {
                hasSpawnPos = false;
                return;
            }

            Vector2 dir = vel.normalized;

            // 공 중심이 아니라 "공 뒤쪽 바닥 위치"를 기준으로 자국 생성
            float ballRadius = GetBallRadiusWorld();
            Vector3 targetMarkPos = transform.position - (Vector3)(dir * (ballRadius + gapBehindBall));

            // 첫 프레임에는 위치만 기억하고 생성은 안 함
            if (!hasSpawnPos)
            {
                lastSpawnPos = targetMarkPos;
                hasSpawnPos = true;
                return;
            }

            float remain = Vector3.Distance(lastSpawnPos, targetMarkPos);

            // 시간 간격이 아니라 "이동 거리 간격" 기준으로 자국 생성
            while (remain >= distanceBetweenMarks)
            {
                Vector3 spawnPos = Vector3.MoveTowards(lastSpawnPos, targetMarkPos, distanceBetweenMarks);

                SpawnEffect(spawnPos, dir);

                lastSpawnPos = spawnPos;
                remain = Vector3.Distance(lastSpawnPos, targetMarkPos);
            }
        }

        private void SpawnEffect(Vector3 spawnPos, Vector2 dir)
        {
            Quaternion rot = Quaternion.identity;

            if (rotateToMoveDirection)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + angleOffset;
                rot = Quaternion.Euler(0f, 0f, angle);
            }

            GameObject fx = Instantiate(afterimagePrefab, spawnPos, rot);

            if (matchEffectLengthToBallSize)
                ApplyScaleToMatchBall(fx);
        }

        private float GetBallRadiusWorld()
        {
            if (ballCollider != null)
            {
                Bounds b = ballCollider.bounds;
                return Mathf.Max(b.extents.x, b.extents.y);
            }

            return 0.5f * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        }

        private void ApplyScaleToMatchBall(GameObject fx)
        {
            if (ballCollider == null) return;

            SpriteRenderer sr = fx.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;

            float ballDiameter = ballCollider.bounds.size.x;

            Vector3 scale = fx.transform.localScale;

            // 현재 원본 이펙트가 "세로형"이고 아래(-Y)를 보는 이미지라고 가정
            // 회전 후 길이축은 local Y를 기준으로 맞추는 편이 자연스러움
            float spriteLength = sr.sprite.bounds.size.y;
            if (spriteLength > 0.0001f)
            {
                float scaleY = ballDiameter / spriteLength;
                scale.y *= scaleY;
            }

            scale.x *= thicknessMultiplier;

            fx.transform.localScale = scale;
        }
    }
}