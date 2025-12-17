using UnityEngine;

public class Bow : MonoBehaviour
{
    [Header("Assign in Prefab")]
    public Arrow projectilePrefab;
    public Transform firePoint;

    [Header("Stats")]
    public float speed = 12f;
    public int damage = 10;
    public float fireCooldown = 1.0f;

    private float lastFireTime;
    private Collider ownerCollider;

    private void Awake()
    {
        ownerCollider = GetComponent<Collider>();
    }

    private void Update()
    {
        // 테스트용: 자동 발사(원하면 조건 바꿔서 트랩처럼 사용)
        if (Time.time - lastFireTime >= fireCooldown)
        {
            lastFireTime = Time.time;
            Fire();
        }
    }

    private void Fire()
    {
        if (projectilePrefab == null || firePoint == null) return;

        var proj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        proj.Init(firePoint.forward, speed, damage, ownerCollider);
    }
}
