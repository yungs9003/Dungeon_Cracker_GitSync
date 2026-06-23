using System;
using System.Collections.Generic;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Map
{
    /// <summary>
    /// 플레이어 ForwardZone 밖을 하나의 투명 Mesh로 덮습니다.
    /// ForwardZoneCollector가 계산한 tangent-outline 기반 시야 외곽선을 그대로 사용합니다.
    /// </summary>
    public class ForwardZoneVisionMask2D : MonoBehaviour
    {
        [Header("Forward Zone")]
        [SerializeField] private ForwardZoneCollector collector;

        [Header("Dark Mask")]
        [SerializeField, Range(0f, 1f)] private float darkAlpha = 0.75f;
        [SerializeField] private Color darkColor = Color.black;
        [SerializeField] private int maskSortingOrder = 5000;
        [SerializeField] private string maskSortingLayerName = "";

        [Tooltip("마스크 외곽 원의 부드러움입니다. 96~192 정도를 추천합니다.")]
        [SerializeField, Range(24, 360)] private int radialSegments = 144;

        [Tooltip("맵 전체를 충분히 덮기 위해 외곽 반지름에 더하는 여유값입니다.")]
        [SerializeField] private float outerRadiusPadding = 6f;

        [Tooltip("호환용 옵션입니다. 이 버전은 타일별 사각형이 아니라 하나의 Mesh 마스크를 사용하므로 실제 동작에는 영향이 거의 없습니다.")]
        [SerializeField] private bool coverOnlyExistingGridCells = true;

        [Header("Trap Reveal")]
        [Tooltip("켜져 있으면 한 번 발견된 숨김 타일은 ForwardZone 밖으로 나가도 계속 실제 모습으로 유지됩니다.")]
        [SerializeField] private bool stayRevealedAfterSeen = true;

        private readonly List<HiddenTrapVisual2D> hiddenTraps = new();

        private Transform maskRoot;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh maskMesh;
        private Material maskMaterial;

        private float outerRadius = 20f;
        private Vector2 lastCenter = new Vector2(float.NaN, float.NaN);
        private Vector2 lastForward = new Vector2(float.NaN, float.NaN);
        private float lastDarkAlpha = -1f;
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

            foreach (var trap in hiddenTraps)
            {
                if (trap != null)
                    trap.SetStayRevealedAfterSeen(value);
            }
        }

        public void ClearRegisteredTraps()
        {
            hiddenTraps.Clear();
        }

        public void RegisterTrap(HiddenTrapVisual2D trap)
        {
            if (trap == null) return;
            if (hiddenTraps.Contains(trap)) return;

            trap.SetStayRevealedAfterSeen(stayRevealedAfterSeen);
            hiddenTraps.Add(trap);
        }

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
            Vector2 forward = collector.CurrentForwardDirection;

            maskRoot.position = new Vector3(center.x, center.y, transform.position.z);

            bool needRebuild =
                Vector2.Distance(center, lastCenter) > 0.001f ||
                Vector2.Distance(forward, lastForward) > 0.001f ||
                !Mathf.Approximately(lastDarkAlpha, darkAlpha) ||
                !Mathf.Approximately(lastOuterRadius, outerRadius) ||
                lastSegments != radialSegments ||
                meshFilter.sharedMesh == null;

            if (needRebuild)
            {
                BuildDarknessMesh();
                lastCenter = center;
                lastForward = forward;
                lastDarkAlpha = darkAlpha;
                lastSegments = radialSegments;
                lastOuterRadius = outerRadius;
            }

            if (meshRenderer != null)
                meshRenderer.enabled = darkAlpha > 0.001f;
        }

        private void RefreshHiddenTraps()
        {
            if (collector == null)
                return;

            for (int i = hiddenTraps.Count - 1; i >= 0; i--)
            {
                var trap = hiddenTraps[i];

                if (trap == null)
                {
                    hiddenTraps.RemoveAt(i);
                    continue;
                }

                bool insideForwardZone = trap.IsInsideForwardZone(collector);
                trap.ApplyForwardZoneState(insideForwardZone);
            }
        }

        private void CreateMaskMeshObject(Transform parent)
        {
            maskRoot = new GameObject("ForwardZone_DarkMaskRoot").transform;
            maskRoot.SetParent(parent != null ? parent : transform, false);
            maskRoot.localPosition = Vector3.zero;
            maskRoot.localRotation = Quaternion.identity;
            maskRoot.localScale = Vector3.one;

            meshFilter = maskRoot.gameObject.AddComponent<MeshFilter>();
            meshRenderer = maskRoot.gameObject.AddComponent<MeshRenderer>();

            maskMesh = new Mesh();
            maskMesh.name = "ForwardZone_DarknessMesh";
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
            mat.name = "ForwardZone_DarkMask_Material_Runtime";
            mat.renderQueue = 3000;

            Texture2D whiteTex = Texture2D.whiteTexture;
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", whiteTex);

            return mat;
        }

        private void ApplyMaterialColor()
        {
            if (maskMaterial == null)
                return;

            Color c = darkColor;
            c.a = darkAlpha;

            if (maskMaterial.HasProperty("_Color"))
                maskMaterial.color = c;
        }

        /// <summary>
        /// 중심 기준 외곽 원과 ForwardZone outline 사이를 링 형태로 채워서 어두운 영역을 만듭니다.
        /// ForwardZone의 경계 반지름은 collector.GetAllowedRadiusForWorldDirection()이 계산합니다.
        /// </summary>
        private void BuildDarknessMesh()
        {
            if (maskMesh == null || collector == null)
                return;

            int segments = Mathf.Max(24, radialSegments);

            Vector3[] vertices = new Vector3[segments * 4];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];
            Color[] colors = new Color[vertices.Length];

            Color c = darkColor;
            c.a = darkAlpha;

            for (int i = 0; i < segments; i++)
            {
                float a0 = i * 360f / segments;
                float a1 = (i + 1) * 360f / segments;

                Vector2 d0 = DirectionFromAngle(a0);
                Vector2 d1 = DirectionFromAngle(a1);

                float inner0 = collector.GetAllowedRadiusForWorldDirection(d0);
                float inner1 = collector.GetAllowedRadiusForWorldDirection(d1);

                int vi = i * 4;
                vertices[vi + 0] = d0 * inner0;
                vertices[vi + 1] = d1 * inner1;
                vertices[vi + 2] = d1 * outerRadius;
                vertices[vi + 3] = d0 * outerRadius;

                uvs[vi + 0] = Vector2.zero;
                uvs[vi + 1] = Vector2.zero;
                uvs[vi + 2] = Vector2.one;
                uvs[vi + 3] = Vector2.one;

                colors[vi + 0] = c;
                colors[vi + 1] = c;
                colors[vi + 2] = c;
                colors[vi + 3] = c;

                int ti = i * 6;
                triangles[ti + 0] = vi + 0;
                triangles[ti + 1] = vi + 1;
                triangles[ti + 2] = vi + 2;
                triangles[ti + 3] = vi + 0;
                triangles[ti + 4] = vi + 2;
                triangles[ti + 5] = vi + 3;
            }

            maskMesh.Clear();
            maskMesh.vertices = vertices;
            maskMesh.uv = uvs;
            maskMesh.colors = colors;
            maskMesh.triangles = triangles;
            maskMesh.RecalculateBounds();
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
            lastForward = new Vector2(float.NaN, float.NaN);
            lastDarkAlpha = -1f;
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
