# scripts/setup-benchmark-data.ps1
# Automates downloading of large Tier C datasets for benchmarking.

$BenchmarkDir = Join-Path (Join-Path $PSScriptRoot "..") "datasets"
$BenchmarkDir = Join-Path $BenchmarkDir "benchmark"
if (-not (Test-Path $BenchmarkDir)) {
    New-Item -ItemType Directory -Path $BenchmarkDir | Out-Null
}

$Datasets = @(
    @{
        Name = "tpb-titles-2023"
        Url  = "https://huggingface.co/datasets/d2mw/thepiratebay-categorized-titles-2023-04/resolve/main/titles-cats-only-2023-04-01.csv?download=true"
        File = "tpb-titles-2023.csv"
        Desc = "7.7 Million real-world torrent titles (TPB April 2023)"
    },
    @{
        Name = "magnetdb-lite-sample"
        Url  = "https://zenodo.org/record/10688434/files/matched_videos_sample.csv?download=1"
        File = "magnetdb-matched-sample.csv"
        Desc = "MagnetDB Matched Subset Sample (IMDb Ground Truth)"
    }
)

Write-Host "Shirarium Benchmark Data Setup" -ForegroundColor Cyan
Write-Host "-------------------------------"
Write-Host "Note: Full MagnetDB (27GB) requires manual download from Academic Torrents." -ForegroundColor Gray
Write-Host "Link: https://academictorrents.com/details/1ce8202af7a500469177ed99de5cd9bf66078de0" -ForegroundColor Gray
Write-Host ""

foreach ($ds in $Datasets) {
    $TargetPath = Join-Path $BenchmarkDir $ds.File
    Write-Host "Checking $($ds.Name)..." -NoNewline
    
    if (Test-Path $TargetPath) {
        Write-Host " [EXISTING]" -ForegroundColor Green
    } else {
        Write-Host " [DOWNLOADING]" -ForegroundColor Yellow
        Write-Host "Source: $($ds.Url)" -ForegroundColor Gray
        try {
            Invoke-WebRequest -Uri $ds.Url -OutFile $TargetPath -ErrorAction Stop
            Write-Host "Download complete: $TargetPath" -ForegroundColor Green
        } catch {
            Write-Host "Failed to download $($ds.Name): $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host "`nSetup Complete." -ForegroundColor Cyan
Write-Host "You can now run benchmarks against these files using engine/tests/benchmark.py"
