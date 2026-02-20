param(
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$pluginProject = Join-Path $repoRoot "src/Jellyfin.Plugin.Shirarium/Jellyfin.Plugin.Shirarium.csproj"
$pluginOutDir = Join-Path $repoRoot "artifacts/plugin/Shirarium"

New-Item -ItemType Directory -Force -Path $pluginOutDir | Out-Null

if (-not $NoBuild) {
    dotnet build $pluginProject -c Debug -o $pluginOutDir
}

docker compose -f "$repoRoot/docker-compose.yml" up -d jellyfin-dev
docker compose -f "$repoRoot/docker-compose.yml" restart jellyfin-dev

Write-Host "Plugin reloaded."
