using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Arrow : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float lifeTime = 3f;

    [Header("Visual (이미지/모델만 따로 회전시키고 싶으면 지정)")]
    [SerializeField] private Transform visual;

    [Header("If your arrow model's forward axis is NOT Z")]
    [SerializeField] private Vector3 modelForwardAxis = Vector3.forward; 
    // 예: 모델이 X축이 앞이면 Vector3.right 로 바꾸기

    private Rigidbody rb;
    private int damage;
    private Collider ownerCol;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        // Trigger 기반 충돌이 편하면 콜라이더 IsTrigger ON 권장
        // rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 빠르면 추천

        if (visual == null) visual = transform;
    }

    public void Launch(Vector3 dir, float speed, int damage, Collider owner = null)
    {
        this.damage = damage;
        ownerCol = owner;

        // 발사자와 충돌 무시
        if (ownerCol != null)
        {
            Collider myCol = GetComponent<Collider>();
            Physics.IgnoreCollision(myCol, ownerCol, true);
        }

        dir.y = 0f;
        dir.Normalize();

        rb.linearVelocity = dir * speed;   // Unity 6: linearVelocity 사용 가능
        AlignVisualToDirection(dir);

        CancelInvoke(nameof(Despawn));
        Invoke(nameof(Despawn), lifeTime);
    }

    private void AlignVisualToDirection(Vector3 dir)
    {
        // dir을 바라보게 회전 (기본은 Z가 forward인 모델 기준)
        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);

        // 모델 forward 축이 Z가 아니라면 보정
        Quaternion axisFix = Quaternion.FromToRotation(modelForwardAxis, Vector3.forward);
        visual.rotation = look * Quaternion.Inverse(axisFix);
    }

    // Trigger 방식(추천): Arrow Collider IsTrigger = ON
    private void OnTriggerEnter(Collider other)
    {
        if (ownerCol != null && other == ownerCol) return;

        // ✅ 플레이어 히트박스에 Health가 직접 없을 수도 있으니 부모까지 탐색
        Health health = other.GetComponentInParent<Health>();
        if (health != null)
        {
            health.TakeDamage(damage);
            Despawn();
            return;
        }

        // 벽/바닥 등에 닿아도 사라지게 하고 싶으면 아래 유지
        Despawn();
    }

    private void Despawn()
    {
        Destroy(gameObject);
    }
}
