param(
    [string]$SourcePath,
    [ValidateSet("move", "skip", "none", "conflict")]
    [string]$Action,
    [AllowEmptyString()]
    [string]$TargetPath,
    [switch]$Remove,
    [string]$PayloadPath,
    [string]$JellyfinBaseUrl = "http://localhost:8097",
    [string]$PlanFingerprint,
    [string]$AccessToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$baseUrl = $JellyfinBaseUrl.TrimEnd("/")
$uri = "$baseUrl/shirarium/organization-plan-entry-overrides"
$headers = @{}

if ($AccessToken) {
    $headers["X-Emby-Token"] = $AccessToken
}

function Resolve-PlanFingerprint {
    param(
        [string]$CurrentPlanFingerprint
    )

    if (-not [string]::IsNullOrWhiteSpace($CurrentPlanFingerprint)) {
        return $CurrentPlanFingerprint
    }

    $planUri = "$baseUrl/shirarium/organization-plan"
    $plan = Invoke-RestMethod -Method Get -Uri $planUri -Headers $headers
    return [string]$plan.planFingerprint
}

if (-not [string]::IsNullOrWhiteSpace($PayloadPath)) {
    if (-not (Test-Path -Path $PayloadPath)) {
        throw "Payload file not found: $PayloadPath"
    }

    $payloadObject = Get-Content -Path $PayloadPath -Raw | ConvertFrom-Json -Depth 20

    if (-not [string]::IsNullOrWhiteSpace($PlanFingerprint)) {
        $payloadObject.expectedPlanFingerprint = $PlanFingerprint
    }
    elseif ($null -eq $payloadObject.expectedPlanFingerprint -or [string]::IsNullOrWhiteSpace([string]$payloadObject.expectedPlanFingerprint)) {
        $payloadObject.expectedPlanFingerprint = Resolve-PlanFingerprint -CurrentPlanFingerprint $PlanFingerprint
    }

    if ([string]::IsNullOrWhiteSpace([string]$payloadObject.expectedPlanFingerprint)) {
        throw "Unable to resolve plan fingerprint. Generate organization plan first."
    }

    $payload = $payloadObject | ConvertTo-Json -Depth 20
}
else {
    if ([string]::IsNullOrWhiteSpace($SourcePath)) {
        throw "SourcePath is required when PayloadPath is not provided."
    }

    if (-not $Remove.IsPresent -and -not $PSBoundParameters.ContainsKey("Action") -and -not $PSBoundParameters.ContainsKey("TargetPath")) {
        throw "Specify -Action or -TargetPath (or use -Remove)."
    }

    $resolvedPlanFingerprint = Resolve-PlanFingerprint -CurrentPlanFingerprint $PlanFingerprint
    if ([string]::IsNullOrWhiteSpace($resolvedPlanFingerprint)) {
        throw "Unable to resolve plan fingerprint. Generate organization plan first."
    }

    $patch = @{
        sourcePath = $SourcePath
        remove = $Remove.IsPresent
    }

    if ($PSBoundParameters.ContainsKey("Action")) {
        $patch["action"] = $Action
    }

    if ($PSBoundParameters.ContainsKey("TargetPath")) {
        $patch["targetPath"] = $TargetPath
    }

    $payload = @{
        expectedPlanFingerprint = $resolvedPlanFingerprint
        patches = @($patch)
    } | ConvertTo-Json -Depth 10
}

$response = Invoke-RestMethod -Method Patch -Uri $uri -Headers $headers -ContentType "application/json" -Body $payload
$response | ConvertTo-Json -Depth 12
