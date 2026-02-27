using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.Traps;

[DisallowMultipleComponent]
public class SwitchStopper002002 : MonoBehaviour, IRelationTargetsReceiver
{
    [Header("Switch Behavior")]
    [SerializeField] private bool disableOnEnter = true;
    [SerializeField] private bool reEnableOnExit = false;

    [Header("Trigger Collider (must be IsTrigger=true)")]
    [SerializeField] private Collider triggerCollider;   // ✅ 스위치 감지용 콜라이더를 명시

    [Header("Player detect")]
    [SerializeField] private LayerMask playerMask;

    private List<string> targetCoords = new();
    private readonly List<GameObject> targetObjects = new();

    private void Reset()
    {
        // 같은 오브젝트에 콜라이더가 여러 개일 수 있으니 "명시"가 필요
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null) triggerCollider.isTrigger = true;
    }

    private void Awake()
    {
        // 런타임에도 안전장치
        if (triggerCollider == null) triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
            triggerCollider.isTrigger = true;
    }

    public void SetRelationTargets(List<string> coords)
    {
        targetCoords = coords ?? new List<string>();
    }

    private void Start()
    {
        StartCoroutine(ResolveTargetsNextFrame());
    }

    private IEnumerator ResolveTargetsNextFrame()
    {
        yield return null;

        targetObjects.Clear();
        var reg = BlockRegistry.Instance;
        if (reg == null) yield break;

        foreach (var c in targetCoords)
            if (reg.TryGet(c, out var go) && go != null)
                targetObjects.Add(go);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        if (disableOnEnter) SetTargetsEnabled(false);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        if (reEnableOnExit) SetTargetsEnabled(true);
    }

    private bool IsPlayer(Collider other)
    {
        if (playerMask.value == 0) return other.GetComponentInParent<PlayerController>() != null;
        return (playerMask.value & (1 << other.gameObject.layer)) != 0;
    }

    private void SetTargetsEnabled(bool enabled)
    {
        foreach (var go in targetObjects)
        {
            if (go == null) continue;

            // ✅ 스위치로 제어할 대상은 "ITrapToggle"로만 제한 (안전)
            var toggles = go.GetComponentsInChildren<ITrapToggle>(true);
            foreach (var t in toggles)
                t.SetTrapEnabled(enabled);

            // ❌ fallback으로 모든 MonoBehaviour 끄는 건 바닥/구조까지 망가질 수 있어 권장 X
        }
    }
}
