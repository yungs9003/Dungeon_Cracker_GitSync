using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.Map
{
    /// <summary>실제 생성한 기본 grid의 영역과 외곽/빈 셀 경계. 벽/함정 통행 정책은 기존 타일에 맡깁니다.</summary>
    [DisallowMultipleComponent]
    public sealed class MapArea2D : MonoBehaviour
    {
        public const string BoundaryLayerName = "MapBoundary";
        private const float Skin = 0.02f;
        private static readonly Dictionary<Rigidbody2D, MapArea2D> owners = new();
        private readonly Dictionary<Vector2Int, string> cells = new();
        private readonly RaycastHit2D[] hits = new RaycastHit2D[128];
        private GameObject boundaryRoot;
        private float cellSize = 1f;
        private int boundaryLayer;
        private bool dirty, built;
        public int CellCount => cells.Count;
        public int BoundaryCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOwners() => owners.Clear();

        public static MapArea2D ForBody(Rigidbody2D body) => body != null && owners.TryGetValue(body, out var area)
            && area != null && area.isActiveAndEnabled ? area : null;

        public void Initialize(float size, Transform player)
        {
            Clear();
            cellSize = Mathf.Max(0.01f, size);
            boundaryLayer = LayerMask.NameToLayer(BoundaryLayerName);
            if (boundaryLayer < 0) Debug.LogError("[MapArea] MapBoundary 레이어가 없습니다.", this);
            var body = player != null ? player.GetComponentInParent<Rigidbody2D>() : null;
            if (body != null) owners[body] = this;
        }

        public void Clear()
        {
            cells.Clear(); dirty = false; built = false; BoundaryCount = 0;
            RemoveBoundary();
        }

        public void RegisterCell(string coord, string code)
        {
            if (!TileRetileSolver2D.TryParseCoord(coord, out int x, out int y)) return;
            var cell = new Vector2Int(x, y);
            if (!cells.ContainsKey(cell)) dirty = true;
            cells[cell] = code;
        }

        public bool ContainsWorld(Vector2 point) => cells.ContainsKey(WorldToCell(point));
        public Vector2Int WorldToCell(Vector2 point) => new(Mathf.FloorToInt(point.x / cellSize + 0.5f), Mathf.FloorToInt(point.y / cellSize + 0.5f));
        public Vector2 CellCenter(Vector2Int cell) => (Vector2)cell * cellSize;

        public void BuildBoundary()
        {
            RemoveBoundary(); dirty = false; built = true; BoundaryCount = 0;
            if (cells.Count == 0 || boundaryLayer < 0) return;
            boundaryRoot = new GameObject("MapBoundary");
            boundaryRoot.layer = boundaryLayer;
            boundaryRoot.transform.SetParent(transform, false);
            boundaryRoot.transform.position = Vector3.zero;
            boundaryRoot.transform.rotation = Quaternion.identity;
            var scale = transform.lossyScale;
            boundaryRoot.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            var edges = new Dictionary<(bool horizontal, int line, int side), SortedSet<int>>();
            void Add(bool horizontal, int line, int side, int start)
            {
                var key = (horizontal, line, side);
                if (!edges.TryGetValue(key, out var starts)) edges[key] = starts = new SortedSet<int>();
                starts.Add(start);
            }
            foreach (var cell in cells.Keys)
            {
                if (!cells.ContainsKey(cell + Vector2Int.up)) Add(true, cell.y * 2 + 1, 1, cell.x);
                if (!cells.ContainsKey(cell + Vector2Int.down)) Add(true, cell.y * 2 - 1, -1, cell.x);
                if (!cells.ContainsKey(cell + Vector2Int.right)) Add(false, cell.x * 2 + 1, 1, cell.y);
                if (!cells.ContainsKey(cell + Vector2Int.left)) Add(false, cell.x * 2 - 1, -1, cell.y);
            }
            foreach (var edge in edges)
            {
                int start = int.MinValue, end = int.MinValue;
                foreach (int next in edge.Value)
                {
                    if (start == int.MinValue) start = end = next;
                    else if (next == end + 1) end = next;
                    else { AddCollider(edge.Key, start, end); start = end = next; }
                }
                if (start != int.MinValue) AddCollider(edge.Key, start, end);
            }
            Physics2D.SyncTransforms();
        }

        private void AddCollider((bool horizontal, int line, int side) edge, int start, int end)
        {
            float thickness = cellSize * 0.5f;
            float fixedPosition = edge.line * cellSize * 0.5f + edge.side * thickness * 0.5f;
            float along = (start + end) * cellSize * 0.5f;
            var collider = boundaryRoot.AddComponent<BoxCollider2D>();
            collider.offset = edge.horizontal ? new Vector2(along, fixedPosition) : new Vector2(fixedPosition, along);
            collider.size = edge.horizontal ? new Vector2((end - start + 1) * cellSize, thickness) : new Vector2(thickness, (end - start + 1) * cellSize);
            BoundaryCount++;
        }

        public Vector2 LimitMovement(Rigidbody2D body, Vector2 from, Vector2 requested, bool slide = true)
        {
            if (cells.Count == 0 || boundaryLayer < 0 || !ContainsWorld(from)) return from;
            Footprint(body, out Vector2 size, out Vector2 offset);
            Vector2 position = from, remaining = requested - from;
            var filter = new ContactFilter2D(); filter.SetLayerMask(1 << boundaryLayer); filter.useTriggers = false;
            for (int step = 0; step < (slide ? 3 : 1) && remaining.sqrMagnitude > 0.00000001f; step++)
            {
                float distance = remaining.magnitude;
                Vector2 direction = remaining / distance, normal = Vector2.zero;
                float allowed = distance;
                int count = Physics2D.BoxCast(position + offset, size, 0f, direction, filter, hits, distance + Skin);
                if (count == hits.Length) return position;
                for (int i = 0; i < count; i++)
                {
                    if (hits[i].collider == null || hits[i].collider.gameObject != boundaryRoot || Vector2.Dot(hits[i].normal, direction) >= -0.0001f) continue;
                    float travel = Mathf.Max(0f, hits[i].distance - Skin);
                    if (travel < allowed) { allowed = travel; normal = hits[i].normal; }
                }
                position += direction * allowed;
                if (normal == Vector2.zero) break;
                remaining = direction * (distance - allowed);
                remaining -= normal * Mathf.Min(0f, Vector2.Dot(remaining, normal));
            }
            return position;
        }

        public bool TryResolveRespawnPoint(Rigidbody2D body, Vector2 requested, out Vector2 point)
        {
            point = requested;
            if (Fits(body, requested)) return true;
            float best = float.PositiveInfinity;
            foreach (var cell in cells)
            {
                if (cell.Value != "000-000") continue;
                Vector2 candidate = CellCenter(cell.Key);
                float distance = (candidate - requested).sqrMagnitude;
                if (distance >= best || !Fits(body, candidate)) continue;
                best = distance; point = candidate;
            }
            return !float.IsPositiveInfinity(best);
        }

        private bool Fits(Rigidbody2D body, Vector2 point)
        {
            Footprint(body, out Vector2 size, out Vector2 offset);
            Vector2 min = point + offset - size * 0.5f + Vector2.one * 0.0001f;
            Vector2 max = point + offset + size * 0.5f - Vector2.one * 0.0001f;
            var low = WorldToCell(min); var high = WorldToCell(max);
            for (int y = low.y; y <= high.y; y++)
                for (int x = low.x; x <= high.x; x++)
                    if (!cells.ContainsKey(new Vector2Int(x, y))) return false;
            return cells.Count > 0;
        }

        private static void Footprint(Rigidbody2D body, out Vector2 size, out Vector2 offset)
        {
            bool found = false; Bounds bounds = default;
            if (body != null)
                foreach (var collider in body.GetComponentsInChildren<Collider2D>())
                {
                    if (!collider.enabled || collider.isTrigger || collider.attachedRigidbody != body) continue;
                    if (!found) { bounds = collider.bounds; found = true; } else bounds.Encapsulate(collider.bounds);
                }
            size = found ? (Vector2)bounds.size : Vector2.one * 0.6f;
            offset = found ? (Vector2)bounds.center - body.position : Vector2.zero;
            size = Vector2.Max(size, Vector2.one * 0.01f);
        }

        private void LateUpdate() { if (built && dirty) BuildBoundary(); }
        private void RemoveBoundary()
        {
            if (boundaryRoot == null) return;
            boundaryRoot.SetActive(false);
            if (Application.isPlaying) Destroy(boundaryRoot); else DestroyImmediate(boundaryRoot);
            boundaryRoot = null;
        }
        private void OnEnable() { if (boundaryRoot != null) boundaryRoot.SetActive(true); }
        private void OnDisable() { if (boundaryRoot != null) boundaryRoot.SetActive(false); }
        private void OnDestroy()
        {
            var remove = new List<Rigidbody2D>();
            foreach (var pair in owners) if (pair.Value == this) remove.Add(pair.Key);
            foreach (var body in remove) owners.Remove(body);
        }
    }
}
