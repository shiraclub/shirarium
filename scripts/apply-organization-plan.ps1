param(
    [Parameter(Mandatory = $true)]
    [string[]]$SourcePath,
    [string]$JellyfinBaseUrl = "http://localhost:8097",
    [string]$AccessToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$uri = "$($JellyfinBaseUrl.TrimEnd('/'))/Shirarium/apply-plan"
$headers = @{}

if ($AccessToken) {
    $headers["X-Emby-Token"] = $AccessToken
}

$payload = @{
    sourcePaths = $SourcePath
} | ConvertTo-Json -Depth 5

$response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType "application/json" -Body $payload
$response | ConvertTo-Json -Depth 10
