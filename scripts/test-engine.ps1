Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$engineDir = Join-Path $repoRoot "engine"
$imageName = "shirarium-engine-test:local"

docker build -t $imageName $engineDir
if ($LASTEXITCODE -ne 0) {
    throw "docker build failed with exit code $LASTEXITCODE"
}

docker run --rm $imageName python -m unittest discover -s tests -v
if ($LASTEXITCODE -ne 0) {
    throw "docker test run failed with exit code $LASTEXITCODE"
}
