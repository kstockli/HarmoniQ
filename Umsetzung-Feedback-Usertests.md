# Umsetzung – Feedback aus User-Tests (Runde 1)

Gesammeltes Feedback aus den ersten User-Tests, priorisiert und mit geplanter Lösung.
Leitlinie (Entscheid Kuno): **Fokus auf Vereinfachen.** Reihenfolge: Performance zuerst,
dann die vereinfachenden Punkte, am Schluss ein **Erklär-Video + Anwendungsfälle**.

Status-Legende: ✅ umgesetzt · 🔜 geplant · 💬 Strategie

---

## 1. Startseite-Performance ✅ (Code) / 🔜 (Infra)
**Feedback:** „Manchmal lagt es am Anfang, wenn ich auf die Startseite gehe (harmoniq.q-no.ch).
Müsste meine nächste Woche vorgerechnet werden?"

**Diagnose (gemessen):** Warm rendert die Startseite in **~100–150 ms** – die Abfragen sind *nicht*
das Problem. Der Prerender blockierte bisher, bis **alle** Abfragen fertig waren. „Lagt am Anfang"
= **Kaltstart auf Railway** (App-Boot + JIT + erste DB-Verbindung nach Leerlauf), bezahlt vor dem
ersten Byte. „Nächste Woche vorrechnen" würde das *nicht* lösen (die Query ist schnell).

**Umgesetzt (Code):**
- **Stream-Rendering** (`@attribute [StreamRendering]`): Gerüst + Skeletons erscheinen **sofort**,
  Inhalte werden danach nachgestreamt (statt hängender Leerseite). Verifiziert.
- **Globale Abfragen gecacht:** kommende Konzerte + neue Videos wandern in denselben 5-min-Cache
  wie die Kennzahlen → pro Aufruf laufen nur noch die **nutzerspezifischen** Abfragen (Feed/Status/
  Teaser), parallel. Weniger DB-Last, schnellere TTFB unter Last.
- **Flacker/„3 s"-Fix via `PersistentComponentState`:** Das Symptom „‚Für dich' erscheint, verschwindet,
  kommt nach ~3 s zurück" war der **Prerender→Interaktiv-Doppellauf** (`OnInitializedAsync` lief zweimal
  → Felder geleert + alle Abfragen erneut gegen die latente Prod-DB). Jetzt sichert der Prerender die
  geladenen Daten; der interaktive Lauf **übernimmt sie ohne erneute Abfragen** → kein Flackern, keine
  zweite Abfragerunde. Verifiziert (State-Blob im HTML, keine Fehler, Feed sofort da). Wirkt erst **nach
  dem nächsten Deploy** (Push nötig).

**Offen (Infra, Railway – Kunos Entscheid):** der eigentliche Fix gegen den Kaltstart ist
**App warm halten** (Railway: Leerlauf-Spin-down vermeiden bzw. externer Uptime-Ping alle paar
Minuten). Optional zusätzlich: DB-Verbindung + globaler Cache beim Start vorwärmen (HostedService).

**Prod gemessen (harmoniq.q-no.ch, noch ALTER Build):** erster Aufruf **~2,7 s** TTFB (Kaltstart),
danach **~1,1 s** (lokal ~0,1 s). Nach dem Deploy des Stream-Renderings sollte „man sieht sofort
etwas" deutlich besser sein (Gerüst kommt vor den DB-Abfragen). Die ~1,1 s warm deuten auf
**App↔DB-Latenz** (Railway-Netz) + Circuit-Start; nach Deploy neu messen, ggf. DB-Region/Pooling prüfen.

