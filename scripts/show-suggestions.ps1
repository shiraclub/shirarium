Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$filePath = Join-Path $repoRoot "data/jellyfin/config/data/plugins/Shirarium/dryrun-suggestions.json"

if (-not (Test-Path $filePath)) {
    Write-Host "No suggestion snapshot found yet at:"
    Write-Host $filePath
    exit 0
}

Get-Content -Path $filePath
