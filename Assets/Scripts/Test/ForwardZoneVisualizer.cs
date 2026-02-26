using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ForwardZoneVisualizer : MonoBehaviour
{
    [SerializeField] private Material visualMaterial; // 반투명 노란 머티리얼
    [SerializeField] private string visualChildName = "ForwardZoneVisual";

    private BoxCollider box;
    private Transform vis;

    void Awake()
    {
        box = GetComponent<BoxCollider>();

        var child = transform.Find(visualChildName);
        if (child == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = visualChildName;
            go.transform.SetParent(transform, false);

            // 시각화용이니 콜라이더 제거
            Destroy(go.GetComponent<Collider>());

            vis = go.transform;
            var mr = go.GetComponent<MeshRenderer>();
            if (visualMaterial != null) mr.sharedMaterial = visualMaterial;
        }
        else vis = child;

        Sync();
    }

    void LateUpdate() => Sync();

    void Sync()
    {
        if (vis == null || box == null) return;

        // BoxCollider의 center/size에 맞춰서 큐브 배치
        vis.localPosition = box.center;
        vis.localRotation = Quaternion.identity;
        vis.localScale = box.size;
    }
}