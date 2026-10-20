from pathlib import Path
from PIL import Image, ImageChops
import difflib
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]
ISOLATED = ROOT / 'Library/CodexPlayTime/Unity'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


before = json.loads((OUT / 'before-project-sha256.json').read_text(encoding='utf-8'))
current = {path.relative_to(ROOT).as_posix(): sha(path)
           for folder in ('Assets', 'Packages', 'ProjectSettings')
           for path in (ROOT / folder).rglob('*') if path.is_file()}
modified = sorted(name for name in before.keys() & current.keys() if before[name] != current[name])
added = sorted(current.keys() - before.keys())
removed = sorted(before.keys() - current.keys())
expected_modified = sorted(path.relative_to(OUT / 'Before').as_posix() for path in (OUT / 'Before').rglob('*') if path.is_file())
assert modified == expected_modified and len(modified) == 6 and not removed, modified
assert len(added) == 91, added
assert all(name.startswith(('Assets/Sprites/FieldThemes', 'Assets/Resources/Data/MapSpriteThemes.asset', 'Assets/Scripts/GamePlay/Map/MapSpriteThemeCatalog.cs')) for name in added)
source_hashes = json.loads((ROOT / 'Docs/FieldThemeReview/2026-10-21/source-sha256.json').read_text(encoding='utf-8'))
assert all(sha(Path(name)) == digest for name, digest in source_hashes.items()), 'Original image changed'
images = json.loads((OUT / 'asset-import-manifest.json').read_text(encoding='utf-8'))
for row in images:
    incoming = Path(row['incoming'])
    assert sha(incoming) == row['incoming_sha256']
    assert sha(ROOT / row['asset']) == row['asset_sha256']
    with Image.open(incoming) as first, Image.open(ROOT / row['asset']) as second:
        expected = first.convert('RGBA')
        if row['counterclockwise_degrees']:
            expected = expected.transpose(Image.Transpose.ROTATE_90)
        assert expected.tobytes() == second.convert('RGBA').tobytes(), row['asset']
    if not row['counterclockwise_degrees']:
        assert sha(incoming) == sha(ROOT / row['asset'])
isolated_differences = [name for name in modified + added if not (ISOLATED / name).exists() or sha(ISOLATED / name) != current[name]]
# Unity may serialize importer metadata; code, PNG and catalog must remain byte-identical.
material_isolated_differences = [name for name in isolated_differences if not name.endswith('.meta')]
assert not material_isolated_differences, material_isolated_differences
for name in modified + added:
    target = OUT / 'After' / name
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT / name, target)
for name in modified:
    target = OUT / 'Diff' / (name + '.diff')
    target.parent.mkdir(parents=True, exist_ok=True)
    text = ''.join(difflib.unified_diff((OUT / 'Before' / name).read_text(encoding='utf-8-sig').splitlines(keepends=True),
                                      (ROOT / name).read_text(encoding='utf-8-sig').splitlines(keepends=True), fromfile='Before/' + name, tofile='After/' + name))
    target.write_text(text, encoding='utf-8')
checks = (OUT / 'Validation/runtime-checks.txt').read_text(encoding='utf-8-sig')
assert checks.splitlines()[-1].endswith('FAILED 0'), checks.splitlines()[-1]
assert not any('error CS' in line for line in (OUT / 'Validation/runtime.log').read_text(encoding='utf-8-sig').splitlines())
manifest = {'status': 'implemented_and_verified', 'baseline_count': len(before), 'final_count': len(current),
            'modified': modified, 'added': added, 'removed': removed, 'unchanged_count': len(before) - len(modified),
            'original_images_unchanged': len(source_hashes), 'theme_images': len(images),
            'unrotated_exact_copies': sum(not row['counterclockwise_degrees'] for row in images),
            'pixel_exact_quarter_turns': sum(bool(row['counterclockwise_degrees']) for row in images),
            'runtime_checks': checks.splitlines()[-1], 'isolated_product_differences': material_isolated_differences,
            'isolated_importer_metadata_differences': isolated_differences,
            'actual_save_files_copied': 0,
            'files': [{'path': name, 'before_sha256': before.get(name), 'after_sha256': current[name]} for name in modified + added]}
(OUT / 'after-project-sha256.json').write_text(json.dumps(current, indent=2) + '\n', encoding='utf-8')
(OUT / 'change-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({key: value for key, value in manifest.items() if key not in ('files', 'added')}, ensure_ascii=False, indent=2))
