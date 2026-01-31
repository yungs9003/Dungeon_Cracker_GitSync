<#
apply_namespaces.ps1

간단 설명:
- `Assets/Scripts` 아래 각 서브폴더(예: Player, UI 등)를 기준으로 네임스페이스를 적용합니다.
- 각 .cs 파일의 백업을 `Assets/Scripts/namespace_backups`에 저장한 뒤 수정합니다.
- 지원되는 매핑: Player, UI, Traps, Map, Core, Manager, System

주의:
- 복잡한 파일(파일 스코프 네임스페이스, 이미 블록 네임스페이스, partial class 등)을 완벽히 처리하려면 수동 검토 권장.
- 실행 전 반드시 변경사항을 버전관리 또는 백업에서 확인하세요.
#>
param(
    [string]$RootDir = "Assets\Scripts"
)

$map = @{
    "Player" = "Dungeon.Game.Player"
    "UI" = "Dungeon.Game.UI"
    "Traps" = "Dungeon.Game.Traps"
    "Map" = "Dungeon.Game.Map"
    "Core" = "Dungeon.Game.Core"
    "Manager" = "Dungeon.Game.Manager"
    "System" = "Dungeon.Game.System"
}

$backupRoot = Join-Path $RootDir 'namespace_backups'
if (-not (Test-Path $backupRoot)) { New-Item -ItemType Directory -Path $backupRoot | Out-Null }

Get-ChildItem -Path $RootDir -Recurse -Filter *.cs | ForEach-Object {
    $file = $_
    $dir = $file.DirectoryName
    $segments = $dir -split '[\\/]'
    $idx = [Array]::IndexOf($segments, 'Scripts')
    if ($idx -ge 0 -and $idx + 1 -lt $segments.Length) { $key = $segments[$idx + 1] } else { $key = $segments[-1] }

    if (-not $map.ContainsKey($key)) {
        Write-Host "Skipping $($file.FullName) — no mapping for folder '$key'" -ForegroundColor Yellow
        return
    }
    $targetNs = $map[$key]

    # backup file preserving relative path
    $relPath = $file.FullName.Substring((Get-Item $RootDir).FullName.Length).TrimStart('\','/')
    $backupPath = Join-Path $backupRoot $relPath
    $backupDir = Split-Path $backupPath -Parent
    if (-not (Test-Path $backupDir)) { New-Item -ItemType Directory -Path $backupDir -Force | Out-Null }
    Copy-Item -Path $file.FullName -Destination $backupPath -Force

    $text = Get-Content -Raw -Path $file.FullName

    # detect file-scoped namespace: "namespace X;"
    if ($text -match "namespace\s+[\w\.]+\s*;") {
        Write-Host "Converting file-scoped namespace in $($file.Name) -> $targetNs"
        $text = [regex]::Replace($text, "namespace\s+[\w\.]+\s*;", "namespace $targetNs {")
        # ensure trailing closing brace
        if ($text.TrimEnd() -notlike '*}`') { $text = $text + "`r`n}" }
        Set-Content -Path $file.FullName -Value $text -Force
        return
    }

    # detect block namespace: "namespace X {"
    if ($text -match "namespace\s+[\w\.]+\s*\{") {
        Write-Host "Replacing namespace in $($file.Name) -> $targetNs"
        $text = [regex]::Replace($text, "namespace\s+[\w\.]+\s*\{", "namespace $targetNs {", 1)
        Set-Content -Path $file.FullName -Value $text -Force
        return
    }

    # no namespace: separate using block and wrap rest
    $usingMatch = [regex]::Match($text, '^(\s*(using\s+[^;\r\n]+;\s*\r?\n)+)', 'Singleline')
    $usings = $usingMatch.Value
    $rest = $text.Substring($usings.Length)
    Write-Host "Adding namespace to $($file.Name) -> $targetNs"
    $newText = $usings + "`r`nnamespace " + $targetNs + " {`r`n" + $rest + "`r`n}" 
    Set-Content -Path $file.FullName -Value $newText -Force
}

Write-Host "Namespace application complete. Backups in: $backupRoot" -ForegroundColor Green
