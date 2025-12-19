using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SwitchStopper002002 : MonoBehaviour, IRelationTargetsReceiver
{
    [Header("Switch Behavior")]
    [SerializeField] private bool disableOnEnter = true;
    [SerializeField] private bool reEnableOnExit = false;

    [Header("Player detect")]
    [SerializeField] private LayerMask playerMask; // Player 레이어 지정 추천

    private List<string> targetCoords = new();
    private readonly List<GameObject> targetObjects = new();

    private void Reset()
    {
        // 스위치는 Trigger가 편함
        GetComponent<Collider>().isTrigger = true;
    }

    public void SetRelationTargets(List<string> coords)
    {
        targetCoords = coords ?? new List<string>();
        Debug.Log($"[Switch] SetRelationTargets called. coords={targetCoords.Count}");
    }

    private void Start()
    {
        Debug.Log($"[Switch] Start. targetCoords={targetCoords?.Count ?? 0}");
        // 맵 생성 직후 타이밍 이슈 방지: 한 프레임 뒤에 resolve
        StartCoroutine(ResolveTargetsNextFrame());
    }

    private IEnumerator ResolveTargetsNextFrame()
    {
        yield return null;

        targetObjects.Clear();
        var reg = BlockRegistry.Instance;
        if (reg == null) yield break;

        foreach (var c in targetCoords)
        {
            if (reg.TryGet(c, out var go) && go != null)
                targetObjects.Add(go);
        }
        Debug.Log($"[Switch] Resolved targetObjects = {targetObjects.Count}");

    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[Switch] TriggerEnter with {other.name}, layer={LayerMask.LayerToName(other.gameObject.layer)}");
        if (!IsPlayer(other)) return;
        Debug.Log($"[Switch] Player confirmed. targetsCoords={targetCoords?.Count ?? 0}, targetObjects={targetObjects.Count}");
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
            int cnt=0;
            // 1) 인터페이스가 있으면 그걸 우선 사용
            var toggles = go.GetComponentsInChildren<ITrapToggle>(true);
            if (toggles != null && toggles.Length > 0)
            {
                foreach (var t in toggles) t.SetTrapEnabled(enabled);
                cnt++;
            }
            Debug.Log($"[Switch] {go.name} has ITrapToggle count = {cnt}");

            // 2) 없으면 Behaviour를 꺼버리는 fallback (함정 스크립트들이 MonoBehaviour일 때)
            //    너무 광범위하게 끄기 싫으면 특정 타입만 골라 끄도록 바꿔도 됨.
            var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var b in behaviours)
            {
                // 자기 자신(스위치)은 끄지 않기
                if (b == null || b.gameObject == this.gameObject) continue;
                // 필요하면 여기서 화이트리스트/블랙리스트로 조절 가능
                b.enabled = enabled;
            }
        }
    }
    
}