**„Zurück zur Startseite" laggy (~3 s) — Fix per Konto-Cache (2026-07-12):** Ursache: jede Navigation
ZURÜCK zur Startseite lädt die Seite komplett neu und führt **alle nutzerspezifischen Abfragen erneut**
aus (`FeedLadenAsync` + `DigestService` ≈ 12 DB-Round-Trips); der `PersistentComponentState`-Cache
greift nur beim allerersten Aufruf. Fix: die teuren nutzerspezifischen Daten (Feed/Status/Teaser)
werden **pro Konto 30 s server-seitig gecacht** (`home:user:{userId}`); die **Vormerkungen („geplant")
bleiben live** (schlanke Query) → „zurück" ist schnell UND eine soeben gesetzte „möchte hin" ist sofort
sichtbar. Lokal verifiziert (Feed aus Cache, Vormerkung live). **Prod-Wirkung nach Deploy bestätigen.**
Offen falls der ERSTE Aufruf noch zu langsam ist: `DigestService`-Queries reduzieren/bündeln.

**Cache jetzt GLEITEND (2026-07-12):** war fälschlich absolut 30 s (ab Erstellung) → nach 30 s wieder
langsam. Jetzt `SlidingExpiration = 30 s` (+ absolute Obergrenze 5 min) → jeder Startseiten-Besuch
verlängert neu, Browsen bleibt schnell.

**Konzert-Detailseite langsam (3–5 s) — auf PROD GEMESSEN (curl):** Server-TTFB **~1,1 s warm / 2,15 s
kalt**, **kein** Stream-Marker → der Prerender blockierte bis alle Abfragen fertig waren. Die gefühlten
3–5 s = Server-Render **+** Client: KonzertDetail hatte **kein** `PersistentComponentState`, d. h. beim
(Enhanced-Navigation-)Laden laufen die Abfragen **zweimal** (Prerender + interaktive Hydration). Fix
jetzt: **`@attribute [StreamRendering]`** auf KonzertDetail → Gerüst/Spinner **sofort**, Inhalt streamt
nach (lokal verifiziert: Stream-Marker + Spinner im ersten Chunk). **Offen falls weiter zu langsam:**
KonzertDetail-Abfragen parallelisieren/reduzieren (viele sequenzielle Round-Trips) und/oder Doppellauf
via `PersistentComponentState` vermeiden — dafür Prod-Logs der langsamsten Query nutzen.

**Weitere Mess-Befunde (2026-07-12, Prod-URL vom User):**
- **Response-Compression war AUS** (Prod-Antwort ohne `Content-Encoding`, 95 KB HTML unkomprimiert). Jetzt
  `AddResponseCompression` (Brotli/Gzip, `EnableForHttps`) in Program.cs. Lokal verifiziert: `/bands`,
  `/`, `/konzerte` → `br`. **Hinweis:** eine `[StreamRendering]`-Seite, die tatsächlich streamt, liefert
  `identity` (Blazor puffert den Stream nicht) → Konzert-Detailseite bleibt beim Streamen unkomprimiert;
  Transfer ist laut Messung aber NICHT der Flaschenhals (total ≈ TTFB), daher ok.
- **Zeitmessung in KonzertDetail eingebaut** (ILogger, temporär): loggt `total/DbOpen/Konzert/PublicData/
  User+Eindruecke` ms. **Lokal: 1. Aufruf 518 ms (EF-Query-Erstkompilierung!), warm 76–144 ms.** Verdacht:
  auf Prod zahlt jede Sitzung nach Railway-Leerlauf die EF-Kompilierung neu (Kaltstart). **Nächster Schritt:
  Prod deployen, Konzert öffnen, die `KonzertDetail …: total …ms`-Logzeile aus Railway teilen** → dann ist
  klar, ob Kaltstart-Kompilierung, DB-Latenz oder Doppellauf. Mögliche Fixes danach: App warm halten
  (Infra), EF-Query-Warmup beim Start, `PersistentComponentState` gegen den Doppellauf.
- **GELÖST (2026-07-12): Ursache war die DB-Region.** Prod-Log zeigte **~140 ms JE Abfrage** (× ~15 × 2
  Läufe ≈ 4 s). Grund: **Web-Service in Amsterdam, Postgres in den USA** (interne Verbindung, aber
  Cross-Region-Round-Trip ~140 ms). Nach **Postgres → Amsterdam**: `KonzertDetail … total 47ms`
  (Konzert-Query 3 ms statt 140 ms). ✅ An der Wurzel gelöst.
  Danach aufgeräumt: temporäre Zeitmess-Logzeile **entfernt**; `[StreamRendering]` auf KonzertDetail
  **zurückgenommen** (erzwang unkomprimierte Antwort → jetzt wieder `br`-komprimiert). Der **Doppellauf**
  (Prerender + Hydration, by-design für Tempo/SEO) ist bei 47 ms vernachlässigbar → bewusst belassen;
  saubere Vermeidung via `PersistentComponentState` nur auf der Startseite (dort lohnend).

---

## 2. Bands-Liste & Filter ✅
**Feedback:**
- „Band-Liste → in Band rein (um zu folgen) → zurück: **Filter ist weg** = Fehler!"
- „Folgen müsste auf der Liste sein, dafür weniger Kategorie/Zusatz-Infos. Wichtig: **Name, Ort, Folgen**."
- Smartphone: „Eingabe Stadt + Enter → Filter bleibt oben (Live-Filter ok). Besser: nach Enter **zum
  ersten Resultat** springen."

**Plan:**
- **Filter in die URL-Query** (`/bands?suche=…&ort=…&km=…`) → „zurück" stellt den Filter wieder her (Bug-Fix).
- **„Folgen"-Knopf direkt in der Liste** (Toggle je Zeile/Karte; anonym → „Login zum Folgen").
- Spalten reduzieren auf **Name · Ort · Folgen** (Kategorie/Zusatz raus bzw. in „Mehr Filter"/Detail).
- **Enter → zum 1. Resultat** scrollen/fokussieren.

---

## 3. Filter allgemein vereinfachen (öffentliche Seiten) — `/bands` ✅ · `/konzerte` ✅
**Feedback:** „Einfacher: nur **1 Filter** und ein Knopf **‚Mehr Filter'**, wo man Standort/Stärkeklasse
etc. detailliert filtern kann."

**Plan:** Ein Hauptfilter (Suche) sichtbar; sekundäre Filter (Standort/Umkreis/Stärkeklasse/Kategorie/
Zeitraum) hinter **„Mehr Filter"** (aufklappbar). **`/bands` umgesetzt** (Suche + „Mehr Filter"), das
gleiche Muster steht für **`/konzerte`** noch aus.

