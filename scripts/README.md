# Scripts

Hilfsskripte für die lokale Entwicklung.

## restore-dev-from-dump.ps1 — Prod-Dump in die Dev-DB laden

Spielt einen Postgres-Dump aus dem Prod-System (Railway) in die lokale Dev-Datenbank ein.

> ⚠️ **Der Inhalt der Dev-DB wird vollständig ersetzt.** Das Schema `public` wird gelöscht
> und aus dem Dump neu aufgebaut. Aktuelle lokale Testdaten sind danach weg.

### Voraussetzungen

- **PostgreSQL-Client-Tools** (`psql`, `pg_restore`) — erwartet unter
  `C:\Program Files\PostgreSQL\18\bin` (sonst wird der PATH genutzt).
- **Dump-Datei** im Format `pg_dump --format=custom` (Endung `.dump`), z. B. aus dem
  Railway-Backup. Standard-Ablage: `C:\Entw\HarmoniQBackup\`.
- **Dev-App gestoppt** — sie hält sonst offene DB-Verbindungen. Prozess beenden:
  ```powershell
  taskkill /F /IM HarmoniQ.Web.exe    # bzw. den laufenden dotnet-Host beenden
  ```
  (Das Skript kappt offene Verbindungen zwar zusätzlich selbst, sauberer ist App-Stopp vorher.)

### Aufruf

Wegen der PowerShell-ExecutionPolicy am einfachsten über den **`.cmd`-Wrapper**
(umgeht die Policy nur für diesen Aufruf, ohne Systemeinstellung zu ändern):

```powershell
# Neuesten Dump aus C:\Entw\HarmoniQBackup nehmen, ohne Rückfrage
.\scripts\restore-dev-from-dump.cmd -Force

# Bestimmten Dump wählen
.\scripts\restore-dev-from-dump.cmd -Dump "C:\Entw\HarmoniQBackup\backup_2026-07-14_23-16-34.dump" -Force
```

Alternativ direkt die `.ps1` mit einmaligem Bypass:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\restore-dev-from-dump.ps1 -Force
```

Ohne `-Force` fragt das Skript vor dem Überschreiben nach.

### Parameter

| Parameter    | Default                     | Bedeutung |
|--------------|-----------------------------|-----------|
| `-Dump`      | *neueste `.dump` im BackupDir* | Pfad zur Dump-Datei |
| `-BackupDir` | `C:\Entw\HarmoniQBackup`    | Ordner, in dem der neueste Dump gesucht wird |
| `-DbHost`    | `localhost`                 | DB-Host |
| `-Port`      | `5432`                      | DB-Port |
| `-Db`        | `harmoniq`                  | Ziel-Datenbank |
| `-User`      | `harmoniq`                  | DB-Benutzer |
| `-Password`  | `sysadm`                    | DB-Passwort (Dev-Credential) |
| `-Force`     | *(aus)*                     | Überspringt die Sicherheitsabfrage |

### Was das Skript macht

1. **Verbindungen kappen** — beendet offene Sessions zur Ziel-DB (über die Wartungs-DB `postgres`).
2. **Schema zurücksetzen** — `DROP SCHEMA public CASCADE; CREATE SCHEMA public;` (+ Grants).
3. **Restore** — `pg_restore --no-owner --no-privileges --no-acl --exit-on-error --single-transaction`.
   - `--no-owner/--no-privileges/--no-acl`: der Prod-Dump gehört dem Rollen-User `postgres`;
     lokal existiert der nicht → die Objekte gehören danach dem lokalen `harmoniq`-User.
   - `--single-transaction`: alles-oder-nichts; bei einem Fehler bleibt die DB unverändert.

### Danach

- **Dev-App wieder starten.** Beim Start gleicht EF Core die Migrationen ab
  (`__EFMigrationsHistory` aus dem Dump ↔ lokale `Migrations/`):
  - Sind sie identisch → nichts zu tun.
  - Ist der **Dev-Code neuer** → EF wendet die fehlenden Migrationen automatisch an.
  - Ist der **Dump neuer** als der Dev-Code → Code zuerst aktualisieren (`git pull`),
    sonst kann der Start fehlschlagen.

### Hinweise

- Das Skript ist bewusst **rein ASCII** gehalten — Windows PowerShell 5.1 liest `.ps1` ohne
  BOM als ANSI; Umlaute würden sonst die Zeichenkodierung verschieben und Parser-Fehler erzeugen.
- `-Password sysadm` ist die bekannte **Dev**-Credential; bei Bedarf per `-Password` überschreiben.
