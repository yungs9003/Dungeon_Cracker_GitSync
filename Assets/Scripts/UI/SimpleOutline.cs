using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SimpleOutline : MonoBehaviour
{
    [Header("Outline")]
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Color outlineColor = Color.yellow;
    [SerializeField, Range(0.001f, 0.1f)] private float outlineWidth = 0.02f;

    [Header("Targets")]
    [SerializeField] private bool includeInactiveChildren = false;

    private readonly List<GameObject> outlineObjects = new();
    private bool built = false;

    private void Awake()
    {
        BuildOutlineObjects();
        SetOutlineEnabled(false);
    }

    private void OnEnable()
    {
        if (!built)
            BuildOutlineObjects();
    }

    private void OnDisable()
    {
        SetOutlineEnabled(false);
    }

    public void SetOutlineEnabled(bool enabled)
    {
        if (!built)
            BuildOutlineObjects();

        foreach (var go in outlineObjects)
        {
            if (go != null)
                go.SetActive(enabled);
        }
    }

    public void Rebuild()
    {
        ClearOutlineObjects();
        BuildOutlineObjects();
    }

    private void BuildOutlineObjects()
    {
        if (built) return;
        built = true;

        if (outlineMaterial == null)
        {
            Debug.LogWarning($"[{name}] SimpleOutline: outlineMaterial이 비어 있습니다.");
            return;
        }

        // MeshRenderer + MeshFilter
        var meshFilters = GetComponentsInChildren<MeshFilter>(includeInactiveChildren);
        foreach (var mf in meshFilters)
        {
            if (mf == null) continue;
            if (mf.sharedMesh == null) continue;

            var srcRenderer = mf.GetComponent<MeshRenderer>();
            if (srcRenderer == null) continue;

            CreateOutlineForMeshRenderer(mf, srcRenderer);
        }

        // SkinnedMeshRenderer
        var skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(includeInactiveChildren);
        foreach (var smr in skinnedRenderers)
        {
            if (smr == null) continue;
            if (smr.sharedMesh == null) continue;

            CreateOutlineForSkinnedRenderer(smr);
        }
    }

    private void CreateOutlineForMeshRenderer(MeshFilter sourceFilter, MeshRenderer sourceRenderer)
    {
        GameObject outlineGO = new GameObject(sourceFilter.gameObject.name + "_Outline");
        outlineGO.transform.SetParent(sourceFilter.transform, false);
        outlineGO.transform.localPosition = Vector3.zero;
        outlineGO.transform.localRotation = Quaternion.identity;
        outlineGO.transform.localScale = Vector3.one;
        outlineGO.layer = sourceFilter.gameObject.layer;

        var mf = outlineGO.AddComponent<MeshFilter>();
        mf.sharedMesh = sourceFilter.sharedMesh;

        var mr = outlineGO.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(outlineMaterial);
        ApplyOutlineProperties(mr.sharedMaterial);

        CopyRendererSettings(sourceRenderer, mr);

        outlineGO.SetActive(false);
        outlineObjects.Add(outlineGO);
    }

    private void CreateOutlineForSkinnedRenderer(SkinnedMeshRenderer sourceRenderer)
    {
        GameObject outlineGO = new GameObject(sourceRenderer.gameObject.name + "_Outline");
        outlineGO.transform.SetParent(sourceRenderer.transform, false);
        outlineGO.transform.localPosition = Vector3.zero;
        outlineGO.transform.localRotation = Quaternion.identity;
        outlineGO.transform.localScale = Vector3.one;
        outlineGO.layer = sourceRenderer.gameObject.layer;

        var smr = outlineGO.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = sourceRenderer.sharedMesh;
        smr.rootBone = sourceRenderer.rootBone;
        smr.bones = sourceRenderer.bones;
        smr.sharedMaterial = new Material(outlineMaterial);
        ApplyOutlineProperties(smr.sharedMaterial);

        CopyRendererSettings(sourceRenderer, smr);

        outlineGO.SetActive(false);
        outlineObjects.Add(outlineGO);
    }

    private void ApplyOutlineProperties(Material mat)
    {
        if (mat == null) return;

        if (mat.HasProperty("_OutlineColor"))
            mat.SetColor("_OutlineColor", outlineColor);

        if (mat.HasProperty("_OutlineWidth"))
            mat.SetFloat("_OutlineWidth", outlineWidth);
    }

    private void CopyRendererSettings(Renderer src, Renderer dst)
    {
        dst.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dst.receiveShadows = false;
        dst.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        dst.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        dst.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        dst.allowOcclusionWhenDynamic = false;

        // 렌더 순서 살짝 뒤로
        dst.sortingLayerID = src.sortingLayerID;
        dst.sortingOrder = src.sortingOrder;
    }

    private void ClearOutlineObjects()
    {
        for (int i = outlineObjects.Count - 1; i >= 0; i--)
        {
            if (outlineObjects[i] != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(outlineObjects[i]);
                else
                    Destroy(outlineObjects[i]);
#else
                Destroy(outlineObjects[i]);
#endif
            }
        }

        outlineObjects.Clear();
        built = false;
    }
}