---

## 4. Einstieg / Bottom-Navigation ✅
**Feedback:** „Knöpfe unten: solange man kein Tagebuch hat, soll **Bands** da stehen."

**Plan:** Bottom-Nav zeigt **„Bands"** statt „Tagebuch", solange das Konto **keinen Tagebuch-Eintrag**
hat (führt neue Nutzer zum Folgen → macht den Feed erst wertvoll). Sobald ein Eintrag existiert →
„Tagebuch".

---

## 5. Konzerte: „Ich möchte hingehen" (statt „war da") ✅
**Feedback:** „Bei zukünftigen Konzerten kann man nicht ‚ich war da' sagen, sondern **‚ich möchte an
dieses Konzert gehen'**. Solange das Konzert nicht durch ist, müsste das in meiner Übersicht sichtbar sein."

**Umgesetzt — bewusst OHNE Status-Feld** (nach Diskussion mit Kuno: einfacher):
- Ob „möchte hin" oder „war dabei", wird **rein aus dem Konzertdatum** abgeleitet (kein neues Feld,
  keine Status-Migration). Strikt künftig (> heute) → **„Da möchte ich hin"**; heute/vergangen → **„Ich
  war im Publikum"**. Ein `KonzertBesuch` ist einfach die Markierung; abwählen via „Doch nicht".
- **Notizen** (Konzert + je Stück) schon **vorab** möglich; **Sterne** erst **ab dem Konzerttag**
  (Datum ≤ heute, unabhängig von der Uhrzeit). Feed „war beim Konzert" nur für heutige/vergangene.
