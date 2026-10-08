from pathlib import Path
import shutil, json
ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]
REVIEW = ROOT / 'Temp' / 'WallForceTrapReview'
REVIEW.mkdir(parents=True,exist_ok=True)
editor = REVIEW / 'Assets/Editor'
editor.mkdir(parents=True,exist_ok=True)
shutil.copy2(OUT / 'Tools/WallForceTrapReviewBuilder.cs', editor / 'WallForceTrapReviewBuilder.cs')
for scope in ('Core','GamePlay'):
    shutil.copytree(ROOT / 'Assets' / 'Scripts' / scope, REVIEW / 'Assets' / 'Scripts' / scope, dirs_exist_ok=True)
shutil.copytree(OUT / 'New' / 'Assets', REVIEW / 'Assets', dirs_exist_ok=True)
for source in (OUT / 'Integration' / 'After' / 'Assets').rglob('*.cs'):
    dest = REVIEW / 'Assets' / source.relative_to(OUT / 'Integration' / 'After' / 'Assets')
    dest.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(source,dest)
    # 원본 .meta는 원본 프로젝트에서 복사해 동일한 스크립트 GUID를 사용합니다.
    meta = ROOT / source.relative_to(OUT / 'Integration' / 'After')
    if meta.with_suffix('.cs.meta').exists(): shutil.copy2(meta.with_suffix('.cs.meta'),dest.with_suffix('.cs.meta'))

game_asm = REVIEW / 'Assets/Scripts/GamePlay/Dungeon.GamePlay.asmdef'
data = json.loads(game_asm.read_text(encoding='utf-8-sig'))
data['references'] = ['Dungeon.Core']
game_asm.write_text(json.dumps(data,indent=2),encoding='utf-8')
system_asm = REVIEW / 'Assets/Scripts/System/WallForceReview.System.asmdef'
system_asm.parent.mkdir(parents=True,exist_ok=True)
system_asm.write_text(json.dumps({'name':'Dungeon.System','references':['Dungeon.Core','Dungeon.GamePlay']}),encoding='utf-8')
plugins = REVIEW / 'Assets/Plugins'
plugins.mkdir(exist_ok=True)
for path in [ROOT/'Library/ScriptAssemblies/Unity.InputSystem.dll',
             ROOT/'Library/ScriptAssemblies/UnityEngine.UI.dll',
             ROOT/'Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll']:
    shutil.copy2(path,plugins/path.name)
(plugins/'Editor').mkdir(exist_ok=True)
shutil.copy2(ROOT/'Library/ScriptAssemblies/UnityEditor.UI.dll',plugins/'Editor/UnityEditor.UI.dll')

sprite = ROOT / 'Assets/Sprites/Projectiles/PJ-005.png'
wall_meta = next(p for p in (ROOT/'Assets/Sprites').rglob('*.meta')
                 if 'guid: acc3e26f2f57dc14d99e15d366a51b3f' in p.read_text(encoding='utf-8-sig'))
for asset in [sprite,wall_meta.with_suffix('')]:
    target = REVIEW/asset.relative_to(ROOT)
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(asset,target)
    shutil.copy2(asset.with_suffix(asset.suffix+'.meta'),target.with_suffix(target.suffix+'.meta'))

for name in ['TagManager.asset','ProjectVersion.txt']:
    target = REVIEW/'ProjectSettings'/name
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(ROOT/'ProjectSettings'/name,target)
packages = REVIEW/'Packages'
packages.mkdir(exist_ok=True)
# 모든 의존 DLL은 로컬 복사. 모듈 이외 패키지 다운로드와 사용자 세이브가 없습니다.
packages.joinpath('manifest.json').write_text(json.dumps({'dependencies':{
    'com.unity.modules.physics2d':'1.0.0','com.unity.modules.physics':'1.0.0',
    'com.unity.modules.jsonserialize':'1.0.0','com.unity.modules.imageconversion':'1.0.0',
    'com.unity.modules.animation':'1.0.0','com.unity.modules.audio':'1.0.0',
    'com.unity.modules.ui':'1.0.0','com.unity.modules.imgui':'1.0.0',
    'com.unity.modules.particlesystem':'1.0.0'}}),encoding='utf-8')
print(REVIEW)
