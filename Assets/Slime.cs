using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Slime : MonoBehaviour
{
    [Header("Slime")]
    [SerializeField] private float slowAmount = 2f;     // 속도 2 감소
    [SerializeField] private float duration = 2f;       // 2초 지속

    // 같은 대상이 지속적으로 밟아도 duration 동안은 재적용 X (코루틴으로 관리)
    private readonly Dictionary<SpeedController, Coroutine> active = new();

    private void Reset()
    {
        // Collision 방식이면 Trigger OFF
        var col = GetComponent<Collider>();
        col.isTrigger = false;
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col.isTrigger) col.isTrigger = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryApply(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        // 계속 밟고 있어도 "이미 걸려있으면" 무시되도록 active로 막음
        TryApply(collision.collider);
    }

    private void TryApply(Collider other)
    {
        var sc = other.GetComponentInParent<SpeedController>();
        if (sc == null) return;

        // 이미 이 타일이 해당 플레이어에게 디버프를 걸어둔 상태면 재적용 금지
        if (active.ContainsKey(sc)) return;

        Coroutine c = StartCoroutine(ApplySlow(sc));
        active[sc] = c;
    }

    private IEnumerator ApplySlow(SpeedController sc)
    {
        // 속도 감소 적용
        sc.AddBonus(-slowAmount);

        yield return new WaitForSeconds(duration);

        // 2초 후 원복
        if (sc != null)
            sc.AddBonus(+slowAmount);

        active.Remove(sc);
    }

    private void OnDisable()
    {
        // 오브젝트가 비활성화될 때 느려진 애들 원복(안전장치)
        foreach (var kv in active)
        {
            var sc = kv.Key;
            if (sc != null) sc.AddBonus(+slowAmount);
        }
        active.Clear();
    }
}
