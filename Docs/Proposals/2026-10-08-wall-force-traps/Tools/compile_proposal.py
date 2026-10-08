from pathlib import Path
import subprocess, re
ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]
BUILD = OUT / 'Validation' / 'Compile'
BUILD.mkdir(parents=True, exist_ok=True)
UNITY = Path('C:/Program Files/Unity/Hub/Editor/6000.2.14f1/Editor/Data')
for assembly, scope in [('Dungeon.Core','Core'),('Dungeon.GamePlay','GamePlay'),('Dungeon.App','App'),('Dungeon.System','System')]:
    original_rsp = ROOT / f'Library/Bee/artifacts/1900b0aE.dag/{assembly}.rsp'
    lines = []
    for line in original_rsp.read_text(encoding='utf-8-sig').splitlines():
        if line.startswith('-out:'):
            line = f'-out:"{(BUILD / (assembly+".dll")).as_posix()}"'
        elif line.startswith('-refout:'):
            line = f'-refout:"{(BUILD / (assembly+".ref.dll")).as_posix()}"'
        elif line.startswith('-r:'):
            match = re.search(r'/(Dungeon\.[\w]+)\.ref\.dll',line)
            if match and (BUILD / (match[1]+'.ref.dll')).exists():
                line = f'-r:"{(BUILD / (match[1]+".ref.dll")).as_posix()}"'
        elif line.startswith('"Assets/') and line.endswith('.cs"'):
            rel = line.strip('"')
            staged = OUT / 'Integration' / 'After' / rel
            if staged.exists(): line = '"'+staged.as_posix()+'"'
        lines.append(line)
    for source in (OUT / 'New' / 'Assets' / 'Scripts' / scope).rglob('*.cs'):
        lines.append('"'+source.as_posix()+'"')
    rsp = BUILD / (assembly+'.rsp')
    rsp.write_text('\n'.join(lines)+'\n', encoding='utf-8')
    run = subprocess.run([str(UNITY/'NetCoreRuntime/dotnet.exe'), str(UNITY/'DotNetSdkRoslyn/csc.dll'), '@'+str(rsp)],
                         cwd=ROOT, capture_output=True, text=True, encoding='utf-8')
    (BUILD / (assembly+'.log')).write_text(run.stdout+run.stderr,encoding='utf-8')
    print(assembly, 'PASS' if run.returncode == 0 else 'FAIL')
    if run.stdout.strip(): print(run.stdout.strip())
    if run.stderr.strip(): print(run.stderr.strip())
    if run.returncode: raise SystemExit(run.returncode)
