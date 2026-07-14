<#
.SYNOPSIS
  Restore a Prod Postgres dump (pg_dump CUSTOM format, .dump) into the local Dev database.

.DESCRIPTION
  WARNING: the Dev database content is COMPLETELY REPLACED (schema "public" is dropped and
  rebuilt from the dump). The dump comes from Prod (owner "postgres"); with --no-owner /
  --no-privileges the restored objects belong to the local role afterwards.

  Precondition: the Dev app must NOT be running (it would hold DB connections). The script
  defensively terminates open connections to the target DB, but stopping the app first is cleanest.

.PARAMETER Dump
  Path to the .dump file. If omitted, the newest .dump in C:\Entw\HarmoniQBackup is used.

.PARAMETER Force
  Skip the confirmation prompt.

.EXAMPLE
  .\scripts\restore-dev-from-dump.ps1
  .\scripts\restore-dev-from-dump.ps1 -Dump "C:\Entw\HarmoniQBackup\backup_2026-07-14_23-16-34.dump" -Force
#>
param(
    [string]$Dump,
    [string]$BackupDir = "C:\Entw\HarmoniQBackup",
    [string]$DbHost    = "localhost",
    [int]   $Port      = 5432,
    [string]$Db        = "harmoniq",
    [string]$User      = "harmoniq",
    [string]$Password  = "sysadm",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# --- Locate Postgres tools (install dir, else PATH) ---
$pgBin     = "C:\Program Files\PostgreSQL\18\bin"
$psql      = Join-Path $pgBin "psql.exe"
$pgRestore = Join-Path $pgBin "pg_restore.exe"
if (-not (Test-Path $psql))      { $psql = "psql" }
if (-not (Test-Path $pgRestore)) { $pgRestore = "pg_restore" }

# --- Determine dump (newest if not given) ---
if ([string]::IsNullOrWhiteSpace($Dump)) {
    $newest = Get-ChildItem -Path $BackupDir -Filter *.dump -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $newest) { throw "No *.dump found in $BackupDir. Please pass -Dump." }
    $Dump = $newest.FullName
}
if (-not (Test-Path $Dump)) { throw "Dump not found: $Dump" }

$size = [math]::Round((Get-Item $Dump).Length / 1MB, 2)
Write-Host "Dump   : $Dump ($size MB)" -ForegroundColor Cyan
Write-Host "Target : $User@${DbHost}:$Port/$Db" -ForegroundColor Cyan

if (-not $Force) {
    Write-Host ""
    Write-Warning "Database '$Db' will be COMPLETELY REPLACED. Stop the Dev app first!"
    $answer = Read-Host "Continue? (yes/no)"
    if ($answer -notin @("yes", "y", "ja", "j")) { Write-Host "Aborted."; return }
}

# Provide password for all psql/pg_restore calls (this session only).
$env:PGPASSWORD = $Password
$common = @("-h", $DbHost, "-p", "$Port", "-U", $User)

# --- 1) Terminate open connections to the target DB (via maintenance DB 'postgres') ---
Write-Host "`n[1/3] Terminating open connections to '$Db' ..." -ForegroundColor Yellow
$terminate = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$Db' AND pid != pg_backend_pid();"
& $psql @common -d postgres -v ON_ERROR_STOP=0 -c $terminate | Out-Null

# --- 2) Reset schema 'public' (drops all tables/data of the Dev DB) ---
Write-Host "[2/3] Resetting schema 'public' ..." -ForegroundColor Yellow
$reset = "DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public; GRANT ALL ON SCHEMA public TO $User; GRANT ALL ON SCHEMA public TO public;"
& $psql @common -d $Db -v ON_ERROR_STOP=1 -c $reset
if ($LASTEXITCODE -ne 0) { throw "Schema reset failed (exit $LASTEXITCODE)." }

# --- 3) Restore the dump (no owner/privileges -> owned by the local user afterwards) ---
Write-Host "[3/3] Restoring dump (pg_restore) ..." -ForegroundColor Yellow
& $pgRestore @common -d $Db --no-owner --no-privileges --no-acl `
    --exit-on-error --single-transaction "$Dump"
if ($LASTEXITCODE -ne 0) {
    Write-Warning "pg_restore returned exit $LASTEXITCODE. With --single-transaction the restore was rolled back."
    throw "Restore failed."
}

Write-Host "`nDone. Dev DB '$Db' now matches the dump." -ForegroundColor Green
Write-Host "Note: on next app start EF may auto-apply newer Dev migrations." -ForegroundColor DarkGray
