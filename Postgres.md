# PostgreSQL – Notizen & Spickzettel (für Oracle-Umsteiger)

Kontext: HarmoniQ wird auf PostgreSQL umgestellt (Dev = Prod). Diese Datei sammelt das
Wichtigste für den Einstieg – speziell für jemanden, der **Oracle** gut kennt.

---

## 1. Tooling – was entspricht was?

| Oracle | PostgreSQL | Bemerkung |
|---|---|---|
| **SQL\*Plus** (`sqlplus`) | **`psql`** | Interaktive Kommandozeile. Meta-Befehle beginnen mit `\` statt mit Oracle-Spezialsyntax. |
| **SQL Developer** | **pgAdmin 4** (kommt mit dem Installer) oder **DBeaver** (frei, sehr beliebt) | GUI zum Browsen/Abfragen. DBeaver kann auch Oracle – guter „Brücken"-Client. |
| **TNS / `tnsnames.ora`** | Connection-String / `host port dbname user` | Kein TNS-Listener-Konzept wie bei Oracle; Verbindung direkt über Host/Port (Standard **5432**). |
| **`expdp` / `impdp`** (Data Pump) | **`pg_dump`** / **`pg_restore`** (+ `pg_dumpall` für ganze Instanz) | Logisches Backup. |
| **RMAN** | `pg_basebackup` + WAL-Archiving / Tools wie pgBackRest | Physisches Backup/PITR. |
| **AWR / `v$`-Views** | `pg_stat_*`-Views, `EXPLAIN (ANALYZE, BUFFERS)`, Extension `pg_stat_statements` | Performance-Analyse. |
| **`DBA_/ALL_/USER_`-Views** | `information_schema.*` + `pg_catalog.*` (`pg_class`, `pg_attribute`, …) | Metadaten. |
| **Enterprise Manager** | pgAdmin-Dashboard / extern (Grafana etc.) | |

---

## 2. psql – die wichtigsten Meta-Befehle (≈ SQL\*Plus)

```
psql -U postgres -h localhost -d harmoniq    -- verbinden
\l            Datenbanken auflisten           (≈ kein direktes Oracle-Pendant)
\c harmoniq   Datenbank wechseln              (≈ CONNECT user/pw@db)
\dn           Schemas auflisten
\dt           Tabellen im aktuellen Schema    (≈ SELECT * FROM user_tables)
\dt *.*       Tabellen aller Schemas
\d tabelle    Tabelle beschreiben             (≈ DESC tabelle)
\d+ tabelle   Tabelle inkl. Details/Indizes
\di           Indizes
\dv           Views
\df           Funktionen
\du           Rollen/User auflisten           (≈ SELECT * FROM dba_users)
\x            Erweiterte Anzeige an/aus        (≈ guter Ersatz für breite Spalten)
\timing       Ausführungszeit anzeigen
\e            Letzte Query im Editor öffnen
\i datei.sql  SQL-Datei ausführen             (≈ @datei.sql)
\conninfo     aktuelle Verbindung anzeigen
\password     Passwort der aktuellen Rolle ändern
\q            beenden                          (≈ EXIT)
\?            Hilfe zu Meta-Befehlen
\h SELECT     SQL-Syntaxhilfe                  (≈ keine, schön!)
```

Statement-Ende mit `;` (wie Oracle). **Kein** `/` zum Ausführen von Blöcken nötig.

---

## 3. Wichtigste Unterschiede zu Oracle

- **Identifier-Casing (umgekehrt!):** Oracle faltet unquoted Namen zu **GROSS**, Postgres zu
  **klein**. `SELECT Name FROM Person` → sucht `name`/`person`. Mit `"Person"` (Anführungszeichen)
  wird exakt unterschieden. **EF Core quotet immer** → unsere Tabellen heißen z. B. `"Personen"`
  (gross geschrieben, daher in psql immer mit `"..."` ansprechen).
- **Auto-Werte:** `GENERATED ALWAYS AS IDENTITY` oder `serial`/`bigserial` statt Oracle-Sequence+Trigger.
  (Sequences gibt es auch: `CREATE SEQUENCE`, `nextval('…')`.)
- **Kein `DUAL`:** `SELECT 1;` geht ohne FROM. (`SELECT now();`)
- **NULL & Leerstring sind verschieden** (Oracle behandelt `''` wie NULL – Postgres **nicht**!). Wichtiger Stolperstein.
- **Datentypen:** `varchar2` → `varchar`/`text` (text ist in PG ohne Längen-Nachteil), `number` →
  `numeric`/`integer`/`bigint`, `date` (Oracle = Datum+Zeit!) → in PG `date` = nur Datum,
  Datum+Zeit = `timestamp`/**`timestamptz`** (mit Zeitzone – empfohlen). `clob` → `text`, `blob` → `bytea`.
  Stark: **`jsonb`**, `uuid`, Arrays.
- **Strings:** Verkettung `||` (gleich). `NVL` → **`COALESCE`**. `DECODE` → `CASE`. `SYSDATE` → `now()`/`current_timestamp`.
  `ROWNUM`/`FETCH FIRST` → `LIMIT n OFFSET m` (oder `FETCH FIRST n ROWS ONLY` geht auch).
- **PL/SQL → PL/pgSQL:** sehr ähnlich (`DECLARE/BEGIN/END`), aber: **keine Packages**, Funktionen/Prozeduren
  einzeln (`CREATE FUNCTION` / `CREATE PROCEDURE`), Exceptions via `RAISE`.
- **MVCC** wie Oracle, ABER: **VACUUM/Autovacuum** ist PG-spezifisch und essenziell. Tote Zeilen
  („dead tuples") werden durch Autovacuum aufgeräumt; bei viel UPDATE/DELETE → `VACUUM (ANALYZE)`,
  Bloat im Auge behalten. `ANALYZE` aktualisiert Statistiken (≈ `DBMS_STATS`).
- **Transaktionen:** DDL ist **transaktional** (CREATE TABLE etc. kann man zurückrollen – anders als Oracle!).
- **Schemas statt „User=Schema":** In Oracle ≈ ein User = ein Schema. In PG sind **Rollen** (User/Gruppen)
  und **Schemas** getrennt; Default-Schema heisst `public`.
- **Berechtigungen:** `GRANT`/`REVOKE` ähnlich; Rollen können `LOGIN` haben oder nicht (Gruppen-Rollen).

---

## 4. Installation unter Windows (nativ)

**Variante A – EDB-Installer (empfohlen, GUI):**
1. Download: https://www.postgresql.org/download/windows/ → „Download the installer" (EDB).
   Aktuelle Hauptversion (z. B. **17.x**) wählen.
2. Installer starten. Komponenten: **PostgreSQL Server**, **pgAdmin 4**, **Command Line Tools**
   (Stack Builder kann man abwählen).
3. **Data Directory:** Standard belassen (`C:\Program Files\PostgreSQL\17\data`).
4. **Passwort für den Superuser `postgres`** setzen → **gut merken/notieren** (das ist der „SYS/SYSTEM" von PG).
5. **Port:** `5432` (Standard) belassen.
6. **Locale:** Standard/„Default" ist ok (für CH evtl. `German_Switzerland.1252`, aber Default genügt).
7. Fertig installieren. Der Dienst **„postgresql-x64-17"** läuft danach automatisch (Windows-Dienst).

**Variante B – winget (Kommandozeile, falls verfügbar):**
```powershell
winget install PostgreSQL.PostgreSQL.17
```
(Setzt ggf. trotzdem einen GUI-Schritt voraus und braucht Admin-Rechte.)

**Nach der Installation – `psql` in die PATH (für die aktuelle PS-Sitzung):**
```powershell
$env:Path += ";C:\Program Files\PostgreSQL\17\bin"
psql --version
```
Dauerhaft: System-Umgebungsvariable `Path` um `C:\Program Files\PostgreSQL\17\bin` ergänzen.

---

## 5. Erste Schritte: DB & Rolle für HarmoniQ

Als Superuser verbinden (fragt nach dem bei der Installation gesetzten Passwort):
```powershell
psql -U postgres -h localhost
```
Dann in psql:
```sql
-- eigene Login-Rolle (≈ Schema-User in Oracle)
CREATE ROLE harmoniq WITH LOGIN PASSWORD 'EinSicheresPasswort';

