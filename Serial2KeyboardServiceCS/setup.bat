@echo off
title Installing Serial2Keyboard Service
:: Check for Administrator privileges
net session >nul 2>&1
if %errorLevel% == 0 (
    goto :run
) else (
    echo Requesting Administrator privileges (UAC)...
    powershell -Command "Start-Process -FilePath '%0' -Verb RunAs"
    exit /b
)

:run
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File .\build_and_install.ps1
echo.
echo Press any key to close this window...
pause >nul
