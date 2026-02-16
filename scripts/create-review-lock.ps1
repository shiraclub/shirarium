param(
    [string[]]$SourcePath,
    [string]$JellyfinBaseUrl = "http://localhost:8097",
    [string]$PlanFingerprint,
    [string]$AccessToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$baseUrl = $JellyfinBaseUrl.TrimEnd("/")
$uri = "$baseUrl/shirarium/review-locks"
$headers = @{}

if ($AccessToken) {
    $headers["X-Emby-Token"] = $AccessToken
}

if ([string]::IsNullOrWhiteSpace($PlanFingerprint)) {
    $planUri = "$baseUrl/shirarium/organization-plan"
    $plan = Invoke-RestMethod -Method Get -Uri $planUri -Headers $headers
    $PlanFingerprint = [string]$plan.planFingerprint
}

if ([string]::IsNullOrWhiteSpace($PlanFingerprint)) {
    throw "Unable to resolve plan fingerprint. Generate organization plan first."
}

$payload = @{
    expectedPlanFingerprint = $PlanFingerprint
}

if ($SourcePath -and $SourcePath.Count -gt 0) {
    $payload["sourcePaths"] = $SourcePath
}

$json = $payload | ConvertTo-Json -Depth 6
$response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType "application/json" -Body $json
$response | ConvertTo-Json -Depth 12
