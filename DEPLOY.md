# Deployment auf Railway (Produktion)

HarmoniQ läuft als **Blazor Server** (.NET 10) mit **PostgreSQL**. Railway baut das mitgelieferte
`Dockerfile`, stellt eine managed Postgres bereit und terminiert HTTPS.

> Voraussetzungen erfüllt: Repo auf `github.com/kstockli/HarmoniQ`, Railway-Login via GitHub.

---

## 1. Projekt aus GitHub deployen
1. railway.app → **New Project** → **Deploy from GitHub repo** → `kstockli/HarmoniQ`.
2. Railway erkennt das **Dockerfile** automatisch und startet den ersten Build.
   (Der erste Deploy schlägt evtl. fehl, solange DB/Variablen fehlen – das ist ok.)

## 2. PostgreSQL hinzufügen
1. Im Projekt **New → Database → PostgreSQL**. Railway legt eine DB an und setzt u. a. `DATABASE_URL`.
2. Beim **Web-Service** unter **Variables** eine Referenz anlegen:
   ```
   DATABASE_URL = ${{Postgres.DATABASE_URL}}
   ```
   (Der App-Code wandelt diese URL automatisch ins Npgsql-Format um.)

## 3. Umgebungsvariablen (Web-Service → Variables)
Verschachtelte Config-Schlüssel mit **doppeltem Unterstrich** `__`:

```
DATABASE_URL                         = ${{Postgres.DATABASE_URL}}
Admin__Emails__0                     = me@q-no.ch

Authentication__Google__ClientId     = <deine Google Client ID>
Authentication__Google__ClientSecret = <dein Google Secret>
Authentication__Microsoft__ClientId  = <deine Microsoft Client ID>
Authentication__Microsoft__ClientSecret = <dein Microsoft Secret>

Email__Host     = www05.servertown.ch
Email__Port     = 465
Email__User     = me@q-no.ch
Email__Password = <SMTP-Passwort>
Email__From     = me@q-no.ch
```
(ASPNETCORE_ENVIRONMENT=Production ist schon im Dockerfile gesetzt.)

## 4. Domain
1. Web-Service → **Settings → Networking → Generate Domain** → du bekommst `…up.railway.app`
   (zum ersten Testen sofort nutzbar).
2. **Custom Domain** → `harmoniq.q-no.ch` eingeben. Railway zeigt ein **CNAME-Ziel**.
3. Im DNS von **q-no.ch** einen **CNAME** `harmoniq` → (Railway-Ziel) setzen. Zertifikat kommt automatisch (Let's Encrypt).

## 5. OAuth-Redirect-URIs ergänzen
- **Google Cloud Console** → Credentials → Redirect-URIs:
  - `https://harmoniq.q-no.ch/signin-google`
  - (optional zum Testen: `https://<app>.up.railway.app/signin-google`)
- **Azure App-Registrierung** → Authentication → Web → Redirect-URIs:
  - `https://harmoniq.q-no.ch/signin-microsoft`
  - (optional: `https://<app>.up.railway.app/signin-microsoft`)

## 6. Was beim ersten Start automatisch passiert
- EF-Core-**Migration** legt das Schema in der Railway-Postgres an.
- **DbSeeder** befüllt den John-Mackey-Katalog (5 Bands, ~79 Stücke, 9 Videos).
- **AdminInitializer** macht `me@q-no.ch` zum Admin (sobald registriert/eingeloggt).
- **DataProtection-Keys** liegen in der DB → Logins/Tokens überleben Redeploys.

> Die Luzern-Roster (Stadtmusik/JBL) werden **nicht** automatisch eingespielt (Einmal-Importer).
> Bei Bedarf später erneut über den Importer-Mechanismus oder den Admin-Editor pflegen.

## 7. Technische Hinweise
- **WebSockets:** von Railway unterstützt (Blazor-Server-Circuits laufen).
- **Eine Instanz:** Blazor Server braucht Sticky State → Replica-Anzahl auf **1** lassen
  (nicht horizontal skalieren ohne Sticky Sessions).
- **ForwardedHeaders:** aktiv → OAuth-Redirects zeigen korrekt auf `https`.
- **SMTP Port 465:** ausgehend nötig (servertown). Falls Mails nicht rausgehen → Railway-Support/Port prüfen.
- **Backups:** `pg_dump` gegen die Railway-DB (Connection-Daten im Postgres-Service → „Connect").

## 8. Updates ausrollen
`git push` auf den Default-Branch → Railway baut & deployt automatisch neu. Migrationen laufen
beim Start mit; Daten bleiben (Postgres ist entkoppelt).
