from pathlib import Path
import json
import shutil

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]
ISOLATED = ROOT / 'Library/CodexPlayTime/Unity'
assert ISOLATED.resolve().is_relative_to((ROOT / 'Library').resolve())
assert not (ISOLATED / 'Temp/UnityLockfile').exists(), 'Isolated Unity is already open'
copied = 0
for folder in ('Assets', 'Packages', 'ProjectSettings'):
    for path in (ROOT / folder).rglob('*'):
        if not path.is_file() or path.is_relative_to(ROOT / 'Assets/StreamingAssets'):
            continue
        destination = ISOLATED / path.relative_to(ROOT)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, destination)
        copied += 1
for name, destination in (
    ('FieldThemeRuntimeChecks.cs', 'Assets/FieldThemeValidation/FieldThemeRuntimeChecks.cs'),
    ('FieldThemeRuntimeReview.cs', 'Assets/Editor/FieldThemeRuntimeReview.cs'),
):
    target = ISOLATED / destination
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(OUT / 'Validation/Tools' / name, target)

resources = ISOLATED / 'Assets/Resources/FieldThemeValidation'
resources.mkdir(parents=True, exist_ok=True)
manifest = json.loads((OUT / 'asset-import-manifest.json').read_text(encoding='utf-8'))
by_source = {path.relative_to(ROOT).as_posix(): {'source': path.relative_to(ROOT).as_posix()}
             for path in (ROOT / 'Assets/Sprites/Tiles').glob('*.png')}
for row in manifest:
    by_source[row['original']][row['theme']] = row['asset']
(resources / 'expectations.json').write_text(json.dumps({'rows': list(by_source.values())}, indent=2), encoding='utf-8')

cells = []
def add(x, y, code, asset=0, direction='00'):
    cells.append({'coord': f'{x:02x}{y:02x}', 'code': code, 'assetNo': asset, 'direction': direction})
for asset in range(14):
    add(asset, 2, '001-001', asset, ('00', '01', '11', '10')[asset % 4])
for asset in range(5):
    add(asset, 4, '100-000', asset)
for x, code in enumerate(('000-000', '001-002', '001-003', '001-004', '001-005', '101-001', '101-007', '101-014', '101-015')):
    add(x, 6, code, 0, ('00', '01', '11', '10')[x % 4])
upper = [{'coord': f'{x:02x}08', 'code': code, 'assetNo': 1 if code.startswith('101') else 0, 'direction': '01'}
         for x, code in enumerate(('101-001', '101-007', '001-005', '001-002'))]
for theme in ('ice', 'fire', 'burn', 'none', 'missing', 'unknown'):
    data = {'size': '100b', 'grid': cells, '3F': upper}
    if theme != 'missing': data['field_effect'] = {'type': theme, 'damage': 0}
    (resources / (theme + '.json')).write_text(json.dumps(data), encoding='utf-8')

for theme in ('ice', 'fire'):
    for fixture in ('neighbour', 'forceNeighbour', 'floor3'):
        source = ISOLATED / ('Assets/Resources/CodexRetileChecks/' + fixture + '.json')
        data = json.loads(source.read_text(encoding='utf-8-sig'))
        data['field_effect'] = {'type': theme, 'damage': 0}
        (resources / (theme + '-' + fixture + '.json')).write_text(json.dumps(data), encoding='utf-8')
    data = json.loads((ROOT / 'Assets/Resources/Maps/DUNGEON_001/map_002.json').read_text(encoding='utf-8-sig'))
    data['field_effect'] = {'type': theme, 'damage': 0}
    (resources / (theme + '-map002.json')).write_text(json.dumps(data), encoding='utf-8')

(OUT / 'Validation').mkdir(exist_ok=True)
(OUT / 'Validation/isolation.json').write_text(json.dumps({'isolated_project': str(ISOLATED), 'copied_product_files': copied,
    'excluded_directory': 'Assets/StreamingAssets', 'actual_save_files_copied': 0,
    'save_initialization': 'Runner serializes StatusSaveDefaults inside isolated project only'}, indent=2), encoding='utf-8')
print(f'Synchronized {copied} files into existing isolated cache. StreamingAssets excluded entirely.')
