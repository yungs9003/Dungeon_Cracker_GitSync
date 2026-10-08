from pathlib import Path
import hashlib, json, re, zipfile
ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
baseline=json.loads((OUT/'Validation/original-project-sha256.json').read_text(encoding='utf-8'))
current={str(p.relative_to(ROOT)).replace('\\','/'):sha(p)
         for folder in ('Assets','ProjectSettings','Packages') for p in (ROOT/folder).rglob('*') if p.is_file()}
changed=[name for name in baseline if current.get(name)!=baseline[name]]
added=sorted(set(current)-set(baseline))
assert not changed and not added,(changed,added)
manifest=json.loads((OUT/'Integration/manifest.json').read_text(encoding='utf-8'))
for row in manifest:
    assert sha(ROOT/row['path'])==row['before_sha256']
    assert sha(OUT/'Integration/Before'/row['path'])==row['before_sha256']
    assert sha(OUT/'Integration/After'/row['path'])==row['after_sha256']
guids={}
for folder in (ROOT/'Assets',OUT/'New/Assets'):
    for meta in folder.rglob('*.meta'):
        match=re.search(r'^guid: ([0-9a-f]{32})',meta.read_text(encoding='utf-8-sig'),re.M)
        if match: guids[match[1]]=meta
for asset in list((OUT/'New').rglob('*.prefab'))+list((OUT/'New').rglob('*.unity')):
    missing={g for g in re.findall(r'guid: ([0-9a-f]{32})',asset.read_text(encoding='utf-8-sig'))
             if g not in guids and not g.startswith('0000000000000000')}
    assert not missing,(asset,missing)
checks=(OUT/'Validation/unity-checks.txt').read_text(encoding='utf-8-sig')
assert checks.strip().endswith('ALL CHECKS PASSED')
count=sum(line.startswith('PASS: ') for line in checks.splitlines())
for assembly in ('Dungeon.Core','Dungeon.GamePlay','Dungeon.App','Dungeon.System'):
    assert (OUT/f'Validation/Compile/{assembly}.dll').exists()
    assert 'error CS' not in (OUT/f'Validation/Compile/{assembly}.log').read_text(encoding='utf-8')
result={'status':'passed','original_files_verified':len(baseline),'original_files_modified':changed,
        'original_files_added':added,'integration_files':len(manifest),'unity_assertions':count,
        'prefabs':['102-014','102-015'],'original_project_applied':False,
        'compile_assemblies':['Dungeon.Core','Dungeon.GamePlay','Dungeon.App','Dungeon.System'],
        'validation_mode':'Isolated Unity Editor; deterministic Tick/reflection and Physics2D.Simulate; original Play Mode not run'}
(OUT/'Validation/summary.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
with zipfile.ZipFile(OUT/'wall-force-traps-review.zip','w',zipfile.ZIP_DEFLATED) as package:
    for folder in ['New','Integration','Tools']:
        for asset in (OUT/folder).rglob('*'):
            if asset.is_file() and '__pycache__' not in asset.parts: package.write(asset,asset.relative_to(OUT))
    for name in ['README.md','Validation/unity-checks.txt','Validation/summary.json',
                 'Validation/review-idle.png','Validation/review-moving.png']:
        package.write(OUT/name,name)
print(json.dumps(result,ensure_ascii=False))
print('Package:',OUT/'wall-force-traps-review.zip')
