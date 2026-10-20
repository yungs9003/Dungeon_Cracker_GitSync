param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$directionRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$directionManifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'change-manifest.json') -Raw | ConvertFrom-Json
function Get-DirectionTarget([string]$Relative) {
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $directionRoot $Relative))
    if (-not $candidate.StartsWith($directionRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Workspace 밖의 경로입니다: $Relative"
    }
    return $candidate
}
foreach ($entry in $directionManifest.files) {
    $target = Get-DirectionTarget $entry.path
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.after) {
        throw "이번 적용 후 추가 변경된 파일이 있어 중단합니다: $target"
    }
    $original = Join-Path (Join-Path $PSScriptRoot 'Before') $entry.path
    if ((Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.before) {
        throw "백업 해시가 다릅니다: $original"
    }
}
if (-not $Apply) {
    $directionManifest.files | Select-Object path | Format-Table -AutoSize
    Write-Output '미리보기입니다. Unity 종료 후 Restore.ps1 -Apply로 이번 방향 수정만 복원할 수 있습니다.'
    return
}
foreach ($entry in $directionManifest.files) {
    Copy-Item -LiteralPath (Join-Path (Join-Path $PSScriptRoot 'Before') $entry.path) -Destination (Get-DirectionTarget $entry.path) -Force
}
foreach ($entry in $directionManifest.files) {
    if ((Get-FileHash -LiteralPath (Get-DirectionTarget $entry.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.before) {
        throw "복원 후 해시가 다릅니다: $($entry.path)"
    }
}
Write-Output '맵/101-007 방향 수정 이전으로 복원했습니다. 이전 타일 재연결 기능과 Docs 기록은 유지됩니다.'
