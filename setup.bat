@echo off
title Installing Serial2Keyboard Service

:: Check for Administrator privileges
net session >nul 2>&1
if %errorLevel% == 0 goto :run

echo Requesting Administrator privileges (UAC)...
:: Self-elevation via VBScript using Short Path (8.3 format) to avoid space issues
echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
echo UAC.ShellExecute "%~s0", "", "", "runas", 1 >> "%temp%\getadmin.vbs"
"%temp%\getadmin.vbs"
del "%temp%\getadmin.vbs"
exit /b

:run
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File .\build_and_install.ps1
echo.
echo Press any key to close this window...
pause >nul
