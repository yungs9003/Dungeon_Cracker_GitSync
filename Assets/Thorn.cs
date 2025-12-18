using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Thorn : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 2;
    [SerializeField] private float hitCooldown = 2f;

    // 같은 대상이 여러 번 닿아도 hitCooldown 동안은 무시
    private readonly Dictionary<Health, float> nextDamageTime = new();

    private void Reset()
    {
        // 충돌 이벤트를 쓰려면 Trigger가 아니어야 함
        var col = GetComponent<Collider>();
        col.isTrigger = false;
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col.isTrigger)
            col.isTrigger = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryDamage(collision.collider);
    }

    // 계속 닿아있는 동안 쿨다운 끝나면 다시 데미지 주고 싶으면 유지
    private void OnCollisionStay(Collision collision)
    {
        TryDamage(collision.collider);
    }

    private void TryDamage(Collider other)
    {
        // 충돌한 오브젝트(또는 부모)에 Health가 있는지 탐색
        Health health = other.GetComponentInParent<Health>();
        if (health == null) return;

        float now = Time.time;

        if (nextDamageTime.TryGetValue(health, out float allowedTime) && now < allowedTime)
            return;

        health.TakeDamage(damage);
        nextDamageTime[health] = now + hitCooldown;
    }
}
