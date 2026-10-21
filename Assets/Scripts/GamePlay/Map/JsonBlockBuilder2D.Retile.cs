using System;
using System.Collections.Generic;
using Dungeon.Core;
using UnityEngine;

namespace Dungeon.Map
{
    public partial class JsonBlockBuilder2D
    {
        [Header("파괴 후 주변 벽 갱신 (시험 적용)")]
        [Tooltip("끄면 기존 프리팹 교체 방식을 사용합니다. 다시 켠 뒤 맵을 새로 생성하세요.")]
        public bool enableTileRetiling = true;
        public string tileRulesResourcePath = "Data/TileRules";
        public TextAsset tileRulesOverride;
        public bool logTileRetileResults;

        private sealed class TileBinding
        {
            public GameObject instance;
            public CellData2D cell;
            public bool floor3;
        }
        [Serializable] private sealed class TrapCatalog { public TrapEntry[] traps; }
        [Serializable] private sealed class TrapEntry { public string code; public bool destructible; }

        private readonly Dictionary<GameObject, TileBinding> retileBindings = new();
        private readonly Dictionary<string, bool> trapDestructibility = new(StringComparer.OrdinalIgnoreCase);
        private TileRetileSolver2D retileSolver;
        private bool retileRulesAttempted;

        private void ResetTileRetileState()
        {
            foreach (var binding in retileBindings.Values)
                if (!binding.floor3 && BlockRegistry.Instance != null)
                    BlockRegistry.Instance.Unregister(binding.cell.coord, binding.instance);
            retileBindings.Clear();
            retileSolver = null;
            retileRulesAttempted = false;
            LoadedMapData = null;
            trapDestructibility.Clear();
            var json = Resources.Load<TextAsset>("Data/db");
            if (json != null)
                foreach (var trap in JsonUtility.FromJson<TrapCatalog>(json.text)?.traps ?? Array.Empty<TrapEntry>())
                    if ((trap.code ?? "").StartsWith("101-", StringComparison.OrdinalIgnoreCase))
                        trapDestructibility[trap.code] = trap.destructible;
        }

        private void RegisterTileForRetile(GameObject block, CellData2D cell, bool floor3)
        {
            retileBindings[block] = new TileBinding { instance = block, cell = cell, floor3 = floor3 };
            if (!floor3 && MapArea != null && MapArea.enabled) MapArea.RegisterCell(cell.coord, cell.code);
        }

        /// <summary>기존 해체 경로의 대체 타일도 원래 레이어/좌표와 맵 소유권을 이어받는다.</summary>
        public void RegisterLegacyReplacement(GameObject destroyedRoot, GameObject replacement, string replacementCode)
        {
            if (replacement == null || destroyedRoot == null ||
                !retileBindings.TryGetValue(destroyedRoot, out var binding)) return;
            if (!TileRetileSolver2D.TryParseCoord(binding.cell.coord, out int x, out int y)) return;
            replacement.transform.SetParent(transform, true);
            replacement.name = $"{(binding.floor3 ? "3F_" : "")}{replacementCode}_{x:X2}{y:X2}";
            ApplySortingBand(replacement, binding.floor3 ? floor3SortingOrderBase : gridSortingOrderBase);
            ApplyBlockCoordAndRegistry(replacement, x, y, !binding.floor3);
            retileBindings.Remove(destroyedRoot);
            // 102- 등 기존 경로의 MapData/해체 정책은 유지하고 실물 바인딩만 교체한다.
            RegisterTileForRetile(replacement, binding.cell, binding.floor3);
        }

        private bool EnsureRetileSolver()
        {
            if (retileRulesAttempted) return retileSolver != null;
            retileRulesAttempted = true;
            try
            {
                var json = tileRulesOverride != null ? tileRulesOverride : Resources.Load<TextAsset>(tileRulesResourcePath);
                var allowed = new List<string>();
                if (GameDatabase.Instance != null)
                {
                    foreach (var trap in GameDatabase.Instance.Traps)
                        if ((trap.code ?? "").StartsWith("101-", StringComparison.OrdinalIgnoreCase) && trap.destructible == true)
                            allowed.Add(trap.code);
                }
                else foreach (var trap in trapDestructibility)
                    if (trap.Value) allowed.Add(trap.Key);
                retileSolver = new TileRetileSolver2D(TileRetileSolver2D.LoadRules(json), allowed);
            }
            catch (Exception error) { Debug.LogError($"[TileRetile] 규칙 로드 실패. 함정은 유지합니다: {error.Message}", this); }
            return retileSolver != null;
        }

