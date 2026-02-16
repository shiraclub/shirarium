param(
    [switch]$WithOllama
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

if ($WithOllama) {
    docker compose -f "$repoRoot/docker-compose.yml" --profile local-llm up -d --build
}
else {
    docker compose -f "$repoRoot/docker-compose.yml" up -d --build
}

Write-Host "Stack is running."
$jellyfinPort = if ($env:JELLYFIN_PORT) { $env:JELLYFIN_PORT } else { "8097" }
Write-Host "Jellyfin: http://localhost:$jellyfinPort"