- **Übersicht:** künftige Vormerkungen auf **Startseite** (Block „Du möchtest hingehen") und im
  **Tagebuch** (Abschnitt „Ich möchte hingehen", aufsteigend), getrennt von den Besuchen.

Hinweis: der zwischenzeitliche Status-Ansatz wurde wieder entfernt (Migration zurückgenommen, war nur lokal).

---

## 6. 💬 Erklär-Video + Anwendungsfälle (ans Ende der Umsetzung)
**Feedback:** „Man kann so viel – zu wenig klar, was man tun soll / wozu das Ganze."

**Entscheid:** Zuerst die Seite **vereinfachen** (Punkte 2–5). **Danach** ein kurzes **Erklär-Video**
produzieren, das zeigt, **was man von harmoniq.q-no.ch erwarten kann** – anhand konkreter, „cooler"
**Anwendungsfälle**, z. B.:
- „Ich war an einem Konzert – festhalten & Stücke bewerten (mein persönliches Konzert-Tagebuch)."
- „Meinen Lieblingsvereinen folgen → automatisch erfahren, wann/wo sie als Nächstes spielen."
- „Ein Stück gehört, das mir gefiel → wer hat es komponiert, wer spielt es noch, gibt es Videos?"
- „Als Verein: eigenen Auftritt pflegen und Publikum in der Nähe erreichen."

Platzierung: auf der Landingpage / im Onboarding als optionaler Einstieg (Akquise + Orientierung),
nicht als Ersatz für eine selbsterklärende Oberfläche.

---

## 7. Anmelden / Registrieren – eine „clevere" Maske ✅
**Feedback:** „Nicht klar, ob anmelden oder ‚Kostenlos registrieren'. Besser: **ein** Knopf, dort zuerst
**Google/Microsoft**, dann E-Mail/Passwort. Ist die E-Mail neu, direkt die Registrierung ergänzen
(2× Passwort o. Ä.). Also **eine** Login/Anmelde-Maske, evtl. clever. Wie machen es andere?"

**Wie andere es machen (identifier-first):** Notion/Slack/Google zeigen erst **E-Mail eingeben**, danach
entscheidet das System, ob bekannt (→ Passwort) oder neu (→ Registrierung). Social-Login-Knöpfe stehen
**oben** („Weiter mit Google/Microsoft"), Trenner „oder", darunter das E-Mail-Feld.

**Umgesetzt (identifier-first, `/Account/Login`):**
1. Schritt 1 (für alle): oben **Google/Microsoft**, Trenner „oder", **E-Mail** + „Weiter", darunter Passkey.
2. „Weiter" prüft server-seitig, ob die E-Mail existiert → **bekannt** = Passwort-Feld („Willkommen zurück",
   Anmelden); **neu** = Passwort **+ Wiederholung** inline („Konto erstellen", Kostenlos registrieren).
3. „ändern" führt zurück zu Schritt 1 (E-Mail bleibt erhalten). Ein Einstieg, kein „Login vs. Registrieren"-Rätsel.
- Bewährte Flows unverändert dahinter (Bestätigungsmail, 2FA, Lockout, Passwort-vergessen, Passkey).
  `/Account/Register` **leitet** auf die Maske um (alte Links); App-CTAs („Jetzt mitmachen", Einladung)
  zeigen direkt auf `/Account/Login`.
- **Verifiziert:** Schritt 1 (Social/E-Mail/Passkey), bekannte E-Mail → Anmelden, neue E-Mail → Registrieren,
  „ändern"-Reset, `/Account/Register`-Redirect (ReturnUrl erhalten). Konsolen-„Fehler" nur WebAuthn/Passkey
  (kein Authenticator im Preview) – keine Regression.
- **Bewusst so (identifier-first):** die Existenz einer E-Mail wird beim „Weiter" erkennbar (wie bei
  Google/Notion/Slack; die Registrierung verriet das ohnehin). Von Kuno abgesegnet.

## 8. Admin: Benutzer löschen ✅
**Feedback:** „Wieso kann ich als Admin keine Benutzer löschen?" → Auf `/admin/benutzer` gab es nur
„Verknüpfung"; ein Löschen war nie gebaut.
**FK-Verhalten (aus der DB abgefragt):** CASCADE = Identity-Tabellen, KonzertBesuche, StueckEindruecke,
PersonAnsprueche, Benachrichtigungen/Praeferenzen, PushSubscriptions · SET NULL = Personen.BenutzerId,
Videos/Richtigstellungen/BandbeitrittAntraege/VideoMitwirkungen (…VonId) · **NO ACTION = Bewertungen**
(blockiert!) · **keine DB-FK = BandAdministratoren, BandAdminEinladung.EingeladenVon** (Waisen-Gefahr).
**Umgesetzt** (`BenutzerAdmin.razor`): Lösch-Knopf pro Zeile (nicht fürs eigene Konto) + Bestätigung.
Vor `UserManager.DeleteAsync` werden **Bewertungen** und **BandAdministratoren** des Kontos per
`ExecuteDeleteAsync` entfernt und `BandAdminEinladung.EingeladenVon` genullt; den Rest erledigt die DB
(Cascade/SetNull). Person bleibt erhalten (nur entkoppelt).
**DB-verifiziert:** ohne Bereinigung → Block durch `FK_Bewertungen_AspNetUsers_BenutzerId`; mit Fix →
User weg, KonzertBesuch weg (Cascade), Person bleibt mit `BenutzerId = NULL` (SET NULL).

## 9. Person-Löschen scheitert (Prod) ✅
**Feedback / Prod-Fehler:** `update or delete on table "Personen" violates RESTRICT setting of foreign key
constraint "FK_Freundschaften_Personen_AnfragerPersonId"`.
**Ursache:** `Freundschaft` hat zwei FKs auf `Person` mit **`OnDelete(Restrict)`** (bewusst, um doppelte
Kaskadenpfade zu vermeiden — laut Code-Kommentar sollte „die Person vorher aus Freundschaften entfernt"
werden). Genau dieses Vorab-Löschen fehlte im **direkten Admin-Löschen** (`PersonenAdmin.Loeschen`). Der
**Merge** (`PersonMergeService`) macht es korrekt; nur das Löschen nicht. Alle anderen Person-FKs sind
Cascade/SetNull.
**Fix:** In `PersonenAdmin.Loeschen` vor `Personen.Remove` die Freundschaften der Person per
`ExecuteDeleteAsync` entfernen (beide FK-Richtungen). DB-verifiziert: alter Weg → exakt dieser
FK-Fehler; neuer Weg (erst Freundschaften, dann Person) → Erfolg.

## 10. Band-Admin: Videos hinzufügen & YouTube-Crawler ✅ (vor dem Erklär-Video)
**Auftrag Kuno:** „Band-Admin: einzelnes Video mit Link direkt hinzufügen (Stück + Komponist:in
herausfinden, evtl. mit LLM). Und ein Crawler für die YouTube-Links der bestehenden Bands – nur einmal
suchen, dann entscheiden ja/nein, beim nächsten Lauf nur Neueres."

**A) Einzel-Link** (`/admin/bands/{id}/video`, `BandVideoHinzufuegen`) ✅ — Link einfügen → Titel via
oEmbed → LLM (`VideoTitelAnalysierenAsync`, Mistral) schlägt Stück + Komponist:in aus dem Titel vor →
prüfen/korrigieren → speichern (find-or-create Stück/Person via `VideoErfassung`, Video **genehmigt**).
End-to-end verifiziert (inkl. Anlegen Stück + Komponist).

**B) YouTube-Suche pro Band** (`/admin/bands/{id}/videos-suchen`, `BandVideosSuchen` +
`BandVideoCrawlService`) ✅ — Entscheide:
- **On-demand pro Band auf Knopfdruck** (nicht Sammellauf): YouTube-Suche kostet 100 Einheiten/Aufruf
  (~100 Band-Suchen/Tag); Bands haben zudem keine Kanal-Links hinterlegt → **Suche über den Bandnamen**.
- **Review mit editierbaren Feldern + Video-Preview** (Kunos Wunsch): je Treffer Thumbnail→Embed,
  editierbares Stück/Komponist:in (LLM-Vorschlag), Übernehmen/Ablehnen.
- **Inkrementell:** neue Entität `BandVideoFund` merkt jeden Treffer (Offen/Übernommen/Verworfen),
  Unique-Index `(BandId, ExternId)`; ein erneuter Lauf zeigt nur Neues, Entschiedene tauchen nicht wieder auf.
- Specs nachgeführt (`Spezifikation-Crawler.md` §4.5, `Spezifikation.md`).
- **Verifiziert auf Dev** (Band Neuenkirch): Suche liefert Kandidaten, LLM erkennt Stück/Komponist
  (z. B. „nabucco" → Nabucco / Verdi), Übernehmen erzeugt genehmigtes Video + Stück/Person, Ablehnen
  setzt Verworfen, 2. Suche legt Bekanntes **nicht** doppelt an (keine Dubletten). Testdaten belassen (Kunos Wunsch).

## Empfohlene Reihenfolge
1. **Performance** ✅ Code (Stream-Rendering + Cache); Railway-Keep-warm bleibt Kunos Infra-Schritt.
2. **Bands-Liste & Filter** (Punkt 2) – Filter-Bug + Folgen-Knopf + Enter→Resultat (billig, sofort spürbar).
3. **Filter vereinfachen** (Punkt 3) – 1 Filter + „Mehr Filter", einheitlich.
4. **Bottom-Nav** (Punkt 4) – klein.
5. **Konzert „möchte hingehen"** (Punkt 5) – Feature mit Datenmodell-Änderung.
6. **Erklär-Video + Anwendungsfälle** (Punkt 6) – am Schluss.

---
---

# Feedback aus User-Tests (Runde 2 – weiterer User)

Feedback eines weiteren Users. **Noch nicht abgeschlossen – es folgt mehr.** Erst sammeln & einschätzen,
dann mit Kuno priorisieren. Gleiche Status-Legende (✅ umgesetzt · 🔜 geplant · 💬 Diskussion).

---

## 11. 💬 Zweck der Seite auch als lesbarer Fliesstext (nicht nur im Video)
**Feedback:** „Der Text des Zwecks der Webseite gemäss dem Video auf der Startseite sollte auch als
Fliesstext lesbar zur Verfügung gestellt werden (siehe auch Rechtsgutachten in Beilage)."

**Kunos Einschätzung:** Noch abzuklären. Eigentlich wollen die Leute nicht ewig lesen – es sollte viel
selbsterklärend sein. Aber auf einer **separaten Seite** könnte man das „Wozu" dieser Seite ergänzen.

**Zusatz-Einschätzung (Claude):**
- Zwei mögliche Treiber im Rechtsgutachten: (a) **Barrierefreiheit** – Videoinhalt braucht eine
  gleichwertige **Text-Alternative** (WCAG); (b) **Transparenz/Impressum** – klarer, nachlesbarer
  Zweck der Plattform und der Datenverarbeitung.
- **Kein Konflikt mit „selbsterklärend":** Die Oberfläche bleibt schlank; der Fliesstext lebt auf einer
  **eigenen Seite** (z. B. `/ueber` „Über HarmoniQ / Wozu diese Seite"), verlinkt aus Footer und dezent
  beim Video („Lieber lesen? →").
- **Aufwand gering:** eine statische Seite mit demselben Inhalt wie das Video-Skript als Prosa.
- **Offen für Kuno:** Rechtsgutachten sichten (was genau verlangt es?), dann Text finalisieren.
  → Tendenz: **umsetzen als separate `/ueber`-Seite**, sobald der geforderte Inhalt klar ist.

---

## 12. 🔜 Konzert: Link zur Vereins-Ankündigung (Billette)
**Feedback:** „Im Verzeichnis der Konzerte wäre ein Link zur Ankündigung des Vereins hilfreich, z. B.
damit man die Billette findet."

**Kunos Einschätzung:** Gute Idee – auf dem Einzelkonzert gut darstellbar, in der Liste müsste es knapp
Platz finden. **Sollten wir umsetzen.**

**Zusatz-Einschätzung / Plan (Claude), noch nicht umgesetzt:**
- **Datenmodell:** optionales Feld `AnkuendigungUrl` (o. Ä.) an `Konzert` (nullable). → Beim Umsetzen
  **Spec nachführen** (`Spezifikation.md`); falls der Crawler die URL künftig mitliefert, auch
  `Spezifikation-Crawler.md`.
- **Einzelkonzert:** klarer Knopf, z. B. „Zur Ankündigung / Billette" (öffnet extern, `target=_blank`,
  `rel=noopener`).
- **Liste:** kompaktes Icon-Link (Ticket/Extern-Symbol) je Zeile, nur wenn eine URL hinterlegt ist –
  spart Platz.
- **Pflege:** manuell im Konzert-Admin/-Erfassen; optional später automatisch aus EventFrog/Vereinsseite.
- **Sicherheit:** URL validieren (nur http/https), als externen Link kennzeichnen.

---

## 13. 💬 Instrumenten-Namen inkonsistent (Es-Klarinette vs. Klarinette)
**Feedback:** „Beim Benutzerprofil kann man «Es-Klarinette» wählen, etwas später sieht man «Klarinette».
Das ist systematisch inkonsistent. Ich spiele B-Klarinette und empfehle, die beiden zu ersetzen mit
«Klarinette in Es» und «Klarinette in B», oder mit «Klarinette» (ohne Details), oder «B-Klarinette»."

**Kunos Einschätzung:** Unsicher, eher langfristige Liste. Kommt auch **vom Crawler** her – je nach
gescannter Webseite kommt A oder B heraus. **Idee:** Instrumente um **„Alternative Namen"** ergänzen
(wie bei den Bands) → man wählt „Klarinette", angezeigt wird die kanonische Form (z. B. „B-Klarinette").

**Zusatz-Einschätzung (Claude), noch nicht umgesetzt:**
- Genau das Alias-Muster der Bands: ein **kanonischer Name** + Liste **alternativer Schreibweisen**.
  Der Crawler mappt Fundstücke („Es-Klarinette", „Klarinette in Es", „Eb Clarinet") auf den kanonischen
  Eintrag statt neue Dubletten anzulegen → löst Inkonsistenz **und** Crawler-Varianten in einem.
- **Datenmodell:** `Instrument` bekommt Aliasse (analog Band-Aliasse). → Beim Umsetzen **Spec nachführen**
  (`Spezifikation.md`) und Crawler-Normalisierung in `Spezifikation-Crawler.md`.
- **Migration/Bereinigung:** bestehende Dubletten (Es-Klarinette ↔ Klarinette) einmalig zusammenführen
  (Merge, wie bei Personen/Bands).
- Passt zu Kunos „langfristige Liste" – kein Sofort-Fix, aber der Alias-Ansatz ist der saubere Weg.

---

## 14. 🔜 (Bug) + 💬 „Konzerte in deiner Nähe": mehrere Orte & Umkreis-Wahl
**Feedback:** „Unter «Konzerte in deiner Nähe» wollte ich Bachenbülach **und** Menznau eingeben, das
System erlaubte nur **1 Ort**. Empfehlung: **5–10 Orte** zulassen. Zudem sollte man wie auf
immoscout24.ch den **Umkreis** wählen können (0 / 5 / 25 km; 6122 +/- 25 km = auch Sempach)."

**Kunos Einschätzung:** Grundsatz **„Mobile first"** (Smartphone). Man erlaubt der Seite den **eigenen
Standort**; bei ausgeklappten Filtern wählt man den **km-Umkreis**. Mehrere Orte sind damit eher
zweitrangig. **Aber ein Bug:** Gibt man eine **PLZ** ein, wird der **eigene Standort nicht ignoriert** –
das muss korrigiert werden.

**Aufgeteilt:**
- **14a ✅ BUG – PLZ überschreibt Standort nicht:** Erwartung: sobald man eine PLZ (oder „Mein Standort")
  einträgt, gilt dieser Punkt als Bezugspunkt, nicht mehr die GPS-Position. Ursache: der stille
  Standort-Autoload (`harmoniqGeo.autoStandort`) gewann beim (Neu-)Laden gegenüber der manuell gesetzten
  PLZ. **Umgesetzt** in `KonzerteListe` + `BandsListe`: neues Flag `_refManuell` (true bei PLZ/„Mein
  Standort"); der Bezugspunkt wird explizit im Filter-State (sessionStorage) gemerkt und beim Laden
  wiederhergestellt; der GPS-Autoload läuft **nur noch, wenn der User keinen eigenen Punkt gesetzt hat**
  (`if (!_refManuell && _refLat is null)`). „Zurücksetzen" macht wieder GPS-Default. Build grün, keine
  Laufzeitfehler. **Auf echtem Browser zu verifizieren** (PLZ setzen → neu laden → PLZ bleibt Zentrum,
  nicht GPS), da Standort/Interaktivität in der Vorschau nicht zuverlässig testbar ist.
- **14b 💬 Umkreis-Wahl:** ist bereits vorhanden (Umkreis 10/25/50/100 km hinter „Mehr Filter"). Evtl.
  feinere Stufen (0/5 km) ergänzen. Sichtbarkeit wurde kürzlich schon verbessert (aktiver Umkreis
  klappt „Mehr Filter" jetzt auf; Standort setzt nicht mehr still einen 50-km-Umkreis).
- **14c 💬 Mehrere Bezugsorte (5–10):** widerspricht dem schlanken Mobile-first-Ansatz (1 Standort +
  Umkreis). Kunos Tendenz: **nicht** umsetzen; ein Umkreis um einen Punkt deckt „Bachenbülach + Menznau"
  in der Praxis nur, wenn sie nah beieinander liegen – tun sie nicht (ZH-Unterland vs. LU). Falls doch
  gewünscht, wäre die einfachste Form: mehrere PLZ als Bezugspunkte (ODER-Verknüpfung der Umkreise).
  Vorerst **zurückgestellt**.
