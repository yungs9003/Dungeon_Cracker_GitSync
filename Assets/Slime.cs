using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Slime : MonoBehaviour, ITrapToggle
{
    [Header("Slime")]
    [SerializeField] private float slowAmount = 2f;
    [SerializeField] private float duration = 2f;

    [Header("Trap Toggle")]
    [SerializeField] private bool trapEnabled = true;

    private readonly Dictionary<SpeedController, Coroutine> active = new();

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    public void SetTrapEnabled(bool enabled)
    {
        if (trapEnabled == enabled) return;
        trapEnabled = enabled;

        // ✅ OFF가 되면 현재 걸린 슬로우 즉시 해제(원하는 동작이라면)
        if (!trapEnabled)
        {
            foreach (var kv in active)
            {
                if (kv.Value != null) StopCoroutine(kv.Value);

                var sc = kv.Key;
                if (sc != null) sc.AddBonus(+slowAmount);
            }
            active.Clear();
        }
    }

    private void OnTriggerEnter(Collider other) => TryApply(other);
    private void OnTriggerStay(Collider other)  => TryApply(other);

    private void TryApply(Collider other)
    {
        if (!trapEnabled) return; // ✅ OFF면 아무것도 안 함

        var sc = other.GetComponentInParent<SpeedController>();
        if (sc == null) return;

        if (active.ContainsKey(sc)) return;

        Coroutine c = StartCoroutine(ApplySlow(sc));
        active[sc] = c;
    }

    private IEnumerator ApplySlow(SpeedController sc)
    {
        // ✅ 코루틴 시작 전에 혹시 OFF면 정리하고 종료
        if (!trapEnabled)
        {
            active.Remove(sc);
            yield break;
        }

        sc.AddBonus(-slowAmount);

        yield return new WaitForSeconds(duration);

        if (sc != null)
            sc.AddBonus(+slowAmount);

        active.Remove(sc);
    }

    private void OnDisable()
    {
        // 오브젝트 비활성화될 때도 원복
        foreach (var kv in active)
        {
            var sc = kv.Key;
            if (sc != null) sc.AddBonus(+slowAmount);
        }
        active.Clear();
    }
}
