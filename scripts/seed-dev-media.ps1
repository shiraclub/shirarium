param(
    [string]$Preset = "synthetic-community-v1",
    [string]$DatasetPath,
    [string]$MediaRoot,
    [switch]$CleanIncoming,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ConfiguredMediaRoot {
    param(
        [string]$RepoRoot
    )

    $envPath = Join-Path $RepoRoot ".env"
    if (-not (Test-Path -LiteralPath $envPath)) {
        return (Join-Path $RepoRoot "data/media")
    }

    $line = Get-Content -LiteralPath $envPath |
        Where-Object { $_ -match '^\s*MEDIA_PATH\s*=' } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($line)) {
        return (Join-Path $RepoRoot "data/media")
    }

    $value = ($line -split "=", 2)[1].Trim()
    if ($value.StartsWith('"') -and $value.EndsWith('"')) {
        $value = $value.Substring(1, $value.Length - 2)
    }

    if ([string]::IsNullOrWhiteSpace($value)) {
        return (Join-Path $RepoRoot "data/media")
    }

    if ([System.IO.Path]::IsPathRooted($value)) {
        return $value
    }

    return (Join-Path $RepoRoot $value)
}

function Test-PathInsideRoot {
    param(
        [string]$RootPath,
        [string]$CandidatePath
    )

    $rootFull = [System.IO.Path]::GetFullPath($RootPath)
    $candidateFull = [System.IO.Path]::GetFullPath($CandidatePath)

    if ($candidateFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    $rootWithSeparator = if ($rootFull.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $rootFull
    }
    else {
        $rootFull + [System.IO.Path]::DirectorySeparatorChar
    }

    return $candidateFull.StartsWith($rootWithSeparator, [StringComparison]::OrdinalIgnoreCase)
}

function Get-OptionalPropertyValue {
    param(
        [object]$Object,
        [string]$PropertyName
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($MediaRoot)) {
    $MediaRoot = Get-ConfiguredMediaRoot -RepoRoot $repoRoot
}

if ([string]::IsNullOrWhiteSpace($DatasetPath)) {
    $DatasetPath = Join-Path $repoRoot ("datasets/jellyfin-dev/{0}.json" -f $Preset)
}

if (-not (Test-Path -LiteralPath $DatasetPath)) {
    throw "Dataset file not found: $DatasetPath"
}

$manifest = Get-Content -LiteralPath $DatasetPath -Raw | ConvertFrom-Json
if ($null -eq $manifest.entries -or $manifest.entries.Count -eq 0) {
    throw "Dataset manifest has no entries: $DatasetPath"
}

$mediaRootFull = [System.IO.Path]::GetFullPath($MediaRoot)
New-Item -ItemType Directory -Path $mediaRootFull -Force | Out-Null

if ($CleanIncoming.IsPresent) {
    $incomingPath = Join-Path $mediaRootFull "incoming"
    if (Test-Path -LiteralPath $incomingPath) {
        Remove-Item -LiteralPath $incomingPath -Recurse -Force
    }
}

$created = 0
$overwritten = 0
$skippedExisting = 0
$mediaTypeCounts = @{}

foreach ($entry in $manifest.entries) {
    $relativePath = [string]$entry.relativePath
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        throw "Dataset entry contains empty relativePath."
    }

    $normalizedRelativePath = $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar).Replace('\', [System.IO.Path]::DirectorySeparatorChar)
    if ([System.IO.Path]::IsPathRooted($normalizedRelativePath)) {
        throw "Dataset entry must use a relative path: $relativePath"
    }

    $targetPath = [System.IO.Path]::GetFullPath((Join-Path $mediaRootFull $normalizedRelativePath))
    if (-not (Test-PathInsideRoot -RootPath $mediaRootFull -CandidatePath $targetPath)) {
        throw "Dataset entry resolves outside media root: $relativePath"
    }

    $targetDirectory = [System.IO.Path]::GetDirectoryName($targetPath)
    if (-not [string]::IsNullOrWhiteSpace($targetDirectory)) {
        New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    }

    $alreadyExists = Test-Path -LiteralPath $targetPath
    if ($alreadyExists -and -not $Force.IsPresent) {
        $skippedExisting++
        continue
    }

    $defaultContents = @(
        "shirarium synthetic dataset"
        "manifest=$($manifest.name)"
        "source=$relativePath"
    ) -join [Environment]::NewLine

    $entryFileContents = [string](Get-OptionalPropertyValue -Object $entry -PropertyName "fileContents")
    $fileContents = if ([string]::IsNullOrWhiteSpace($entryFileContents)) {
        $defaultContents
    }
    else {
        $entryFileContents
    }

    Set-Content -LiteralPath $targetPath -Value $fileContents -NoNewline -Encoding UTF8

    if ($alreadyExists) {
        $overwritten++
    }
    else {
        $created++
    }

    $expected = Get-OptionalPropertyValue -Object $entry -PropertyName "expected"
    $mediaType = [string](Get-OptionalPropertyValue -Object $expected -PropertyName "mediaType")
    if ([string]::IsNullOrWhiteSpace($mediaType)) {
        $mediaType = "unknown"
    }

    if (-not $mediaTypeCounts.ContainsKey($mediaType)) {
        $mediaTypeCounts[$mediaType] = 0
    }

    $mediaTypeCounts[$mediaType]++
}

$summary = [pscustomobject]@{
    Manifest       = [string]$manifest.name
    DatasetPath    = [System.IO.Path]::GetFullPath($DatasetPath)
    MediaRoot      = $mediaRootFull
    EntryCount     = [int]$manifest.entries.Count
    Created        = $created
    Overwritten    = $overwritten
    SkippedExisting = $skippedExisting
    MediaTypes     = $mediaTypeCounts.GetEnumerator() |
        Sort-Object Name |
        ForEach-Object { [pscustomobject]@{ MediaType = $_.Key; Count = $_.Value } }
}

$summary | ConvertTo-Json -Depth 8
