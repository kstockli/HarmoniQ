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

Email__Resend__ApiKey = <Resend API-Key, re_...>
Email__From           = harmoniq@q-no.ch
```
(ASPNETCORE_ENVIRONMENT=Production ist schon im Dockerfile gesetzt.)

> **Mailversand:** Railway **blockt ausgehenden SMTP (Port 465/587)**. Daher läuft der Versand
> in Prod über die **Resend-HTTPS-API** (`ResendEmailSender`): Sobald `Email__Resend__ApiKey`
> gesetzt ist, schaltet die App automatisch von SMTP auf Resend. Die Absender-Domain (`q-no.ch`)
> muss bei Resend **verifiziert** sein (SPF/DKIM-DNS-Einträge), damit an beliebige Empfänger
> gesendet werden darf. Lokal/Dev bleibt es bei SMTP (MailKit).

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
- **Backups:** `pg_dump` gegen die Railway-DB (Connection-Daten im Postgres-Service → „Connect").

## 8. Updates ausrollen
`git push` auf den Default-Branch → Railway baut & deployt automatisch neu. Migrationen laufen
beim Start mit; Daten bleiben (Postgres ist entkoppelt).

## 9. Bekannte Stolpersteine (gelöst – nicht wieder hineinlaufen!)
Diese Punkte haben beim Erst-Deployment Zeit gekostet; sie sind im Code/Dockerfile bereits gelöst:

- **OAuth `redirect_uri_mismatch` hinter dem Railway-Proxy.** Der Edge-Proxy terminiert TLS und
  spricht intern `http` mit dem Container → die OAuth-Middleware baute die `redirect_uri` mit `http`.
  Lösungen in `Program.cs`:
  1. `UseForwardedHeaders` (X-Forwarded-Proto/-For, `ForwardLimit = null`, KnownProxies/Networks geleert).
  2. In Produktion zusätzlich `ctx.Request.Scheme = "https"` erzwingen.
  3. **`UseAuthentication`/`UseAuthorization` EXPLIZIT** und **nach** den obigen Schritten einhängen –
     sonst hängt das Framework die Auth automatisch zu früh in die Pipeline und baut wieder `http`-URLs.
- **`Cannot load library libgssapi_krb5.so.2`** beim DB-Zugriff. Npgsql versucht eine
  GSSAPI/Kerberos-Aushandlung; die Lib fehlt im schlanken Runtime-Image. → im **Dockerfile**
  `libgssapi-krb5-2` nachinstalliert.
- **Crawler-JS-Rendering (Playwright/Chromium) liefert auf Railway nur die „Hülle".** Symptom: SPA-Seiten
  wie `https://www.emf26.ch/vereine` rendern leer (nur das äussere Gerüst). Drei Ursachen, alle gelöst:
  1. **Production lädt `appsettings.Development.json` NICHT** → `Crawler:RenderingAktiv` war `false`. → im
     **Dockerfile** `ENV Crawler__RenderingAktiv=true` gesetzt.
  2. **Kein Browser im Runtime-Image.** → Final-Stage ist jetzt `mcr.microsoft.com/playwright/dotnet:v1.60.0-noble`
     (Chromium + System-Libs passend zu Microsoft.Playwright 1.60.0 enthalten). Damit das Image nicht an die
     .NET-Version des Playwright-Images gebunden ist, wird die App **self-contained** (`-r linux-x64
     --self-contained`) veröffentlicht. **Wichtig:** Bei Update der `Microsoft.Playwright`-NuGet-Version den
     Image-Tag `v<version>-noble` mitziehen, sonst passen die Browser-Revisionen nicht.
  3. **Chromium startet als root nicht.** Im Container läuft der Prozess als root → `PlaywrightRenderer` startet
     den Browser mit `--no-sandbox --disable-dev-shm-usage --disable-gpu …`.
  4. **`Target crashed` (Renderer-Absturz, meist OOM).** Symptom nach erfolgreichem Browser-Start: „Rendern
     fehlgeschlagen … Target crashed" → Fallback HTTP → wieder nur die Hülle. Chromium ist speicherhungrig.
     Lösungen in `PlaywrightRenderer`: **schwere Ressourcen blocken** (Bilder/Medien/Fonts/CSS via
     `page.RouteAsync`-Abort – für die Link-/Text-Ernte unnötig) + GPU-Flags. **Falls es weiter crasht:**
     der Railway-Service braucht genug RAM – Chromium + .NET zusammen realistisch **≥ 1 GB** (lieber 2 GB).
  > Der Renderer fällt bei fehlendem Browser **still auf reinen HTTP-Fetch** zurück (kein Crash) – deshalb sieht
  > man nur die leere Hülle statt eines Fehlers. Im Log steht dann „Playwright/Chromium nicht verfügbar …".
- **SMTP blockiert.** Railway lässt ausgehenden SMTP (465/587) nicht zu → Mailversand über die
  **Resend-HTTPS-API** (siehe §3).
- **Custom-Domain-Port:** Bei der Custom Domain den **Container-Port (8080)** angeben, nicht 443
  (443 macht Railway selbst).
- **Google OAuth „Testmodus".** Solange der OAuth-Zustimmungsbildschirm auf **„Testing"** steht,
  können sich nur eingetragene Testnutzer anmelden. Für öffentliche Nutzung im
  **Google Cloud Console → OAuth-Zustimmungsbildschirm** auf **„In Produktion"** veröffentlichen
  (bei nur Basis-Scopes E-Mail/Profil ohne aufwändigen Review).
- **Microsoft & Entra:** App-Registrierung als *multi-tenant + personal accounts*. Externe Nutzer
  werden **nicht** als Gäste im eigenen Tenant angelegt – ihre Identität lebt nur in `AspNetUsers`.
