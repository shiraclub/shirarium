Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$filePath = Join-Path $repoRoot "data/jellyfin/config/data/plugins/Shirarium/apply-journal.json"

if (-not (Test-Path $filePath)) {
    Write-Host "No apply journal found yet at:"
    Write-Host $filePath
    exit 0
}

Get-Content -Path $filePath
