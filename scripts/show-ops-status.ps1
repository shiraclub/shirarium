param(
    [string]$JellyfinBaseUrl = "http://localhost:8097",
    [string]$AccessToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$uri = "$($JellyfinBaseUrl.TrimEnd('/'))/Shirarium/ops-status"
$headers = @{}

if ($AccessToken) {
    $headers["X-Emby-Token"] = $AccessToken
}

$response = Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
$response | ConvertTo-Json -Depth 12
