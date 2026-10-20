using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.Map
{
    [Serializable] public sealed class TileReplacementRule
    {
        public string pre, prePrefix, post, tilesetCode;
        public bool requireDestructible;
        public string[] extraWallLikeCodes, extraWallLikePrefixes;
    }
    [Serializable] public sealed class SideProfileCodeRule
    {
        public string code, name;
        public string[] compatibleWith;
    }
    [Serializable] public sealed class StructureMaskEntry
    {
        public string[] mask3x3;
        public int[] candidates;
    }
    [Serializable] public sealed class TileRuleEntry
    {
        public int assetNo;
        public string structureType, signature;
    }
    [Serializable] public sealed class TileRulesetData
    {
        public string tilesetCode;
        public StructureMaskEntry[] structureMasks;
        public TileRuleEntry[] tiles;
    }
    [Serializable] public sealed class TileRulesRoot
    {
        public int schemaVersion;
        public string signatureOrder;
        public bool missingCellsAreWalls;
        public TileReplacementRule[] TileReplacements;
        public SideProfileCodeRule[] sideProfileCodes;
        public TileRulesetData[] rulesets;
    }
    public sealed class RetileResult2D
    {
        public string coord, oldCode, oldDirection, newCode, newDirection, reason;
        public int oldAssetNo, newAssetNo;
        // 101 함정의 assetNo/direction은 공격·발견용 값이므로 벽 외형과 구분한다.
        public bool wallVisualOnly;
    }
    public enum TileDestructionHandling { NotHandled, Applied, Rejected }

    /// <summary>
    /// 원본 MapData를 수정하지 않고 제거 후 구조만 읽어 결과를 계산한다.
    /// 이웃의 기존 assetNo/direction은 선택 점수에 사용하지 않는다.
    /// </summary>
    public sealed class TileRetileSolver2D
    {
        private static readonly string[] Directions = { "00", "01", "11", "10" };
        private readonly TileRulesRoot rules;
        private readonly Dictionary<string, TileRulesetData> sets = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> destructibleCodes;

        public static TileRulesRoot LoadRules(TextAsset json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            return JsonUtility.FromJson<TileRulesRoot>(json.text);
        }

        public TileRetileSolver2D(TileRulesRoot rules, IEnumerable<string> destructibleTrapCodes = null)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            destructibleCodes = new HashSet<string>(destructibleTrapCodes ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (rules.schemaVersion != 2 || rules.signatureOrder != "NESW")
                throw new ArgumentException("TileRules는 schemaVersion 2 / signatureOrder NESW여야 합니다.");
            foreach (var set in rules.rulesets ?? Array.Empty<TileRulesetData>())
            {
                if (set == null || string.IsNullOrWhiteSpace(set.tilesetCode) || sets.ContainsKey(set.tilesetCode))
                    throw new ArgumentException("중복 또는 비어 있는 tilesetCode입니다.");
                var assets = new Dictionary<int, TileRuleEntry>();
                foreach (var tile in set.tiles ?? Array.Empty<TileRuleEntry>())
                {
                    if (tile == null || assets.ContainsKey(tile.assetNo) || tile.signature?.Length != 8)
                        throw new ArgumentException("잘못된 벽 assetNo/signature입니다.");
                    assets.Add(tile.assetNo, tile);
                }
                foreach (var mask in set.structureMasks ?? Array.Empty<StructureMaskEntry>())
                {
                    if (mask?.mask3x3?.Length != 3 || mask.candidates == null || mask.candidates.Length == 0)
                        throw new ArgumentException("잘못된 3x3 마스크입니다.");
                    foreach (string row in mask.mask3x3)
                        if (row?.Length != 3 || Array.Exists(row.ToCharArray(), c => c != '0' && c != '1' && c != '?'))
                            throw new ArgumentException("마스크는 0/1/? 세 글자씩 세 줄이어야 합니다.");
                    foreach (int asset in mask.candidates)
                    {
                        if (!assets.TryGetValue(asset, out var tile)) throw new ArgumentException("마스크 후보 에셋이 없습니다.");
                        // 잘못된 면 순서가 다시 들어오면 로딩 단계에서 발견한다.
                        int[] sides = { 1, 5, 7, 3 };
                        for (int i = 0; i < 4; i++)
                        {
                            char occupancy = mask.mask3x3[sides[i] / 3][sides[i] % 3];
                            bool connected = tile.signature.Substring(i * 2, 2) != "00";
                            if (occupancy == '?' || (occupancy == '1') != connected)
                                throw new ArgumentException($"asset {asset}의 마스크와 NESW signature가 다릅니다.");
                        }
                    }
                }
                sets.Add(set.tilesetCode, set);
            }
            foreach (var replacement in rules.TileReplacements ?? Array.Empty<TileReplacementRule>())
                if (replacement == null || !sets.ContainsKey(replacement.tilesetCode ?? "") ||
                    string.IsNullOrWhiteSpace(replacement.post) ||
                    (string.IsNullOrWhiteSpace(replacement.pre) && string.IsNullOrWhiteSpace(replacement.prePrefix)))
                    throw new ArgumentException("잘못된 교체 규칙입니다.");
        }

        public bool TryGetReplacement(string code, out TileReplacementRule replacement)
        {
            replacement = null;
            // 정확한 코드 규칙을 prefix 규칙보다 우선한다.
            foreach (var entry in rules.TileReplacements ?? Array.Empty<TileReplacementRule>())
                if (!string.IsNullOrEmpty(entry.pre) && Equal(code, entry.pre) &&
                    (!entry.requireDestructible || destructibleCodes.Contains(code))) { replacement = entry; return true; }
            foreach (var entry in rules.TileReplacements ?? Array.Empty<TileReplacementRule>())
                if (!string.IsNullOrEmpty(entry.prePrefix) && (code ?? "").StartsWith(entry.prePrefix, StringComparison.OrdinalIgnoreCase) &&
                    (!entry.requireDestructible || destructibleCodes.Contains(code)))
                { replacement = entry; return true; }
            return false;
        }

        public static bool IsWallCode(string code, TileReplacementRule replacement)
        {
            if (Equal(code, replacement.tilesetCode)) return true;
            foreach (var item in replacement.extraWallLikeCodes ?? Array.Empty<string>())
                if (Equal(code, item)) return true;
            foreach (var prefix in replacement.extraWallLikePrefixes ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(prefix) && (code ?? "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public List<RetileResult2D> DisarmTrapAndResolve(MapData2D map, string centerCoord)
        {
            var results = new List<RetileResult2D>();
            var cells = new Dictionary<string, CellData2D>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in map?.grid ?? Array.Empty<CellData2D>())
                if (cell != null && TryParseCoord(cell.coord, out int x, out int y)) cells[Coord(x, y)] = cell;
            if (!TryParseCoord(centerCoord, out int cx, out int cy) || !cells.TryGetValue(Coord(cx, cy), out var center) ||
                !TryGetReplacement(center.code, out var replacement)) return results;

            results.Add(Result(center, replacement.post, 0, "00", false, "replacement"));
            var set = sets[replacement.tilesetCode];
            bool Wall(int x, int y)
            {
                if (x == cx && y == cy) return IsWallCode(replacement.post, replacement);
                if (x < 0 || y < 0 || x > 255 || y > 255 || !cells.TryGetValue(Coord(x, y), out var cell))
                    return rules.missingCellsAreWalls;
                return IsWallCode(cell.code, replacement);
            }
            for (int dy = 1; dy >= -1; dy--)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x > 255 || y > 255 || !cells.TryGetValue(Coord(x, y), out var cell) ||
                        !IsWallCode(cell.code, replacement)) continue;
                    var actual = new string[3];
                    for (int r = 0; r < 3; r++)
                    {
                        var row = new char[3];
                        for (int c = 0; c < 3; c++) row[c] = Wall(x + c - 1, y + 1 - r) ? '1' : '0';
                        actual[r] = new string(row);
                    }
                    var choice = ResolveWall(cell, actual, set, !Equal(cell.code, set.tilesetCode));
                    if (choice != null) results.Add(choice);
                }
            return results;
        }

        private static RetileResult2D ResolveWall(CellData2D cell, string[] actual, TileRulesetData set, bool visualOnly)
        {
            // 전용 에셋이 없는 고립 벽은 기존 표시를 유지한다. 맵 제작에서는 제외한 배치다.
            if (actual[0][1] == '0' && actual[1][2] == '0' && actual[2][1] == '0' && actual[1][0] == '0') return null;
            int bestMiss = int.MaxValue, bestSpecificity = -1, bestAsset = int.MaxValue, bestRotation = 0;
            foreach (var mask in set.structureMasks)
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    var pattern = RotateMask(mask.mask3x3, rotation);
                    // 상하좌우의 개방 방향은 반드시 일치시키고, 전용 그림이 없는 대각 배치만 근사한다.
                    if (actual[0][1] != pattern[0][1] || actual[1][2] != pattern[1][2] ||
                        actual[2][1] != pattern[2][1] || actual[1][0] != pattern[1][0]) continue;
                    int misses = 0, specificity = 0;
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 3; c++)
                            if (pattern[r][c] != '?') { specificity++; if (pattern[r][c] != actual[r][c]) misses++; }
                    foreach (int asset in mask.candidates)
                        if (misses < bestMiss || (misses == bestMiss && (specificity > bestSpecificity ||
                            (specificity == bestSpecificity && (asset < bestAsset || (asset == bestAsset && rotation < bestRotation))))))
                        { bestMiss = misses; bestSpecificity = specificity; bestAsset = asset; bestRotation = rotation; }
                }
            return bestAsset == int.MaxValue ? null : Result(cell, cell.code, bestAsset, Directions[bestRotation], visualOnly,
                bestMiss == 0 ? "structure-mask" : $"structure-nearest-diagonal:{bestMiss}");
        }

        private static RetileResult2D Result(CellData2D cell, string code, int asset, string direction, bool visualOnly, string reason) =>
            new() { coord = cell.coord.Trim().ToLowerInvariant(), oldCode = cell.code, oldAssetNo = cell.assetNo,
                oldDirection = cell.direction, newCode = code, newAssetNo = asset, newDirection = direction,
                wallVisualOnly = visualOnly, reason = reason };

        public static string[] RotateMask(string[] mask, int turns)
        {
            var result = (string[])mask.Clone();
            for (int i = 0; i < (turns % 4 + 4) % 4; i++)
            {
                var next = new string[3];
                for (int r = 0; r < 3; r++) next[r] = new string(new[] { result[2][r], result[1][r], result[0][r] });
                result = next;
            }
            return result;
        }
        public static bool TryParseCoord(string coord, out int x, out int y)
        {
            x = y = 0; coord = coord?.Trim();
            return coord?.Length == 4 && int.TryParse(coord.Substring(0, 2), System.Globalization.NumberStyles.HexNumber,
                null, out x) && int.TryParse(coord.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out y);
        }
        private static string Coord(int x, int y) => $"{x:x2}{y:x2}";
        private static bool Equal(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
