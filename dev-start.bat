@echo off
REM ============================================================
REM  HarmoniQ Dev-Server starten  ->  http://localhost:5294
REM  Laeuft in einem eigenen Fenster (mit Live-Logs).
REM  Stoppen mit dev-stop.bat (oder das Fenster schliessen).
REM ============================================================
cd /d "%~dp0"

REM Schon aktiv? (Port 5294 belegt) -> nicht doppelt starten.
netstat -ano | findstr LISTENING | findstr ":5294 " >nul 2>&1
if %errorlevel%==0 (
    echo HarmoniQ Dev laeuft bereits auf http://localhost:5294 .
    echo Zum Neustart zuerst dev-stop.bat ausfuehren.
    pause
    exit /b 0
)

echo Starte HarmoniQ Dev auf http://localhost:5294 ...
start "HarmoniQ Dev" cmd /k dotnet run --project src\HarmoniQ.Web\HarmoniQ.Web.csproj --urls http://localhost:5294
echo.
echo Fenster "HarmoniQ Dev" wurde geoeffnet. Der Start (Build) dauert ein paar Sekunden.
echo Danach im Browser: http://localhost:5294
