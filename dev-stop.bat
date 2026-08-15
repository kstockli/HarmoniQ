@echo off
REM ============================================================
REM  HarmoniQ Dev-Server stoppen (Prozess auf Port 5294).
REM ============================================================
setlocal enabledelayedexpansion
set PORT=5294
set FOUND=

for /f "tokens=5" %%a in ('netstat -ano ^| findstr LISTENING ^| findstr ":%PORT% "') do (
    echo Stoppe HarmoniQ Dev - PID %%a ...
    taskkill /PID %%a /T /F >nul 2>&1
    set FOUND=1
)

if not defined FOUND (
    echo Kein Dev-Server auf Port %PORT% aktiv.
) else (
    echo Dev-Server gestoppt.
)
endlocal
