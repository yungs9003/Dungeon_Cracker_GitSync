using UnityEngine;
using Dungeon.Core;
using Dungeon.Player;

namespace Dungeon.Traps
{
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Arrow : MonoBehaviour, IPlayerDestroyableProjectile // ✅ IPlayerDestroyableProjectile 구현 추가
{
    [Header("Lifetime")]
    [SerializeField] private float lifeTime = 3f;

    [Header("Visual")]
    [SerializeField] private Transform visual;

    [Header("Model forward axis (if not Z)")]
    [SerializeField] private Vector3 modelForwardAxis = Vector3.forward;

    [Header("Hit timing")]
    [SerializeField] private float hitEnableDelay = 0.05f; // 너무 길면 '붙어있을 때 무시'가 커짐

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private Rigidbody rb;
    private int damage;
    private Collider ownerCol;
    private Collider myCol;

    private bool hasHit;
    private float spawnedAt;
    private float hitEnabledAt;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCol = GetComponent<Collider>();
        rb.useGravity = false;

        if (visual == null) visual = transform;
    }

    public void Launch(Vector3 dir, float speed, int damage, Quaternion rot, Collider owner = null)
    {
        this.damage = damage;
        ownerCol = owner;
        hasHit = false;

        spawnedAt = Time.time;
        hitEnabledAt = spawnedAt + hitEnableDelay;

        // 발사자와 충돌 무시(발사자 콜라이더 여러 개 대비)
        if (ownerCol != null)
        {
            var ownerColsParent = ownerCol.GetComponentsInParent<Collider>();
            for (int i = 0; i < ownerColsParent.Length; i++)
                Physics.IgnoreCollision(myCol, ownerColsParent[i], true);

            var ownerColsChild = ownerCol.GetComponentsInChildren<Collider>();
            for (int i = 0; i < ownerColsChild.Length; i++)
                Physics.IgnoreCollision(myCol, ownerColsChild[i], true);
        }

        dir.y = 0f;
        dir.Normalize();

        rb.rotation = rot;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.linearVelocity = dir * speed;

        AlignVisualToDirection(dir);

        CancelInvoke(nameof(Despawn));
        Invoke(nameof(Despawn), lifeTime);
    }

    private void AlignVisualToDirection(Vector3 dir)
    {
        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion axisFix = Quaternion.FromToRotation(modelForwardAxis, Vector3.forward);
        visual.rotation = look * Quaternion.Inverse(axisFix);
    }

    // ✅ 스폰 직후 겹쳐있으면 Enter가 delay 때문에 무시될 수 있으니 Stay도 같이 처리
    private void OnTriggerEnter(Collider other) => TryHit(other, "Enter");
    private void OnTriggerStay(Collider other)  => TryHit(other, "Stay");

    private void TryHit(Collider other, string evt)
    {
        if (hasHit) return;
        if (other.GetComponentInParent<Arrow>() != null) return;

        // hitEnableDelay: 아직 활성화 전이면 로그만 찍고 return
        if (Time.time < hitEnabledAt)
        {
            return;
        }
        // ✅ 추가: 트리거는 데미지 대상으로 취급하지 않음 (ForwardZone 방지)
        if (other.isTrigger) return;
        Health health = other.GetComponentInParent<Health>();
        if (health != null)
        {
            hasHit = true;

            health.TakeDamage(damage);
            Despawn();
            return;
        }

        // 벽/바닥 등
        hasHit = true;
        Despawn();
    }

    // ✅ 플레이어 액션으로 제거될 때 호출
    public void DestroyByPlayer()
    {
        Despawn();
    }

    private void Despawn()
    {
        Destroy(gameObject);
    }
}
}