@echo off
REM Build and Pack NuGet Package for Flowtex.DataAccess
REM Version: 1.0.1

echo Building and packaging Flowtex.DataAccess library...

REM Create output directory
if not exist "nupkgs" mkdir nupkgs

REM Restore packages first
echo Restoring NuGet packages...
dotnet restore

REM Build and pack the main project
echo Building and packing Flowtex.DataAccess...
dotnet pack "Flowtex.DataAccess.Infrastructure\Flowtex.DataAccess.Infrastructure.csproj" --configuration Release --output nupkgs
if errorlevel 1 goto :error

echo.
echo Package created successfully!
echo Package location: nupkgs\
echo.
echo Created package:
dir /b nupkgs\*.nupkg

echo.
echo To push packages to NuGet.org, use:
echo   dotnet nuget push "nupkgs\*.nupkg" --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY

pause
goto :end

:error
echo ERROR: Package creation failed!
pause
exit /b 1

:end