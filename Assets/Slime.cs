using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Slime : MonoBehaviour
{
    [Header("Slime")]
    [SerializeField] private float slowAmount = 2f;
    [SerializeField] private float duration = 2f;

    private readonly Dictionary<SpeedController, Coroutine> active = new();

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;   // ✅ Trigger ON
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;   // ✅ Trigger ON (강제)
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApply(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApply(other);
    }

    private void TryApply(Collider other)
    {
        var sc = other.GetComponentInParent<SpeedController>();
        if (sc == null) return;

        if (active.ContainsKey(sc)) return;

        Coroutine c = StartCoroutine(ApplySlow(sc));
        active[sc] = c;
    }

    private IEnumerator ApplySlow(SpeedController sc)
    {
        sc.AddBonus(-slowAmount);

        yield return new WaitForSeconds(duration);

        if (sc != null)
            sc.AddBonus(+slowAmount);

        active.Remove(sc);
    }

    private void OnDisable()
    {
        foreach (var kv in active)
        {
            var sc = kv.Key;
            if (sc != null) sc.AddBonus(+slowAmount);
        }
        active.Clear();
    }
}
