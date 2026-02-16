param(
    [string[]]$SourcePath,
    [string]$JellyfinBaseUrl = "http://localhost:8097",
    [string]$PlanFingerprint,
    [string]$PreflightToken,
    [string]$AccessToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$baseUrl = $JellyfinBaseUrl.TrimEnd("/")
$uri = "$baseUrl/shirarium/apply-reviewed-plan"
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

if ([string]::IsNullOrWhiteSpace($PreflightToken)) {
    $preflightUri = "$baseUrl/shirarium/preflight-reviewed-plan"
    $preflightPayload = @{
        expectedPlanFingerprint = $PlanFingerprint
    }

    if ($SourcePath -and $SourcePath.Count -gt 0) {
        $preflightPayload["sourcePaths"] = $SourcePath
    }

    $preflightJson = $preflightPayload | ConvertTo-Json -Depth 6
    $preflightResponse = Invoke-RestMethod -Method Post -Uri $preflightUri -Headers $headers -ContentType "application/json" -Body $preflightJson
    $PreflightToken = [string]$preflightResponse.preflightToken
}

if ([string]::IsNullOrWhiteSpace($PreflightToken)) {
    throw "Unable to resolve preflight token. Run preflight-reviewed-plan first."
}

$payload = @{
    expectedPlanFingerprint = $PlanFingerprint
    preflightToken = $PreflightToken
}

if ($SourcePath -and $SourcePath.Count -gt 0) {
    $payload["sourcePaths"] = $SourcePath
}

$json = $payload | ConvertTo-Json -Depth 6
$response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType "application/json" -Body $json
$response | ConvertTo-Json -Depth 12
