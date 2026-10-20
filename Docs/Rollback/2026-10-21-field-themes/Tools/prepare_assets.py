from pathlib import Path
from PIL import Image
import hashlib
import json
import re
import shutil
import uuid

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]
review = ROOT / 'Docs/FieldThemeReview/2026-10-21'
mapping = json.loads((review / 'proposed-mapping.json').read_text(encoding='utf-8'))
modified = [
    'Assets/Scripts/GamePlay/Map/AssetVariantSprite2D.cs',
    'Assets/Scripts/GamePlay/Map/FieldEffectManager.cs',
    'Assets/Scripts/GamePlay/Map/JsonBlockBuilder2D.cs',
    'Assets/Scripts/GamePlay/Map/JsonBlockBuilder2D.Retile.cs',
    'Assets/Scripts/GamePlay/Traps/Destructible.cs',
    'Assets/Scripts/System/ReplaceSelfWithPrefabOnDisable.cs',
]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'Dungeon.FieldThemes.20261021/' + path).hex


if (OUT / 'before-project-sha256.json').exists():
    raise RuntimeError('Preparation already recorded; do not overwrite Before history.')
baseline = {path.relative_to(ROOT).as_posix(): sha(path)
            for folder in ('Assets', 'Packages', 'ProjectSettings')
            for path in (ROOT / folder).rglob('*') if path.is_file()}
(OUT / 'before-project-sha256.json').write_text(json.dumps(baseline, indent=2) + '\n', encoding='utf-8')
for name in modified:
    destination = OUT / 'Before' / name
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT / name, destination)

template = (ROOT / 'Assets/Sprites/Tiles/001-005.png.meta').read_text(encoding='utf-8')
asset_manifest = []
for row in mapping['rows']:
    for theme in ('ice', 'fire'):
        if not row.get(theme):
            continue
        incoming = Path(row[theme])
        rel = 'Assets/Sprites/FieldThemes/' + theme.capitalize() + '/' + Path(row['source']).name
        destination = ROOT / rel
        if destination.exists():
            raise RuntimeError('Unexpected existing destination: ' + rel)
        destination.parent.mkdir(parents=True, exist_ok=True)
        rotation = row.get('fire_preview_ccw_degrees', 0) if theme == 'fire' else 0
        if rotation:
            assert rotation == 90
            with Image.open(incoming) as image:
                image.transpose(Image.Transpose.ROTATE_90).save(destination)
        else:
            shutil.copy2(incoming, destination)
        meta = re.sub(r'(?m)^guid: .+$', 'guid: ' + guid(rel), template)
        meta = re.sub(r'(?m)^    filterMode: .+$', '    filterMode: 0', meta)
        meta = re.sub(r'(?m)^    textureCompression: .+$', '    textureCompression: 0', meta)
        meta = re.sub(r'(?m)^  spriteMeshType: .+$', '  spriteMeshType: 0', meta)
        Path(str(destination) + '.meta').write_text(meta, encoding='utf-8')
        asset_manifest.append({'original': row['source'], 'theme': theme, 'incoming': str(incoming),
                               'asset': rel, 'guid': guid(rel), 'counterclockwise_degrees': rotation,
                               'incoming_sha256': sha(incoming), 'asset_sha256': sha(destination)})

for rel in ('Assets/Sprites/FieldThemes', 'Assets/Sprites/FieldThemes/Ice', 'Assets/Sprites/FieldThemes/Fire'):
    (ROOT / (rel + '.meta')).write_text('fileFormatVersion: 2\nguid: ' + guid(rel) + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
script_rel = 'Assets/Scripts/GamePlay/Map/MapSpriteThemeCatalog.cs'
(ROOT / (script_rel + '.meta')).write_text('fileFormatVersion: 2\nguid: ' + guid(script_rel) + '\n', encoding='utf-8')

yaml = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
yaml += '  m_Script: {fileID: 11500000, guid: ' + guid(script_rel) + ', type: 3}\n'
yaml += '  m_Name: MapSpriteThemes\n  m_EditorClassIdentifier: Dungeon.GamePlay::Dungeon.Map.MapSpriteThemeCatalog\n  entries:\n'
by_original = {row['original']: {} for row in asset_manifest}
for row in asset_manifest:
    by_original[row['original']][row['theme']] = row['guid']
for source in sorted((ROOT / 'Assets/Sprites/Tiles').glob('*.png')):
    rel = source.relative_to(ROOT).as_posix()
    meta = Path(str(source) + '.meta').read_text(encoding='utf-8')
    original_guid = re.search(r'(?m)^guid: (\w+)', meta).group(1)
    mode = re.search(r'(?m)^  spriteMode: (\d+)', meta).group(1)
    file_id = '21300000' if mode == '1' else re.search(r'(?m)^      internalID: (-?\d+)', meta).group(1)
    yaml += '  - original: {fileID: ' + file_id + ', guid: ' + original_guid + ', type: 3}\n'
    for theme in ('ice', 'fire'):
        theme_guid = by_original.get(rel, {}).get(theme)
        yaml += '    ' + theme + ': ' + ('{fileID: 21300000, guid: ' + theme_guid + ', type: 3}' if theme_guid else '{fileID: 0}') + '\n'
catalog_rel = 'Assets/Resources/Data/MapSpriteThemes.asset'
(ROOT / catalog_rel).write_text(yaml, encoding='utf-8')
(ROOT / (catalog_rel + '.meta')).write_text('fileFormatVersion: 2\nguid: ' + guid(catalog_rel) + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
(OUT / 'asset-import-manifest.json').write_text(json.dumps(asset_manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'Before preserved for {len(modified)} files; created {len(asset_manifest)} sprites and 32 catalog entries. Extra pit pair deferred.')
