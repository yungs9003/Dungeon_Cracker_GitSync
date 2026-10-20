from pathlib import Path
from datetime import datetime, timezone, timedelta
import csv
import hashlib
import json

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


data = json.loads((OUT / 'proposed-mapping.json').read_text(encoding='utf-8'))
sources = json.loads((OUT / 'source-sha256.json').read_text(encoding='utf-8'))
baseline = json.loads((OUT / 'before-project-sha256.json').read_text(encoding='utf-8'))
source_changes = [name for name, digest in sources.items() if not Path(name).is_file() or sha(Path(name)) != digest]
current = {path.relative_to(ROOT).as_posix(): sha(path)
           for folder in ('Assets', 'Packages', 'ProjectSettings')
           for path in (ROOT / folder).rglob('*') if path.is_file()}
changed = [name for name in baseline.keys() & current.keys() if baseline[name] != current[name]]
added = sorted(current.keys() - baseline.keys())
removed = sorted(baseline.keys() - current.keys())
matched = [row for row in data['rows'] if row['ice'] and row['fire']]
incoming = [row[key] for row in data['rows'] + data['extras'] for key in ('ice', 'fire') if row.get(key)]
original_incoming = [name for name in sources if not Path(name).is_relative_to(ROOT)]
existing = {path.relative_to(ROOT).as_posix() for path in (ROOT / 'Assets/Sprites/Tiles').glob('*.png')}
accounted_existing = {row['source'] for row in data['rows']} | set(data['existing_without_theme'])
assert set(incoming) == set(original_incoming), 'Incoming image coverage mismatch'
assert len(incoming) == len(set(incoming)), 'Duplicate incoming assignment'
assert existing == accounted_existing, 'Existing image coverage mismatch'
assert len(matched) == 21 and len(incoming) == 44 and len(existing) == 32
assert not source_changes, f'Source image changes: {source_changes}'
assert not changed and not added and not removed, 'Product files changed; inspect before reporting'

rows = []
for row in data['rows']:
    rows.append([row['source'], row.get('ice') or '', row.get('fire') or '',
                 row.get('fire_preview_ccw_degrees', 0), row['status']])
for name in data['existing_without_theme']:
    if name not in {row['source'] for row in data['rows']}:
        rows.append([name, '', '', 0, 'missing'])
for row in data['extras']:
    rows.append(['', row['ice'], row['fire'], 0, row['status']])
with (OUT / 'mapping.csv').open('w', encoding='utf-8-sig', newline='') as file:
    writer = csv.writer(file)
    writer.writerow(['existing_png', 'ice_png', 'fire_png', 'fire_counterclockwise_degrees', 'review_status'])
    writer.writerows(rows)

manifest = {
    'checked_at_kst': datetime.now(timezone(timedelta(hours=9))).isoformat(timespec='seconds'),
    'status': 'awaiting_user_mapping_review',
    'scope': 'Read-only sprite matching; Docs review artifacts only. No Unity run or gameplay implementation.',
    'existing_png_count': len(existing),
    'incoming_png_count': len(incoming),
    'matched_existing_count': len(matched),
    'existing_without_theme_count': len(data['existing_without_theme']),
    'extra_pair_count': len(data['extras']),
    'source_hash_count': len(sources),
    'source_changes': source_changes,
    'product_baseline_file_count': len(baseline),
    'product_current_file_count': len(current),
    'product_changed_files': sorted(changed),
    'product_added_files': added,
    'product_removed_files': removed,
    'incoming_coverage_complete': True,
    'incoming_assignments_unique': True,
    'existing_coverage_complete': True,
    'preview_rotation_only': {'IMG_4944.png': '90 degrees counterclockwise'},
    'artifact_sha256': {path.relative_to(OUT).as_posix(): sha(path)
                        for path in sorted(OUT.rglob('*'))
                        if path.is_file() and path.name != 'review-manifest.json'},
}
(OUT / 'review-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({key: value for key, value in manifest.items() if key != 'artifact_sha256'}, ensure_ascii=False, indent=2))
