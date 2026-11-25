# Build, test and pack the Flowtex.DataAccess NuGet packages.
#
# Produces, for every target framework in Directory.Build.props (net10.0 and net11.0):
#   Flowtex.DataAccess.Abstractions  (no EF Core dependency)
#   Flowtex.DataAccess               (depends on Flowtex.DataAccess.Abstractions + EF Core)
#
# The version comes from <Version> in Directory.Build.props; it is not repeated here.

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = ".\nupkgs",
    [switch]$CleanFirst,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

function Invoke-Step {
    param([string]$Name, [scriptblock]$Command)
    Write-Host "`n$Name..." -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$Name failed (exit code $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
}

$solution = "Flowtex.DataAccess.slnx"
$version = ([xml](Get-Content "Directory.Build.props")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

Write-Host "Packaging Flowtex.DataAccess $version" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Output Path:   $OutputPath" -ForegroundColor Yellow

if ($CleanFirst) {
    Invoke-Step "Cleaning" { dotnet clean $solution --configuration $Configuration --nologo }
    if (Test-Path $OutputPath) { Remove-Item $OutputPath -Recurse -Force }
}

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

Invoke-Step "Restoring" { dotnet restore $solution --nologo }
Invoke-Step "Building" { dotnet build $solution --configuration $Configuration --no-restore --nologo }

if (-not $SkipTests) {
    Invoke-Step "Testing" { dotnet test "Tests\Flowtex.DataAccess.Tests\Flowtex.DataAccess.Tests.csproj" --configuration $Configuration --no-build }
}

$packages = @(
    "Flowtex.DataAccess.ApplicationAbstractions\Flowtex.DataAccess.ApplicationAbstractions.csproj",
    "Flowtex.DataAccess.Infrastructure\Flowtex.DataAccess.Infrastructure.csproj"
)
foreach ($project in $packages) {
    Invoke-Step "Packing $(Split-Path $project -Leaf)" { dotnet pack $project --configuration $Configuration --no-build --output $OutputPath --nologo }
}

Write-Host "`nCreated packages:" -ForegroundColor Green
Get-ChildItem -Path $OutputPath -Filter "*$version*nupkg" | ForEach-Object {
    Write-Host "  - $($_.Name)" -ForegroundColor White
}

Write-Host "`nTo push packages to NuGet.org, use:" -ForegroundColor Yellow
Write-Host "  dotnet nuget push `"$OutputPath\*.$version.nupkg`" --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY" -ForegroundColor Gray
