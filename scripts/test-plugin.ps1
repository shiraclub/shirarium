Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$pluginProject = Join-Path $repoRoot "src/Jellyfin.Plugin.Shirarium/Jellyfin.Plugin.Shirarium.csproj"
$testProject = Join-Path $repoRoot "tests/Jellyfin.Plugin.Shirarium.Tests/Jellyfin.Plugin.Shirarium.Tests.csproj"

dotnet restore $pluginProject
dotnet restore $testProject
dotnet test $testProject -c Release --no-restore --verbosity normal
