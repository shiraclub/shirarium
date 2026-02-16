param(
    [Parameter(Mandatory = $true)]
    [string]$ReviewId,
    [string]$JellyfinBaseUrl = "http://localhost:8097",
    [string]$AccessToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$baseUrl = $JellyfinBaseUrl.TrimEnd("/")
$uri = "$baseUrl/shirarium/review-locks/$([uri]::EscapeDataString($ReviewId))"
$headers = @{}

if ($AccessToken) {
    $headers["X-Emby-Token"] = $AccessToken
}

$response = Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
$response | ConvertTo-Json -Depth 20
