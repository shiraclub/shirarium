param(
    [string]$JellyfinBaseUrl = "http://localhost:8097",
    [string]$AccessToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$uri = "$($JellyfinBaseUrl.TrimEnd('/'))/Shirarium/plan-organize"
$headers = @{}

if ($AccessToken) {
    $headers["X-Emby-Token"] = $AccessToken
}

$response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers
$response | ConvertTo-Json -Depth 10
