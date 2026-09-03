using System;
using System.Collections.Generic;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Map
{
    /// <summary>
    /// 플레이어 중심 원형 조명 마스크입니다.
    ///
    /// 동작 방식:
    /// - clearRadius 안쪽: 마스크가 없어 정상 표시
    /// - clearRadius ~ clearRadius + fadeBandRadius: 원형 그라데이션으로 급격히 어두워짐
    /// - 그 바깥: 완전 검은색으로 덮음
    ///
    /// clearRadius와 fadeBandRadius는 ForwardZoneCollector에서 가져옵니다.
    /// </summary>
    public class ForwardZoneVisionMask2D : MonoBehaviour
    {
        [Header("Forward Zone")]
        [SerializeField] private ForwardZoneCollector collector;

        [Header("Dark Mask")]
        [Tooltip("바깥 완전 암전 영역의 색입니다. 보통 검은색을 사용합니다.")]
        [SerializeField] private Color darkColor = Color.black;

        [Tooltip("clearRadius + fadeBandRadius 바깥의 최종 어두움입니다. 완전히 가리려면 1로 둡니다.")]
        [SerializeField, Range(0f, 1f)] private float darkAlpha = 1f;

        [Tooltip("감쇠 구간의 어두워지는 속도입니다. 값이 클수록 clearRadius 바깥에서 더 급격히 어두워집니다.")]
        [SerializeField, Range(0.5f, 8f)] private float fadeSteepness = 4f;

        [Tooltip("감쇠 구간을 몇 단계의 링으로 나눌지 정합니다. 8~16 정도면 충분합니다.")]
        [SerializeField, Range(1, 32)] private int fadeSteps = 10;

        [Tooltip("원의 부드러움입니다. 96~192 정도를 추천합니다.")]
        [SerializeField, Range(24, 360)] private int radialSegments = 144;

        [Tooltip("맵 전체를 덮기 위해 계산된 외곽 반지름에 더하는 여유값입니다.")]
        [SerializeField] private float outerRadiusPadding = 6f;

        [SerializeField] private int maskSortingOrder = 5000;
        [SerializeField] private string maskSortingLayerName = "";

        [Tooltip("호환용 옵션입니다. 현재 버전은 타일별 사각 마스크가 아니라 하나의 원형 Mesh 마스크를 사용합니다.")]
        [SerializeField] private bool coverOnlyExistingGridCells = true;

        [Header("Trap Reveal")]
        [Tooltip("켜져 있으면 한 번 발견된 함정은 조명 밖으로 나가도 실제 모습으로 유지됩니다.")]
        [SerializeField] private bool stayRevealedAfterSeen = true;

        [Tooltip("켜져 있으면 함정의 큰 감지 콜라이더가 아니라 타일 중심 위치가 clearRadius 안에 들어왔을 때만 reveal합니다.")]
        [SerializeField] private bool revealByTileCenterOnly = true;

        [Tooltip("타일 중심 reveal 판정에 더할 여유 반지름입니다. 경계에서 너무 빡빡하면 0.05~0.2 정도로 조정하세요.")]
        [SerializeField] private float revealRadiusPadding = 0.05f;

        private readonly List<HiddenTrapVisual2D> revealTraps = new();

        private Transform maskRoot;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh maskMesh;
        private Material maskMaterial;

        private float outerRadius = 20f;

        private Vector2 lastCenter = new Vector2(float.NaN, float.NaN);
        private float lastClearRadius = -1f;
        private float lastFadeBandRadius = -1f;
        private float lastDarkAlpha = -1f;
        private float lastFadeSteepness = -1f;
        private int lastFadeSteps = -1;
        private int lastSegments = -1;
        private float lastOuterRadius = -1f;

        private void Awake()
        {
            if (collector == null)
                collector = FindFirstObjectByType<ForwardZoneCollector>();
        }

        private void LateUpdate()
        {
            RefreshMask();
            RefreshHiddenTraps();
        }

        public void SetCollector(ForwardZoneCollector newCollector)
        {
            collector = newCollector;
            ForceMeshRefresh();
        }

        public void SetDarkAlpha(float value)
        {
            darkAlpha = Mathf.Clamp01(value);
            ApplyMaterialColor();
            ForceMeshRefresh();
        }

        public void SetStayRevealedAfterSeen(bool value)
        {
            stayRevealedAfterSeen = value;

            foreach (var trap in revealTraps)
            {
                if (trap != null)
                    trap.SetStayRevealedAfterSeen(value);
            }
        }

        public void ClearRegisteredTraps()
        {
            revealTraps.Clear();
        }

        public void RegisterTrap(HiddenTrapVisual2D trap)
        {
            if (trap == null) return;
            if (revealTraps.Contains(trap)) return;

            trap.SetStayRevealedAfterSeen(stayRevealedAfterSeen);
            revealTraps.Add(trap);
        }

        /// <summary>
        /// JsonBlockBuilder2D가 맵을 만든 뒤 호출합니다.
        /// 맵 크기를 기준으로 마스크 외곽 반지름을 충분히 크게 잡습니다.
        /// </summary>
        public void RebuildMask(MapData2D mapData, float cellSize, Transform parent)
        {
            ClearMaskObjects();
            CreateMaskMeshObject(parent);
            outerRadius = CalculateOuterRadius(mapData, cellSize) + outerRadiusPadding;
            ForceMeshRefresh();
            RefreshMask();
        }

        private void RefreshMask()
        {
            if (collector == null || maskRoot == null || meshFilter == null)
                return;

            Vector2 center = collector.ZoneCenter;
            float clearRadius = collector.ClearRadius;
            float fadeBandRadius = collector.FadeBandRadius;
            float requiredOuterRadius = Mathf.Max(outerRadius, collector.OuterLightRadius + outerRadiusPadding);

            // 마스크 Mesh는 플레이어 중심을 원점으로 하는 local 좌표로 만들고,
            // Root 오브젝트만 플레이어 위치로 이동시킵니다.
            maskRoot.position = new Vector3(center.x, center.y, transform.position.z);

            bool needRebuild =
                Vector2.Distance(center, lastCenter) > 0.001f ||
                !Mathf.Approximately(clearRadius, lastClearRadius) ||
                !Mathf.Approximately(fadeBandRadius, lastFadeBandRadius) ||
                !Mathf.Approximately(darkAlpha, lastDarkAlpha) ||
                !Mathf.Approximately(fadeSteepness, lastFadeSteepness) ||
                !Mathf.Approximately(requiredOuterRadius, lastOuterRadius) ||
                lastFadeSteps != fadeSteps ||
                lastSegments != radialSegments ||
                meshFilter.sharedMesh == null;

            if (needRebuild)
            {
                BuildRadialDarknessMesh(clearRadius, fadeBandRadius, requiredOuterRadius);

                lastCenter = center;
                lastClearRadius = clearRadius;
                lastFadeBandRadius = fadeBandRadius;
                lastDarkAlpha = darkAlpha;
                lastFadeSteepness = fadeSteepness;
                lastFadeSteps = fadeSteps;
                lastSegments = radialSegments;
                lastOuterRadius = requiredOuterRadius;
            }

            if (meshRenderer != null)
                meshRenderer.enabled = darkAlpha > 0.001f;
        }

        /// <summary>
        /// 함정 발견 전환은 조명의 clearRadius 안쪽에서만 발생시킵니다.
        ///
        /// 주의:
        /// HiddenTrapVisual2D.IsInsideForwardZone(collector)는 내부적으로 trap의 Collider2D를 사용할 수 있습니다.
        /// 화살/가시 함정처럼 감지용 Trigger Collider가 큰 프리팹은 실제 타일 중심이 r 밖에 있어도
        /// 큰 콜라이더 일부가 r 안에 걸려서 너무 일찍 reveal될 수 있습니다.
        /// 그래서 기본값은 revealByTileCenterOnly = true로 두고, trap.transform.position 기준으로만 판정합니다.
        /// </summary>
        private void RefreshHiddenTraps()
        {
            if (collector == null)
                return;

            for (int i = revealTraps.Count - 1; i >= 0; i--)
            {
                var trap = revealTraps[i];

                if (trap == null)
                {
                    revealTraps.RemoveAt(i);
                    continue;
                }

                bool insideVisibleZone = revealByTileCenterOnly
                    ? IsTrapCenterInsideClearRadius(trap)
                    : trap.IsInsideForwardZone(collector);

                trap.ApplyForwardZoneState(insideVisibleZone);
            }
        }

        /// <summary>
        /// 함정 타일의 중심점이 clearRadius 안에 들어왔는지만 검사합니다.
        /// 이 판정은 함정의 공격/감지 콜라이더 크기에 영향을 받지 않습니다.
        /// </summary>
        private bool IsTrapCenterInsideClearRadius(HiddenTrapVisual2D trap)
        {
            if (trap == null || collector == null)
                return false;

            Vector2 center = collector.ZoneCenter;
            Vector2 trapPos = trap.transform.position;

            float radius = collector.ClearRadius + Mathf.Max(0f, revealRadiusPadding);
            return (trapPos - center).sqrMagnitude <= radius * radius;
        }

        private void CreateMaskMeshObject(Transform parent)
        {
            maskRoot = new GameObject("RadialVision_DarkMaskRoot").transform;
            maskRoot.SetParent(parent != null ? parent : transform, false);
            maskRoot.localPosition = Vector3.zero;
            maskRoot.localRotation = Quaternion.identity;
            maskRoot.localScale = Vector3.one;

            meshFilter = maskRoot.gameObject.AddComponent<MeshFilter>();
            meshRenderer = maskRoot.gameObject.AddComponent<MeshRenderer>();

            maskMesh = new Mesh();
            maskMesh.name = "RadialVision_DarknessMesh";
            meshFilter.sharedMesh = maskMesh;

            maskMaterial = CreateMaskMaterial();
            meshRenderer.sharedMaterial = maskMaterial;
            meshRenderer.sortingOrder = maskSortingOrder;

            if (!string.IsNullOrWhiteSpace(maskSortingLayerName))
                meshRenderer.sortingLayerName = maskSortingLayerName;

            ApplyMaterialColor();
        }

        private Material CreateMaskMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");

            Material mat = new Material(shader);
            mat.name = "RadialVision_DarkMask_Material_Runtime";
            mat.renderQueue = 3000;

            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", Texture2D.whiteTexture);

            return mat;
        }

        /// <summary>
        /// 색 자체는 머티리얼 tint로, 투명도 변화는 vertex color alpha로 처리합니다.
        /// </summary>
        private void ApplyMaterialColor()
        {
            if (maskMaterial == null)
                return;

            Color tint = darkColor;
            tint.a = 1f;

            if (maskMaterial.HasProperty("_Color"))
                maskMaterial.color = tint;
        }

        /// <summary>
        /// 원형 링 Mesh를 만듭니다.
        /// clearRadius 안쪽에는 정점이 없어서 화면이 그대로 보입니다.
        /// clearRadius~outerLightRadius는 여러 개의 링으로 나누어 alpha를 점점 높입니다.
        /// outerLightRadius 바깥은 darkAlpha로 완전히 덮습니다.
        /// </summary>
        private void BuildRadialDarknessMesh(float clearRadius, float fadeBandRadius, float meshOuterRadius)
        {
            if (maskMesh == null)
                return;

            int segments = Mathf.Max(24, radialSegments);
            int steps = Mathf.Max(1, fadeSteps);

            clearRadius = Mathf.Max(0.01f, clearRadius);
            fadeBandRadius = Mathf.Max(0f, fadeBandRadius);
            meshOuterRadius = Mathf.Max(meshOuterRadius, clearRadius + fadeBandRadius + 0.1f);

            float[] radii;
            float[] alphas;

            if (fadeBandRadius <= 0.0001f)
            {
                // 감쇠 구간이 없으면 clearRadius 경계부터 바로 암전시킵니다.
                radii = new[] { clearRadius, meshOuterRadius };
                alphas = new[] { darkAlpha, darkAlpha };
            }
            else
            {
                // 0번 링은 투명, 마지막 전 링은 완전 암전 시작점,
                // 마지막 링은 맵 외곽까지 덮는 검은 영역입니다.
                radii = new float[steps + 2];
                alphas = new float[steps + 2];

                radii[0] = clearRadius;
                alphas[0] = 0f;

                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    radii[i] = clearRadius + fadeBandRadius * t;
                    alphas[i] = darkAlpha * EvaluateFalloff(t);
                }

                radii[steps + 1] = meshOuterRadius;
                alphas[steps + 1] = darkAlpha;
            }

            int ringIntervals = radii.Length - 1;
            int quadCount = segments * ringIntervals;

            Vector3[] vertices = new Vector3[quadCount * 4];
            Vector2[] uvs = new Vector2[vertices.Length];
            Color[] colors = new Color[vertices.Length];
            int[] triangles = new int[quadCount * 6];

            int quad = 0;

            for (int r = 0; r < ringIntervals; r++)
            {
                float innerRadius = radii[r];
                float outerRingRadius = radii[r + 1];
                float innerAlpha = Mathf.Clamp01(alphas[r]);
                float outerAlpha = Mathf.Clamp01(alphas[r + 1]);

                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * 360f / segments;
                    float a1 = (i + 1) * 360f / segments;

                    Vector2 d0 = DirectionFromAngle(a0);
                    Vector2 d1 = DirectionFromAngle(a1);

                    int vi = quad * 4;

                    vertices[vi + 0] = d0 * innerRadius;
                    vertices[vi + 1] = d1 * innerRadius;
                    vertices[vi + 2] = d1 * outerRingRadius;
                    vertices[vi + 3] = d0 * outerRingRadius;

                    uvs[vi + 0] = Vector2.zero;
                    uvs[vi + 1] = Vector2.zero;
                    uvs[vi + 2] = Vector2.one;
                    uvs[vi + 3] = Vector2.one;

                    // 머티리얼 색을 검은색으로 두고, vertex alpha만 이용해 어두움 정도를 조절합니다.
                    colors[vi + 0] = new Color(1f, 1f, 1f, innerAlpha);
                    colors[vi + 1] = new Color(1f, 1f, 1f, innerAlpha);
                    colors[vi + 2] = new Color(1f, 1f, 1f, outerAlpha);
                    colors[vi + 3] = new Color(1f, 1f, 1f, outerAlpha);

                    int ti = quad * 6;
                    triangles[ti + 0] = vi + 0;
                    triangles[ti + 1] = vi + 1;
                    triangles[ti + 2] = vi + 2;
                    triangles[ti + 3] = vi + 0;
                    triangles[ti + 4] = vi + 2;
                    triangles[ti + 5] = vi + 3;

                    quad++;
                }
            }

            maskMesh.Clear();
            maskMesh.vertices = vertices;
            maskMesh.uv = uvs;
            maskMesh.colors = colors;
            maskMesh.triangles = triangles;
            maskMesh.RecalculateBounds();
        }

        /// <summary>
        /// 0~1 구간의 감쇠 곡선입니다.
        /// fadeSteepness가 클수록 clearRadius 바깥에서 빠르게 검어집니다.
        /// </summary>
        private float EvaluateFalloff(float t)
        {
            t = Mathf.Clamp01(t);
            float steepness = Mathf.Max(0.01f, fadeSteepness);
            return 1f - Mathf.Pow(1f - t, steepness);
        }

        private float CalculateOuterRadius(MapData2D mapData, float cellSize)
        {
            if (mapData == null)
                return 30f;

            if (TryParseSize(mapData.size, out int w, out int h))
            {
                float width = Mathf.Max(1, w) * cellSize;
                float height = Mathf.Max(1, h) * cellSize;
                return Mathf.Sqrt(width * width + height * height);
            }

            if (mapData.grid != null && mapData.grid.Length > 0)
            {
                int minX = int.MaxValue;
                int minY = int.MaxValue;
                int maxX = int.MinValue;
                int maxY = int.MinValue;

                foreach (var cell in mapData.grid)
                {
                    if (cell == null) continue;
                    if (!TryParseCoord(cell.coord, out int x, out int y)) continue;

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }

                if (minX != int.MaxValue)
                {
                    float width = Mathf.Max(1, maxX - minX + 1) * cellSize;
                    float height = Mathf.Max(1, maxY - minY + 1) * cellSize;
                    return Mathf.Sqrt(width * width + height * height);
                }
            }

            return 30f;
        }

        private void ForceMeshRefresh()
        {
            lastCenter = new Vector2(float.NaN, float.NaN);
            lastClearRadius = -1f;
            lastFadeBandRadius = -1f;
            lastDarkAlpha = -1f;
            lastFadeSteepness = -1f;
            lastFadeSteps = -1;
            lastSegments = -1;
            lastOuterRadius = -1f;
        }

        private void ClearMaskObjects()
        {
            if (maskMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(maskMesh);
                else
                    DestroyImmediate(maskMesh);
            }

            if (maskMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(maskMaterial);
                else
                    DestroyImmediate(maskMaterial);
            }

            if (maskRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(maskRoot.gameObject);
                else
                    DestroyImmediate(maskRoot.gameObject);
            }

            maskRoot = null;
            meshFilter = null;
            meshRenderer = null;
            maskMesh = null;
            maskMaterial = null;
            ForceMeshRefresh();
        }

        private static Vector2 DirectionFromAngle(float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        private static bool TryParseCoord(string coord, out int x, out int y)
        {
            x = y = 0;

            coord = coord?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(coord) || coord.Length < 4)
                return false;

            try
            {
                x = Convert.ToInt32(coord.Substring(0, 2), 16);
                y = Convert.ToInt32(coord.Substring(2, 2), 16);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseSize(string size, out int w, out int h)
        {
            w = h = 0;

            size = size?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(size) || size.Length < 4)
                return false;

            try
            {
                w = Convert.ToInt32(size.Substring(0, 2), 16);
                h = Convert.ToInt32(size.Substring(2, 2), 16);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
