param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [string]$EngineBaseUrl = "http://localhost:8787"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$body = @{
    path = $Path
} | ConvertTo-Json

$response = Invoke-RestMethod `
    -Method Post `
    -Uri "$($EngineBaseUrl.TrimEnd('/'))/v1/parse-filename" `
    -ContentType "application/json" `
    -Body $body

$response | ConvertTo-Json -Depth 5
