using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Thorn : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 2;
    [SerializeField] private float hitCooldown = 2f;

    // 같은 대상이 여러 번 닿아도 2초 동안은 무시
    private readonly Dictionary<Health, float> nextDamageTime = new();

    private void Reset()
    {
        // 기본은 트리거 권장 (밟았을 때 감지 쉽고 안정적)
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        // 트리거 이벤트가 안정적으로 오려면 (둘 중 하나라도 Rigidbody 필요)
        // 플레이어가 Rigidbody가 없다면, 가시 타일에 Rigidbody(kinematic)를 붙이면 됨.
        // (이 스크립트는 Rigidbody를 강제하진 않음)
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDamage(other);
    }

    // Enter가 누락되거나 계속 밟고 있는 상태에서도 "쿨다운 끝나면 다시 1회" 주고 싶다면 유지
    private void OnTriggerStay(Collider other)
    {
        TryDamage(other);
    }

    private void TryDamage(Collider other)
    {
        // 플레이어 히트박스에 Health가 직접 없을 수 있으니 부모까지 탐색
        Health health = other.GetComponentInParent<Health>();
        if (health == null) return;

        float now = Time.time;

        if (nextDamageTime.TryGetValue(health, out float allowedTime) && now < allowedTime)
            return; // 아직 쿨다운 중

        health.TakeDamage(damage);
        nextDamageTime[health] = now + hitCooldown;
    }
}
