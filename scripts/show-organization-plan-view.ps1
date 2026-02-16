param(
    [string[]]$Strategy,
    [string[]]$Action,
    [string[]]$Reason,
    [string]$PathPrefix,
    [double]$MinConfidence = -1,
    [switch]$OverridesOnly,
    [switch]$MovesOnly,
    [int]$Page = 1,
    [int]$PageSize = 100,
    [ValidateSet("sourcePath", "targetPath", "confidence", "strategy", "action", "reason")]
    [string]$SortBy = "sourcePath",
    [ValidateSet("asc", "desc")]
    [string]$SortDirection = "asc",
    [string]$JellyfinBaseUrl = "http://localhost:8097",
    [string]$AccessToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$baseUri = "$($JellyfinBaseUrl.TrimEnd('/'))/shirarium/organization-plan-view"
$queryParts = New-Object System.Collections.Generic.List[string]

if ($Strategy) {
    foreach ($value in $Strategy) {
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $queryParts.Add("Strategies=$([uri]::EscapeDataString($value))")
        }
    }
}

if ($Action) {
    foreach ($value in $Action) {
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $queryParts.Add("Actions=$([uri]::EscapeDataString($value))")
        }
    }
}

if ($Reason) {
    foreach ($value in $Reason) {
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $queryParts.Add("Reasons=$([uri]::EscapeDataString($value))")
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($PathPrefix)) {
    $queryParts.Add("PathPrefix=$([uri]::EscapeDataString($PathPrefix))")
}

if ($MinConfidence -ge 0) {
    $queryParts.Add("MinConfidence=$MinConfidence")
}

if ($OverridesOnly.IsPresent) {
    $queryParts.Add("OverridesOnly=true")
}

if ($MovesOnly.IsPresent) {
    $queryParts.Add("MovesOnly=true")
}

$queryParts.Add("Page=$Page")
$queryParts.Add("PageSize=$PageSize")
$queryParts.Add("SortBy=$SortBy")
$queryParts.Add("SortDirection=$SortDirection")

$uri = $baseUri
if ($queryParts.Count -gt 0) {
    $uri = "$baseUri?$(($queryParts -join '&'))"
}

$headers = @{}
if ($AccessToken) {
    $headers["X-Emby-Token"] = $AccessToken
}

$response = Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
$response | ConvertTo-Json -Depth 12

