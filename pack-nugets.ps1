# Build and Pack NuGet Package for Flowtex.DataAccess
# Version: 1.0.3

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = ".\nupkgs",
    [switch]$CleanFirst = $false
)

Write-Host "Building and packaging Flowtex.DataAccess library..." -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Output Path: $OutputPath" -ForegroundColor Yellow

# Clean if requested
if ($CleanFirst) {
    Write-Host "Cleaning solution..." -ForegroundColor Yellow
    dotnet clean --configuration $Configuration
    if (Test-Path $OutputPath) {
        Remove-Item $OutputPath -Recurse -Force
    }
}

# Create output directory
if (!(Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# Build and pack the main project
$project = "Flowtex.DataAccess.Infrastructure\Flowtex.DataAccess.Infrastructure.csproj"
$projectName = "Flowtex.DataAccess"

Write-Host "Building $projectName..." -ForegroundColor Cyan
dotnet build $project --configuration $Configuration --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed for $projectName"
    exit 1
}

Write-Host "Packing $projectName..." -ForegroundColor Cyan
dotnet pack $project --configuration $Configuration --no-build --output $OutputPath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Pack failed for $projectName"
    exit 1
}

Write-Host "Package created successfully!" -ForegroundColor Green
Write-Host "Package location: $OutputPath" -ForegroundColor Yellow

# List created packages
Write-Host "Created package:" -ForegroundColor Green
Get-ChildItem -Path $OutputPath -Filter "*.nupkg" | ForEach-Object {
    Write-Host "  - $($_.Name)" -ForegroundColor White
}

Write-Host "`nTo push packages to NuGet.org, use:" -ForegroundColor Yellow
Write-Host "  dotnet nuget push `"$OutputPath\*.nupkg`" --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY" -ForegroundColor Gray