param(
    [Parameter(Mandatory = $true)]
    [string]$ReviewId,
    [string]$JellyfinBaseUrl = "http://localhost:8097",
    [string]$AccessToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$baseUrl = $JellyfinBaseUrl.TrimEnd("/")
$uri = "$baseUrl/shirarium/review-locks/$([uri]::EscapeDataString($ReviewId))/apply"
$headers = @{}

if ($AccessToken) {
    $headers["X-Emby-Token"] = $AccessToken
}

$response = Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType "application/json" -Body "{}"
$response | ConvertTo-Json -Depth 12