        /// <summary>기존 해체 액션이 호출한다. 계산·필수 리소스 확인 후 MapData와 실물에 한 번 적용한다.</summary>
        public TileDestructionHandling TryHandleTileDestruction(GameObject destroyedRoot)
        {
            if (!enableTileRetiling || destroyedRoot == null || LoadedMapData == null ||
                !retileBindings.TryGetValue(destroyedRoot, out var binding)) return TileDestructionHandling.NotHandled;
            string code = binding.cell.code?.Trim();
            if (!(code ?? "").StartsWith("101-", StringComparison.OrdinalIgnoreCase)) return TileDestructionHandling.NotHandled;
            if (GameDatabase.Instance != null && GameDatabase.Instance.TryGetTrap(code, out var definition))
            {
                if (definition.destructible == false) return TileDestructionHandling.Rejected;
            }
            else if (trapDestructibility.TryGetValue(code ?? "", out bool destructible) && !destructible)
                return TileDestructionHandling.Rejected;

            var layer = binding.floor3 ? LoadedMapData.floor3 : LoadedMapData.grid;
            if (!EnsureRetileSolver()) return TileDestructionHandling.Rejected;
            // 3F는 별도 레이어로 계산하고 기본 grid/Registry를 덮어쓰지 않는다.
            var plan = retileSolver.DisarmTrapAndResolve(new MapData2D { grid = layer }, binding.cell.coord);
            if (plan.Count == 0) return TileDestructionHandling.Rejected;

            var replacementPrefab = LoadPrefab(plan[0].newCode);
            var wallPrefab = LoadPrefab("001-001");
            var wallVariants = FindAssetVariant(wallPrefab);
            if (replacementPrefab == null || (plan.Count > 1 && wallVariants == null))
            { Debug.LogError("[TileRetile] 바닥/벽 프리팹이 없습니다. 함정을 유지합니다.", this); return TileDestructionHandling.Rejected; }
            var sameLayer = new Dictionary<string, TileBinding>(StringComparer.OrdinalIgnoreCase);
            foreach (var other in retileBindings.Values)
                if (other.floor3 == binding.floor3) sameLayer[other.cell.coord.Trim()] = other;
            foreach (var result in plan)
                if (!sameLayer.TryGetValue(result.coord, out var target) || target.instance == null ||
                    (result != plan[0] && (wallVariants == null || !wallVariants.HasVariant(result.newAssetNo) ||
                        (!result.wallVisualOnly && (FindAssetVariant(target.instance) == null || !FindAssetVariant(target.instance).HasVariant(result.newAssetNo))))))
                { Debug.LogError("[TileRetile] 적용 대상/벽 Sprite가 없어 함정을 유지합니다.", this); return TileDestructionHandling.Rejected; }

            var first = plan[0];
            var newCell = new CellData2D { coord = first.coord, code = first.newCode, assetNo = first.newAssetNo, direction = first.newDirection };
            TileRetileSolver2D.TryParseCoord(first.coord, out int x, out int y);
            var replacement = CreateTileInstance(replacementPrefab, first.newCode, newCell, x, y);
            if (binding.floor3) replacement.name = "3F_" + replacement.name;
            ApplyAssetVariant(replacement, first.newAssetNo);
            ApplySortingBand(replacement, binding.floor3 ? floor3SortingOrderBase : gridSortingOrderBase);
            ApplyBlockCoordAndRegistry(replacement, x, y, !binding.floor3);
            binding.cell.code = first.newCode;
            binding.cell.assetNo = first.newAssetNo;
            binding.cell.direction = first.newDirection;
            retileBindings.Remove(destroyedRoot);
            RegisterTileForRetile(replacement, binding.cell, binding.floor3);
            for (int i = 1; i < plan.Count; i++)
            {
                var result = plan[i];
                var target = sameLayer[result.coord];
                var rotation = Quaternion.Euler(0, 0, GetZRotationFromDir(result.newDirection));
                if (result.wallVisualOnly)
                    ApplyTrapWallVisual(target.instance, wallVariants, result.newAssetNo, rotation);
                else
                {
                    ApplyAssetVariant(target.instance, result.newAssetNo);
                    target.instance.transform.rotation = rotation;
                    target.cell.assetNo = result.newAssetNo;
                    target.cell.direction = result.newDirection;
                }
            }
            // 비전 마스크는 맵 범위만 사용하므로 재생성할 필요가 없다. 기존 발견 상태를 유지한다.
            if (logTileRetileResults)
                foreach (var result in plan)
                    Debug.Log($"[TileRetile] {result.coord}: {result.newCode} asset={result.newAssetNo} dir={result.newDirection} visualOnly={result.wallVisualOnly} {result.reason}", this);
            return TileDestructionHandling.Applied;
        }

        private void ApplyTrapWallVisual(GameObject target, AssetVariantSprite2D walls, int asset, Quaternion rotation)
        {
            Sprite sprite = MapSpriteThemeCatalog.ResolveDefault(
                walls.GetSpriteForAssetNo(asset, false), CurrentSpriteTheme);
            var hidden = target.GetComponent<HiddenTrapVisual2D>();
            if (hidden != null) { hidden.OverrideHiddenWallSprite(sprite, rotation); return; }
            // 항상 보이는 밀치기/당기기의 별도 WallVisual만 바꾸고 공격 루트는 회전하지 않는다.
            foreach (var renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.transform != target.transform && renderer.name == "WallVisual")
                { renderer.sprite = sprite; renderer.transform.rotation = rotation; }
        }
    }
}
