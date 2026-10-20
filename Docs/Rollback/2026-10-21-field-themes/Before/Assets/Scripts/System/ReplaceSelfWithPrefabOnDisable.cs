using UnityEngine;
using Dungeon.Core;
using Dungeon.Map;

namespace Dungeon.System
{
/// <summary>
/// 002-002 스위치가 SetTrapEnabled(false)를 호출하면
/// 자기 자신(예: 100-000 낭떠러지)을 000-000 블록으로 "교체"한다.
/// </summary>
public class ReplaceSelfWithPrefabOnDisable : MonoBehaviour, ITrapToggle
{
    [Header("Replacement")]
    [SerializeField] private string prefabFolder = "Prefabs/";
    [SerializeField] private string replaceToCode = "000-000"; // 대체될 프리팹 코드

    [Header("Behavior")]
    [SerializeField] private bool destroySelfAfterReplace = true; // 교체 후 자신 제거
    [SerializeField] private bool onlyOnce = true; // 한 번만 교체

    private bool replaced = false;

    public void SetTrapEnabled(bool enabled)
    {
        // 스위치가 "정지"시키는 순간(false)에 교체
        if (enabled) return;

        if (onlyOnce && replaced) return;
        Replace();
    }

    private void Replace()
    {
        replaced = true;

        // 1) 대체 프리팹 로드
        string path = prefabFolder + replaceToCode.Trim().ToUpperInvariant();
        GameObject prefab = Resources.Load<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogWarning($"[ReplaceSelf] 프리팹을 찾지 못함: Resources/{path}.prefab");
            return;
        }

        // 2) 현재 위치/회전/부모에 그대로 생성
        Transform parent = transform.parent;
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;

        GameObject newBlock = Instantiate(prefab, pos, rot, parent);
        newBlock.name = $"{replaceToCode}_ReplacedFrom_{gameObject.name}";

        // 3) 좌표 유지 (BlockCoord가 있으면 복사)
        CopyBlockCoordIfExists(this.gameObject, newBlock);

        // 4) BlockRegistry 갱신 (있으면)
        TryUpdateRegistry(newBlock);

        // 5) 기존 오브젝트 제거(또는 비활성)
        if (destroySelfAfterReplace)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private void CopyBlockCoordIfExists(GameObject from, GameObject to)
    {
        var fromBC = from.GetComponent<BlockCoord>();
        if (fromBC == null) return;

        var toBC = to.GetComponent<BlockCoord>();
        if (toBC == null) toBC = to.AddComponent<BlockCoord>();

        // BlockCoord 구현이 프로젝트마다 다를 수 있어서,
        // 여기서는 "SetCoord(string)"만 있다고 가정하고 호출.
        // (네 Builder가 bc.SetCoord(coordStr)로 쓰고 있음 :contentReference[oaicite:2]{index=2})
        try
        {
            // fromBC에서 좌표 문자열을 꺼내오는 표준 메서드가 없다면
            // 이름 규칙으로 파싱해서 좌표를 만들어 넣는 방식도 가능함.
            // 우선은 "from 오브젝트 이름에서 _뒤 4글자" 파싱을 시도.
            string coord = TryGetCoordString(from);

            if (!string.IsNullOrEmpty(coord))
                toBC.SetCoord(coord);
        }
        catch
        {
            // BlockCoord가 다르게 생긴 경우 (문제 생기면 이 부분만 너 프로젝트에 맞춰 수정)
        }
    }

    private string TryGetCoordString(GameObject go)
    {
        // Builder의 이름 규칙: $"{code}_{x:X2}{z:X2}" :contentReference[oaicite:3]{index=3}
        // 예: "100-000_0305"
        string n = go.name;
        int idx = n.LastIndexOf('_');
        if (idx < 0 || idx + 1 >= n.Length) return null;

        string coord = n.Substring(idx + 1);
        if (coord.Length < 4) return null;

        return coord.Substring(0, 4).ToLowerInvariant();
    }

    private void TryUpdateRegistry(GameObject newBlock)
    {
        // Registry는 Builder가 Register(coordStr, block)로 등록하고 있음 :contentReference[oaicite:4]{index=4}
        // SwitchStopper도 reg.TryGet(coord, out go)를 사용 중 :contentReference[oaicite:5]{index=5}

        var reg = BlockRegistry.Instance;
        if (reg == null) return;

        string coord = TryGetCoordString(newBlock);
        if (string.IsNullOrEmpty(coord)) return;

        // 대부분 Register가 덮어쓰기 되도록 구현되어있을 확률이 높아서 그냥 Register 호출
        reg.Register(coord, newBlock);
    }
}
}