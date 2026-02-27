using UnityEngine;

namespace Dungeon.Traps;

public class Destructible : MonoBehaviour
{
    [Header("What to destroy")]
    [Tooltip("보통 블록 루트(함정/콜라이더/비주얼이 묶여있는 최상위)를 넣어주세요.")]
    [SerializeField] private Transform destroyRoot;

    [Header("Trap toggle (optional)")]
    [Tooltip("비워두면 destroyRoot 하위에서 ITrapToggle 전부 찾아서 꺼요.")]
    [SerializeField] private MonoBehaviour[] explicitTrapToggles; // ITrapToggle 구현체들

    [Header("Behavior")]
    [SerializeField] private bool disableTrapsBeforeDestroy = true;
    [SerializeField] private bool destroyNextFrame = true; // 안정성 위해 1프레임 뒤 삭제 권장

    private void Awake()
    {
        if (destroyRoot == null) destroyRoot = transform;
    }

    public void ExecuteDestruction()
    {
        // ✅ 게이트 체크 없음: 항상 파괴 가능

        if (disableTrapsBeforeDestroy)
            DisableTraps();

        if (destroyRoot == null) return;

        if (destroyNextFrame)
            StartCoroutine(DestroyCo());
        else
            Destroy(destroyRoot.gameObject);
    }

    private void DisableTraps()
    {
        if (destroyRoot == null) return;

        // 1) 명시적으로 넣어준 트랩이 있으면 그것만
        if (explicitTrapToggles != null && explicitTrapToggles.Length > 0)
        {
            foreach (var mb in explicitTrapToggles)
            {
                if (mb == null) continue;
                if (mb is ITrapToggle t) t.SetTrapEnabled(false);
            }
            return;
        }

        // 2) 아니면 destroyRoot 하위에서 전부 찾아서 끄기
        var toggles = destroyRoot.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in toggles)
        {
            if (mb is ITrapToggle t)
                t.SetTrapEnabled(false);
        }
    }

    private System.Collections.IEnumerator DestroyCo()
    {
        yield return null;
        if (destroyRoot != null)
            Destroy(destroyRoot.gameObject);
    }
}