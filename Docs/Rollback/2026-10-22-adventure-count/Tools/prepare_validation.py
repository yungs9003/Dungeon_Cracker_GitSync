from pathlib import Path
import json,shutil
ROOT=Path(__file__).resolve().parents[4];OUT=Path(__file__).resolve().parents[1]
ISOLATED=ROOT/'Library/CodexPlayTime/Unity'
assert ISOLATED.resolve().is_relative_to((ROOT/'Library').resolve())
assert not (ISOLATED/'Temp/UnityLockfile').exists()
for folder in ('Assets','Packages','ProjectSettings'):
 for source in (ROOT/folder).rglob('*'):
  if not source.is_file() or source.is_relative_to(ROOT/'Assets/StreamingAssets'):continue
  target=ISOLATED/source.relative_to(ROOT);target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
for name,folder in [('AdventureCountRuntimeReview.cs','Assets/Editor'),('AdventureCountRuntimeChecks.cs','Assets/AdventureCountValidation')]:
 source=OUT/'Validation/Tools'/name
 if source.exists():
  target=ISOLATED/folder/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
(OUT/'Validation').mkdir(exist_ok=True)
(OUT/'Validation/isolation.json').write_text(json.dumps({'project':str(ISOLATED),'actual_save_files_copied':0,
 'excluded':'Assets/StreamingAssets','save_fixture':'StatusSaveDefaults.Create in isolated project'},indent=2),encoding='utf-8')
print('Synchronized isolated product source excluding StreamingAssets.')
