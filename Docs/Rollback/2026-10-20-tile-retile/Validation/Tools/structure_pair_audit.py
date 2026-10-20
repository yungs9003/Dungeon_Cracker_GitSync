"""Structure-only adjacent-pair diagnostic. Does not modify game assets."""
import itertools
import json
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
RULES = json.loads((ROOT / 'Assets/Resources/Data/TileRules.json').read_text())
MASKS = RULES['rulesets'][0]['structureMasks']
TILES = {tile['assetNo']: tile for tile in RULES['rulesets'][0]['tiles']}
COMPAT = {side['code']: side['compatibleWith'] for side in RULES['sideProfileCodes']}


def rotate(pattern, turns):
    for _ in range(turns):
        pattern = [''.join(pattern[2 - c][r] for c in range(3)) for r in range(3)]
    return pattern


def solve(pattern):
    choices = []
    for mask in MASKS:
        for turns in range(4):
            candidate = rotate(mask['mask3x3'], turns)
            if any(candidate[r][c] != pattern[r][c] for r, c in [(0, 1), (1, 2), (2, 1), (1, 0)]):
                continue
            misses = sum(candidate[r][c] != '?' and candidate[r][c] != pattern[r][c]
                         for r in range(3) for c in range(3))
            specificity = sum(c != '?' for row in candidate for c in row)
            choices += [(misses, -specificity, asset, turns) for asset in mask['candidates']]
    return min(choices) if choices else None


def side(choice, index):
    signature = TILES[choice[2]]['signature']
    index = (index - choice[3]) % 4
    return signature[index * 2:index * 2 + 2]


summary = {}
for vertical in (False, True):
    a, b = (1, 1), ((1, 2) if vertical else (2, 1))
    positions = [(x, y) for x in range(3 if vertical else 4)
                 for y in range(4 if vertical else 3) if (x, y) not in (a, b)]
    counts, bad_examples = Counter(), []
    for values in itertools.product('01', repeat=len(positions)):
        cells = dict(zip(positions, values))
        cells[a] = cells[b] = '1'
        patterns = [[''.join(cells[(centre[0] + dx, centre[1] + dy)] for dx in (-1, 0, 1))
                     for dy in (1, 0, -1)] for centre in (a, b)]
        left, right = map(solve, patterns)
        exact = left[0] == right[0] == 0
        first, second = ((0, 2) if vertical else (1, 3))
        left_side, right_side = side(left, first), side(right, second)
        compatible = right_side in COMPAT[left_side] and left_side in COMPAT[right_side]
        counts['both_exact' if exact else 'includes_nearest'] += 1
        counts[('exact_' if exact else 'nearest_') + ('compatible' if compatible else 'incompatible')] += 1
        if exact and not compatible and len(bad_examples) < 5:
            bad_examples.append({'patterns': patterns, 'choices': [left, right], 'sides': [left_side, right_side]})
    summary['vertical' if vertical else 'horizontal'] = {
        'counts': dict(counts), 'exact_bad_examples': bad_examples,
    }

target = ROOT / 'Docs/Rollback/2026-10-20-tile-retile/Validation/structure-pair-audit.json'
target.write_text(json.dumps(summary, indent=2) + '\n')
print(json.dumps(summary, indent=2))
