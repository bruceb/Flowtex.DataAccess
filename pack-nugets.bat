@echo off
REM Thin wrapper around pack-nugets.ps1 so there is one packaging implementation.
REM Any arguments are passed through, e.g.  pack-nugets.bat -CleanFirst -SkipTests
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0pack-nugets.ps1" %*
exit /b %errorlevel%
