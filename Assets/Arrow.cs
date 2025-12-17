using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Arrow : MonoBehaviour
{
    [SerializeField] private float lifeTime = 3f;

    private Rigidbody rb;
    private int damage;
    private Collider ownerCol;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // 화살 중력 원하면 true로 바꿔도 됨
    }

    public void Init(Vector3 direction, float speed, int damage, Collider owner = null)
    {
        this.damage = damage;
        ownerCol = owner;

        if (ownerCol != null)
            Physics.IgnoreCollision(GetComponent<Collider>(), ownerCol, true);

        rb.linearVelocity = direction.normalized * speed;

        CancelInvoke(nameof(Despawn));
        Invoke(nameof(Despawn), lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (ownerCol != null && other == ownerCol) return;

        var health = other.GetComponent<Health>();
        if (health != null) health.TakeDamage(damage);

        Despawn();
    }

    private void Despawn()
    {
        Destroy(gameObject);
    }
}