-- eigene Datenbank, gehört der Rolle
CREATE DATABASE harmoniq OWNER harmoniq;

-- verbinden und prüfen
\c harmoniq
\conninfo
\q
```

**Connection-String** für die App (Npgsql):
```
Host=localhost;Port=5432;Database=harmoniq;Username=harmoniq;Password=EinSicheresPasswort
```
(In der App via user-secrets / Umgebungsvariable, nicht im Klartext einchecken.)

---

## 6. Backup / Restore (lokal & Prod)

```powershell
# Logisches Backup einer DB (Custom-Format, für pg_restore)
pg_dump -U harmoniq -h localhost -d harmoniq -F c -f harmoniq.dump

# Wiederherstellen
pg_restore -U harmoniq -h localhost -d harmoniq harmoniq.dump

# Reines SQL-Skript-Backup
pg_dump -U harmoniq -d harmoniq -f harmoniq.sql
```

---

## 7. Windows-Dienst steuern (PowerShell, als Admin)

```powershell
Get-Service postgresql*              # Status
Restart-Service postgresql-x64-17    # Neustart
Stop-Service postgresql-x64-17       # Stoppen
Start-Service postgresql-x64-17      # Starten
```

---

## 8. Wichtige Config-Dateien (im Data Directory)

- **`postgresql.conf`** – Server-Einstellungen (Speicher, Logging, `shared_buffers`, …).
- **`pg_hba.conf`** – **Zugriffssteuerung** (wer darf von wo wie authentifizieren). Wichtig zu verstehen!
  Zeilenformat: `TYPE  DATABASE  USER  ADDRESS  METHOD` (z. B. `host all all 127.0.0.1/32 scram-sha-256`).
- Nach Änderungen: `Restart-Service` bzw. in psql `SELECT pg_reload_conf();` (für reload-bare Settings).

---

## 9. Lernfahrplan (tief)
1. psql sicher bedienen (`\?`, `\d`, `\x`, `\timing`).
2. Rollen/Schemas/Rechte verstehen (`\du`, `\dn`, `GRANT`).
3. Datentypen + `jsonb` ausprobieren.
4. `EXPLAIN (ANALYZE, BUFFERS)` lesen, Indizes (B-Tree/GIN) anlegen.
5. VACUUM/Autovacuum & Statistiken verstehen.
6. Backup/Restore üben (`pg_dump`/`pg_restore`).
7. PL/pgSQL-Funktion schreiben.
8. Prod-Vergleich: managed Postgres (Railway) vs. selbst-verwaltet (lokal).
