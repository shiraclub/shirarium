Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$pluginProject = Join-Path $repoRoot "src/Jellyfin.Plugin.Shirarium/Jellyfin.Plugin.Shirarium.csproj"
$unitTestProject = Join-Path $repoRoot "tests/Jellyfin.Plugin.Shirarium.Tests/Jellyfin.Plugin.Shirarium.Tests.csproj"
$integrationTestProject = Join-Path $repoRoot "tests/Jellyfin.Plugin.Shirarium.IntegrationTests/Jellyfin.Plugin.Shirarium.IntegrationTests.csproj"

dotnet restore $pluginProject
dotnet restore $unitTestProject
dotnet restore $integrationTestProject
dotnet test $unitTestProject -c Release --no-restore --verbosity normal
dotnet test $integrationTestProject -c Release --no-restore --verbosity normal
