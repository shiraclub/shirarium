param(
    [Parameter(Mandatory = $true)]
    [string[]]$SourcePath,
    [string]$JellyfinBaseUrl = "http://localhost:8097",
    [string]$PlanFingerprint,
    [string]$AccessToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$uri = "$($JellyfinBaseUrl.TrimEnd('/'))/Shirarium/apply-plan"
$headers = @{}

if ($AccessToken) {
    $headers["X-Emby-Token"] = $AccessToken
}

if (-not $PlanFingerprint) {
    $planUri = "$($JellyfinBaseUrl.TrimEnd('/'))/Shirarium/organization-plan"
    $plan = Invoke-RestMethod -Method Get -Uri $planUri -Headers $headers
    $PlanFingerprint = $plan.planFingerprint
}

if (-not $PlanFingerprint) {
    throw "Unable to resolve plan fingerprint. Generate organization plan first."
}

$payload = @{
    expectedPlanFingerprint = $PlanFingerprint
    sourcePaths = $SourcePath
} | ConvertTo-Json -Depth 5

$response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType "application/json" -Body $payload
$response | ConvertTo-Json -Depth 10
