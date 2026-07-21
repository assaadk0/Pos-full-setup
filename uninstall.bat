@echo off
title Uninstalling Serial2Keyboard Service

:: Check for Administrator privileges
net session >nul 2>&1
if %errorLevel% == 0 goto :run

echo Requesting Administrator privileges (UAC)...
echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
echo UAC.ShellExecute "cmd.exe", "/c " ^& chr(34) ^& "%~f0" ^& chr(34), "", "runas", 1 >> "%temp%\getadmin.vbs"
"%temp%\getadmin.vbs"
del "%temp%\getadmin.vbs"
exit /b

:run
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
echo.
echo Press any key to close this window...
pause >nul
