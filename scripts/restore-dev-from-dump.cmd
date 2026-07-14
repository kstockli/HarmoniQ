@echo off
REM Wrapper: umgeht die PowerShell-ExecutionPolicy nur fuer diesen Aufruf.
REM Reicht alle Argumente an das PS-Skript weiter, z. B.:
REM   scripts\restore-dev-from-dump.cmd -Force
REM   scripts\restore-dev-from-dump.cmd -Dump "C:\Entw\HarmoniQBackup\backup_2026-07-14_23-16-34.dump" -Force
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0restore-dev-from-dump.ps1" %*
