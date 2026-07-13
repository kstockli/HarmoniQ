# HarmoniQ – Spezifikation Crawler / Import-Roboter

> Zweite, eigenständige Spezifikation (ergänzt `Spezifikation.md`). Beschreibt einen halb­automatischen
> **Crawler**, der Blasmusik-Vereins-Webseiten nach **Dirigent:innen, Konzerten (mit Stücken/Komponist:innen)**
> abklappert, die Funde strukturiert aufbereitet und einem **Admin zur Übernahme** vorlegt. Er ersetzt den
> bestehenden `/admin/import`-Assistenten nicht, sondern erweitert ihn (gleiches Ziel: saubere, dublettenfreie
> Daten; gleiche Find-or-create-Bausteine).

## 0. Umsetzungsstand & Entscheide (Stand 2026-06-22)

**Umgesetzt (lauffähig, lokal getestet):**
- Datenmodell `CrawlQuelle/CrawlLauf/CrawlFund/CrawlSeite` (+ Migrationen `CrawlerGrundgeruest`, `CrawlQuelleHinweis`).
- Fetch-Stufe (`CrawlFetchService`): HTML **und** PDF (PdfPig), robots.txt, Rate-Limit pro Domain, Größenlimit.
- Orchestrator: In-Memory-Queue + `IHostedService`, sequenziell, verwaiste Läufe → `Abgebrochen` beim Start;
  `CrawlRunner` (BandDomain-BFS domain-begrenzt mit Tiefe/Seiten-Limit; Dokument/Event Einzelabruf), Seiten-Filter,
  **Fund-Dedup innerhalb eines Laufs** (gleiche Identität → nur der vollständigste Datensatz).
- **LLM-Extraktion live** (`IExtraktion` + `MistralExtraktion`, `mistral-large-latest`, JSON-Modus, tolerantes Parsen).
- Admin-GUI `/admin/crawler` (Quellen als Karten mit editierbarem Zusatz-Hinweis, Lauf starten, Läufe-Log) und
  `/admin/crawler/funde` (lesbare Aufbereitung je Fund-Typ, JSON einblendbar/editierbar, Übernehmen/Verwerfen,
  Massen-Aktionen). Crawler-Link im Admin-Menü über dem Import-Assistenten.
- Übernahme-Pfade (Find-or-create): Konzert → `KonzertErfassungService`; Leitung → `BandMitgliedschaft`;
  Stück → `Stueck` (Titel-/Alias-Abgleich) + `StueckBeitrag`; Komponist:in → `Person`;
  **Verein → `Band`** (Name/Alias-Abgleich, leere Felder füllen, Aliase + Social-Links ergänzen).
  Stücke/Bands lassen sich im Admin **zusammenführen** (Merge), wenn derselbe Eintrag unter
  verschiedenen Namen entstanden ist – alle Referenzen werden umgehängt, der Quell-Name bleibt als Alias.

**Entscheide:**
- **LLM-Anbieter: Mistral „La Plateforme", Modell `mistral-large-latest`** (Interface bleibt anbieter-neutral).
  Kosten bei der Last vernachlässigbar (~$0.01–0.02 pro PDF) → Qualität entscheidet.
- **Heuristik wurde als *Fund-Produzent* verworfen** (zu fragil); sie dient nur noch als billiger **Seiten-Filter**
  (Keyword-Triage vor dem LLM). Die LLM-Extraktion (ursprünglich C3) wurde **vorgezogen**.
- **Arrangeur:in** wird getrennt von Komponist:in extrahiert (eigener `StueckBeitrag` mit `StueckRolle.Arrangeur`).
  Zusätzlich zerlegt der `KomponistParser` beim Übernehmen jedes Komponist-/Arrangeur-Feld deterministisch:
  mehrere Namen werden getrennt (Komma, „&", „und", „/", „;", „+") und Arrangeur-Marker erkannt
  („Arr."/„arr."/„Arrangeur"/„Bearb."/„arranged by"/„orch." …) → diese Namen erhalten die Rolle Arrangeur
  (Marker wirkt „klebrig" für nachfolgende Namen). Beispiel: „Arr. Filip Ceunen, Michael Story" → zwei
  Arrangeur-Beiträge. Greift in `KonzertErfassungService` (Programm) und `CrawlUebernahmeService` (Stück-Fund).
- **Diagnose:** LLM-Calls/Antworten optional protokollierbar (`Crawler:Llm:LogCalls`).

**C2 (teilweise umgesetzt):** JS-Rendering via Playwright/Chromium (`ISeitenRenderer`/`PlaywrightRenderer`,
config-gesteuert `Crawler:RenderingAktiv`, Default aus, HTTP-Fallback) – verifiziert an `emf26.ch/vereine`
(7,4 MB gerendert, 484 Vereins-Domains). **Event-Quellen rendern automatisch** (kein Extra-Flag nötig);
der Renderer wartet, bis die **Link-Anzahl stabil** ist (lazy-geladene SPA-Inhalte) – nicht auf NetworkIdle
(flaky) und ohne Scrollen (würde virtualisierte Listen ausdünnen). **Vereins-Link-Ernte:** Event-Quellen ernten
fremde Domains und legen je einen **Webseiten-Fund** (`CrawlFundTyp.Webseite`) mit Mini-Vorschau (Seitentitel/
Beschreibung, **ohne LLM**, parallel geladen) an. **Kategorie/Stärkeklasse je Verein** wird aus den
**Gruppen-Überschriften** der Verzeichnis-Seite gelesen (Dokumentreihenfolge: die letzte Überschrift gilt –
z. B. „Konzertmusik, Höchstklasse, Harmonie"; verifiziert: 472/472 Vereine auf emf26 zugeordnet). Liegt ein
**Hinweis** vor, filtert das LLM (`IExtraktion.FiltereVereineAsync`, nummern-basiert, gechunkt) die Liste vor
der Fund-Erzeugung (z. B. „Höchstklasse, Harmonie" → 11 statt 472). Beim **Übernehmen** entsteht eine

**EMF-Vereinsverzeichnis = JSON-API statt Rendering (`EmfVereinImporter`):** Die emf26.ch/vereine-Seite ist
eine schwere Wix-SPA, deren Daten aus einer **sauberen öffentlichen JSON-API** stammen
(`https://emf26-api.ch/public/verein?locale=de`: Name, Kategorie/Klasse/Besetzung, Website, Direktion,
Socials). Erkennt der Runner diese Quelle (`EmfVereinImporter.IstZustaendig`), holt er die **API direkt per
HttpClient** (kein Chromium → kein OOM, läuft auch im 512-MB-Container) und legt je Verein **mit** Website einen
Webseiten-Fund an. Der **Hinweis-Filter** ist hier **deterministisch** (auf der Kategorie-Zeichenkette, ohne
LLM): „Höchstklasse, Harmonie" → genau 11 Vereine (verifiziert; 532 total, 468 mit Website). Schlägt die API
fehl, **Fallback** auf den normalen (gerenderten) Seiten-Crawl.
**inaktive BandDomain-Quelle (Vorschlag) mit gesetzter Ziel-Band** (find-or-create über Webseite/Name; Name
aus Seitentitel bzw. Domain; **Kategorie/Stärkeklasse** aus dem Fund übernommen) – so kann der Folge-Crawl
seine Konzerte direkt der richtigen Band zuordnen, und der Admin entscheidet je Verein in der Funde-Review.
Noch offen in C2:
**Join Rangliste-PDF ↔ Spielplan** über Vereinsnamen und automatische **Rück-Zuordnung** der Folge-Crawl-Stücke
ans (Lokal,Datum)-Konzert.

**C3+ (umgesetzt):** Große Seiten werden in überlappende **Chunks** zerlegt und je Abschnitt extrahiert
(statt bei 24 000 Zeichen abzuschneiden); Konzerte gleicher Identität werden über die Chunks hinweg
zusammengeführt. **Wikipedia-Anreicherung** für Komponist:innen umgesetzt: `WikipediaService` (REST-Summary +
Wikidata-Geburtsjahr, kein Key) erzeugt **Komponist-Funde** (auch ohne Lauf, `CrawlFund.LaufId` nullable) →
Review/Übernahme füllt leere Person-Felder (Bio/Bild/Geburtsjahr/Wikipedia-Link). Auslöser: Button in `/admin/crawler`.

**Anforderungen-Bitset (`CrawlQuelle.Anforderungen`, umgesetzt):** je Quelle setzbare strukturierte
Anforderungen. `KonzertBrauchtStueck` → Konzerte ohne Programmzeile werden gar nicht als Fund vorgeschlagen.
`VorstandCrawlen` / `MukoCrawlen` → das LLM erfasst zusätzlich Vorstands-/Muko-Mitglieder (Feld `funktionaere`:
Name, Funktion, E-Mail, Instrument). Übernahme = `BandMitgliedschaft` mit der **Funktion** (Identität =
Person + Band + Funktion, daher getrennt von Dirigent-/Spiel-Mitgliedschaften), Person als **Musikant**,
**Sichtbarkeit Öffentlich**, E-Mail als `PersonLink`, Instrument optional. **Abgänge** werden **nie automatisch**
beendet: am Laufende werden aktive Gremiums-Mitgliedschaften, die der Crawl nicht (mehr) fand, als
**Hinweis-Fund** gemeldet (nur wenn überhaupt Mitglieder gefunden wurden) – der Admin setzt ggf. `BisJahr`.

**Noch offen:** C2-Rest (Join/Rück-Zuordnung, `Band.Webseite`-Autofill), C4 (Ort→Kanton-Regionfilter,
geplante Läufe), C6 (Eventfrog-Einleser, §4.4 — Konzept spezifiziert, nicht umgesetzt),
`Crawler:Llm:TagesLimit` durchsetzen, Rendering in Prod aktivieren.

## 1. Ziel & Abgrenzung

**Ziel:** Den manuellen Erfassungsaufwand senken, indem öffentlich verfügbare Strukturdaten von
Vereins-Webseiten (Konzertprogramme, Leitung) automatisch vorgeschlagen werden. **Der Mensch entscheidet** –
nichts wird automatisch publiziert.

**Im Scope (Start):**
- **Konzerte** (Datum, optionale **Uhrzeit**, Name, Ort) inkl. **Programm** (Stück + Komponist:in) und **Band**.
- **Dirigent:innen / Leitung** einer Band.
- **Band-Stammdaten**, soweit auffindbar: **Kategorie** (Harmonie/Brassband/Fanfare/Unterhaltung) und
  **Stärkeklasse** (Höchstklasse/Elite/1.–4. Klasse/Ober-/Mittel-/Unterstufe). Auf Wettbewerbs-Ranglisten
  stehen Kategorie und Klasse meist als Spalte → direkt mitnehmen und der Band zuordnen.

**Bewusst (vorerst) NICHT im Scope:**
- **Mitglieder-Namen** (personenbezogene Massendaten → Datenschutz, siehe §3). Später optional, nur mit
  Default-Sichtbarkeit `NurInitialen`, Quellenangabe und Löschpfad.
- Offenes Breitband-Crawling der Suchmaschinen. Der Crawler arbeitet **pro Band, auf deren Domain begrenzt**.

## 2. Grundprinzip (Entscheide)

- **Strategie:** Pro Band domain-begrenzt. Admin gibt **Band + Start-URL** vor; der Crawler bleibt auf
  **dieser Domain** und folgt nur internen Links. Links auf andere Vereine werden **nur als Vorschlag**
  gemeldet (kein Auto-Expandieren).
- **Extraktion:** Die **LLM-Extraktion ist der Produzent** der Funde (Konzertprogramme, Leitung, …). Die
  ursprünglich geplante Heuristik als Fund-Produzent war zu fragil und wurde verworfen; Heuristik dient nur
  noch als **billiger Seiten-Filter** (Keyword-Triage), damit nur relevante Seiten ans (kostenpflichtige) LLM gehen.
- **LLM-Anbieter:** **anbieter-neutral** über die Abstraktion `IExtraktion`. **Entschieden:** Mistral
  „La Plateforme", Modell `mistral-large-latest` (per Konfiguration austauschbar). **Wichtig:** Es braucht
  eine **API** (kein Consumer-Chat wie `chat.mistral.ai`), also einen API-Key, pro-Token abgerechnet.
- **Mensch im Loop:** Jeder Fund landet als **Kandidat** in einer Review-Queue; Übernahme erfolgt nur durch
  Admin-Klick und nutzt die bestehenden Find-or-create-Services (keine Dubletten).
- **Quelltypen (NEU):** nicht nur Vereins-HTML, sondern auch **Dokument/PDF** (Ranglisten, Spielpläne) und
  **Event-Seiten** (Festival-Spielpläne mit vielen Vereinen). Siehe §4.1.
- **Fähigkeiten (NEU):** Fetch kann **PDF-Text extrahieren** und **JavaScript-Seiten rendern** (Headless-Browser,
  z. B. Playwright) – viele Wettbewerbsseiten (Wix, SPAs) liefern Daten erst nach JS-Ausführung oder als PDF.

## 3. Recht & Datenschutz (verbindlich)

- **robots.txt respektieren**; höfliches Crawling: niedrige Rate (z. B. 1 Request/2–5 s pro Domain),
  klar erkennbarer **User-Agent mit Kontaktangabe**, Caching (keine Seite doppelt je Lauf).
- **Personenbezug:** Dirigent:innen sind als Funktionsträger:innen i. d. R. unkritisch; **Mitgliederlisten
  sind personenbezogen** (DSG/DSGVO) und vorerst ausgeklammert.
- **Provenienz:** Jeder Fund speichert seine **Quell-URL** und den Abrufzeitpunkt → Nachvollziehbarkeit,
  Richtigstellung und Löschung bleiben möglich.
- **Sichtbarkeit:** Übernommene Personen erhalten konsistent die Default-Sichtbarkeit des Hauptmodells
  (Dirigent:in → Öffentlich; alles Übrige eher `NurInitialen`).

## 4. Architektur / Pipeline

```
Seed (CrawlQuelle: Band + Start-URL + Limits)
  → 1. Fetch        HttpClient, robots.txt, Rate-Limit, nur erlaubte Domain, Tiefen-/Seitenlimit
  → 2. Seiten-Filter relevante Seiten erkennen (URL/Text: „konzert*, programm, besetzung, vorstand,
                     leitung, dirigent, agenda, termine") – Irrelevantes (Footer/Nav) verwerfen
  → 3. Extraktion    LLM (Mistral, Structured Output → JSON); Seiten-Filter triagiert davor
  → 4. Normalisieren Datum/Titel/Namen säubern; Dedup innerhalb des Laufs (vollständigster Datensatz gewinnt)
  → 5. CrawlFund     Kandidat mit Status „Offen", Quell-URL, strukturierten Daten, Dublett-Hinweis
  → 6. Review        Admin prüft (lesbare Aufbereitung), korrigiert, übernimmt/verwirft
  → 7. Import        Übernahme via KonzertErfassungService / BandMitgliedschaft / Stueck / Person (Find-or-create)
```

**Wiederverwendung aus dem Hauptprojekt:** `HtmlAgilityPack`, `WebseitenScraper`, `StueckParser`,
`KonzertErfassungService` (Konzert + Programm + Bands, Find-or-create), `MitwirkungService`,
Review-Queue-/Admin-Muster, `PersonenSicht`-Defaults.

**Betrieb:** **On-demand pro Quelle** (Admin startet einen Lauf). Läuft als Hintergrund-Task im Web-Prozess
(`IHostedService`/Task-Queue) – kein separater Dienst nötig. Geplante/automatische Läufe optional später.

### 4.1 Quelltypen & Fähigkeiten

Eine `CrawlQuelle` hat einen **Typ**, der das Vorgehen bestimmt:

| Typ | Beispiel | Vorgehen |
|---|---|---|
| **BandDomain** | Vereins-Webseite | Auf Domain crawlen, Heuristik/LLM, Leitung & Konzerte |
| **Dokument** | Rangliste-/Spielplan-**PDF** | PDF-Text extrahieren → LLM strukturiert zu Zeilen |
| **Event** | Festival-Spielplan (HTML/JS) | Seite **rendern** (JS) → Programm-Tabelle extrahieren |
| **Wettbewerb** | SBBW (swissbrass.ch) | Spezial-Handler: Jahres-PDF (Rangliste je Kategorie) **+** Video-Seiten zusammenführen → je Jahr/Kategorie ein **Konzert mit Rangliste & Videos** (siehe §4.2) |
| **Veranstalter** | KKL Luzern (vivenu); Eventfrog.ch (Public API) | Spezial-Handler je Anbieter: Eventliste laden (Rendering bei KKL, direkte REST-API bei Eventfrog), Stilfilter (serverseitig per Genre-Parameter bei KKL, Keyword+LLM bei Eventfrog) + Band-Erkennung → Konzert-Funde (siehe §4.3, §4.4) |

**Fähigkeiten der Fetch-Stufe:**
- **PDF:** Dokument laden (Admin gibt Link **oder** Upload; Links können Ablauf-Token haben → frischer Link
  je Lauf) → Text extrahieren → an die Extraktion geben.
- **JS-Rendering:** Headless-Browser (Playwright) rendert die Seite, bevor extrahiert wird. Nötig für
  Wix-/SPA-Seiten (z. B. EMF-Spielplan, WMC).

**Band-Felder aus Ranglisten:** Wettbewerbs-/Ranglistenzeilen liefern neben Verein/Rang auch **Kategorie**
(Harmonie/Brassband/Fanfare/Unterhaltung) und **Stärkeklasse** – diese werden extrahiert und der Band als
`Band.Kategorie` / `Band.Staerkeklasse` vorgeschlagen (bei der Übernahme bestätigt der Admin). Abweichende
Vereinsschreibweisen werden als `BandAlias` geführt (Find-or-create matcht über Name **und** Aliase).

**Event-Regel „(Lokal, Datum) → ein Konzert" (entscheidend):** Spielplan-Zeilen werden nach **Ort/Lokal + Datum**
gruppiert; daraus entsteht **je Gruppe ein `Konzert`**; je teilnehmenden Verein ein `KonzertBand`; je gespieltem
Stück ein `KonzertStueck` (Stück + Komponist:in). Das bildet das HarmoniQ-Konzertmodell 1:1 ab. Eine
**Uhrzeit** wird – falls im Quelltext genannt – zusätzlich als optionale Startzeit übernommen; sie ist
**nicht** Teil des Dedup-Schlüssels (Identität bleibt Lokal + Datum). Bei WMC (ganztägige Session je
Ort/Datum) gilt die früheste Auftrittszeit als Konzertbeginn; KKL liefert die Startzeit aus dem Event-Zeitstempel.

**Join mehrerer Quellen:** Rangliste-PDF (Verein, Rang, Kategorie – **ohne** Stücke) und Spielplan
(Stücke je Verein) werden über den **Vereinsnamen** zusammengeführt. Der Admin kann zwei Quellen einem
Event zuordnen.

**Zweiter Durchgang (kaskadierend):** Häufig stehen die gespielten Stücke **nicht** auf der Event-Seite,
sondern erst auf der **eigenen Vereins-Webseite**. Ablauf:
1. **Vereins-Link-Ernte:** Eine Teilnehmer-/Vereinsseite des Events (z. B. `emf26.ch/vereine`) wird gerendert
   und ihre **ausgehenden Links** zu Vereins-Domains werden geerntet → als **Webseiten-Vorschlag** je Verein
   (füllt `Band.Webseite`, baut so ein wiederverwendbares **Vereins-Verzeichnis** auf). Admin bestätigt.
2. **Folge-Aufträge:** Für Vereine mit (jetzt) bekannter Webseite und **fehlenden Stücken** wird je eine
   `CrawlQuelle` Typ **BandDomain** vorgeschlagen; der Admin gibt sie frei → zweiter Crawl sucht das Programm.
3. **Rück-Zuordnung:** Gefundene Stücke werden dem passenden **(Lokal, Datum)-Konzert** des Events zugeordnet.

> **Keine Such-API nötig:** Die URL-Auflösung kommt aus der Event-eigenen Vereinsliste (robust, keine
> Fehlzuordnung). Such-API bleibt optionaler späterer Fallback (mit Verifikation). **Teildaten sind ok:**
> ist das Wettstück nirgends publiziert, bleibt es leer – nicht raten.

**Filterregeln (in Worten formulierbar):**
- **Feldfilter** auf extrahierten Spalten funktionieren direkt: `Rang ≤ 3` („Top3"), `Kategorie = Harmonie`,
  `Land = Schweiz`.
- **Region („Innerschweiz")** ist **kein** Feld auf den Listen → nur via **Ort→Kanton-Anreicherung**
  (Mapping LU/UR/SZ/OW/NW/ZG) oder indem der Admin aus den extrahierten Treffern auswählt.

> **Rang jetzt speicherbar (Update 2026-06-26):** Für Wettbewerbs-Konzerte (SBBW, §4.2) wurde `KonzertBand`
> um **`Rang`** (+ optional `Punkte`) ergänzt (siehe Spezifikation.md). Die **Stärkeklasse/Kategorie** bleibt
> wie gehabt ein Band-Feld. Außerhalb von Wettbewerben bleibt `Rang` leer; für reine Spielplan-Events ist
> Rang weiter nur Filterkriterium.

### 4.2 Wettbewerb: Schweizer Brass Band Wettbewerb (SBBW) — Spezial-Crawler

**Ziel:** Pro **Jahr × Kategorie** ein `Konzert` mit **vollständiger Rangliste** (1. Rang oben) – je Band:
Platzierung, Dirigent:in, Aufgabestück (+ Komponist:in) und – nur **Höchstklasse** – Selbstwahlstück; dazu die
zugehörigen **Videos** eingebettet. Begründung für eigenen Typ/Handler: die Daten verteilen sich über **zwei
strukturierte Quellen**, die zusammengeführt werden müssen – das passt nicht in den generischen Seiten-Extraktor.
Aufbau analog `EmfVereinImporter` (dedizierter Handler, per URL erkannt), darf intern das **LLM** nutzen.

**Quellen (verifiziert an swissbrass.ch, 2025):**
- **Übersicht** `…/resultate-sbbw`: verlinkt je **Jahr** ein Ergebnis-PDF
  (`…/data/files/documents/resultate_sbbw/results_<jahr>.pdf`, 2021–2025).
- **Jahres-PDF** (z. B. `results_2025.pdf`): je **Kategorie** eine Seite (Höchstklasse/Excellence, Elite,
  1.–4. Kategorie). Kopf: Wettbewerbsnummer/-titel, **Datum** (Sa/So), Halle; **Aufgabestück + Komponist:in**.
  Tabelle: Startnr., **Band** (+ Kanton), **Dirigent:in**, Teilränge/Punkte, **Endrang**; in Höchstklasse
  zusätzlich je Band der **Selbstwahlstück-Titel** (Spalte „Pièce à choix" – **ohne Komponist:in**).
- **Video-Index** `…/videos-sbbw`: verlinkt **Unterseiten je Jahr + Kategoriegruppe**
  (`<jahr>-ch-elite`, `<jahr>-1st-2nd`, `<jahr>-3rd-4th`). Jede Unterseite: Abschnitts-Überschrift
  (Kategorie + Jahr), **Aufgabestück + Komponist:in** im Klartext, und je Video ein **Infomaniak-VOD-iframe**
  (`https://player.vod2.infomaniak.com/embed/<id>`) mit Band-/Stück-Beschriftung (Aufgabe- vs. Selbstwahlstück).

**Pipeline (Handler):**
1. **Jahre ermitteln** aus `…/resultate-sbbw` (PDF-Links) – bzw. Admin gibt Jahr/PDF gezielt vor.
2. **PDF je Kategorie → LLM:** PDF-Text (PdfPig, `-layout`-ähnlich) pro Kategorie-Abschnitt ans LLM →
   strukturiertes JSON: `{ kategorie, datum, ort?, aufgabestueck{titel,komponist}, zeilen:[{rang, band,
   kanton, dirigent, punkte?, selbstwahlstueck?}] }`. Das LLM gleicht die in der PDF **spaltenversetzten**
   Selbstwahl-Titel den Rang-Zeilen zu (deterministisch zu brüchig → LLM ist hier stabiler).
3. **Video-Unterseite je Jahr/Gruppe** (HTML, kein Rendering nötig): iframes + Beschriftungen extrahieren
   (DOM/LLM) → Liste `{kategorie, band, stueckTyp(Aufgabe|Selbstwahl), embedId}`.
4. **Join** PDF ↔ Video über **(Jahr, Kategorie, Band-Name)** (Normalisierung + `BandAlias`, Kanton-Suffix
   wie „(VS)" abtrennen). Video wird dem passenden Stück (Aufgabe/Selbstwahl) der Band zugeordnet.
5. **Selbstwahlstück-Komponist:in fehlt** → **best-effort Auflösung** (Web-/Google-Suche + LLM, oder LLM-Wissen)
   als **niedrig-konfidenter Vorschlag**, den der Admin bestätigt; ohne Treffer bleibt das Feld leer (nicht raten).
   Provider ist **pluggable** (`IKomponistSuche`), Default „leer + Review-Flag" wenn kein Such-Provider gesetzt.
6. **Ergebnis = je (Jahr, Kategorie) ein Konzert-Fund** (`CrawlFundTyp.Konzert`, erweiterte Daten): Datum,
   Name (z. B. „SBBW <Jahr> – <Kategorie>"), Bands **mit Rang/Punkte/Dirigent:in**, Stücke (Aufgabe + Selbstwahl
   inkl. Komponist:in) und **Video-Referenzen** (Plattform InfomaniakVod). Bei der **Übernahme** baut
   `KonzertErfassungService` daraus Konzert + `KonzertBand`(Rang/Punkte) + `KonzertStueck` + `KonzertPerson`
   (Dirigent:in) + `Video`. Find-or-create für Band/Stück/Person wie gehabt (Alias-/Merge-fähig).

**Modell-Anpassungen (in Spezifikation.md gepflegt):** `KonzertBand.Rang`/`.Punkte`; `Video.Plattform`/`.ExternId`
(+ `EmbedUrl`). Die Erfassungs-Eingabe (`KonzertFundDaten`/`KonzertErfassungService.Eingabe`) wird um Rang/Punkte
und Dirigent:in je Band sowie Video-Referenzen erweitert.

### 4.3 Veranstalter: KKL Luzern (vivenu) — Spezial-Crawler

**Quelle:** `kkl-luzern.ch/events` (Next.js/Vercel mit Bot-Schutz → **Rendering nötig**). Die Events kommen vom
Ticketing-Anbieter **vivenu**; pro Event eine öffentliche JSON-API `vivenu.com/api/events/info/<id>`.

> **Wichtig – Render-User-Agent:** Vercel liefert bei **Bot-UA** (z. B. `HarmoniQBot/1.0`) nur den
> „Vercel Security Checkpoint" und unterdrückt die Client-XHR → **0 Funde**. Der Renderer tritt deshalb mit
> realem Browser-UA auf (`Crawler:RenderUserAgent`, Default Chrome). HTTP-Fetches identifizieren sich weiter
> ehrlich als `Crawler:UserAgent`. Lokal verifiziert: Bot-UA → 0, Chrome-UA → 10 Events erfasst.

**Pipeline (`KklImporter` + `CrawlRunner.KklImportierenAsync`):**
1. **Stil-Filter über die Site-Kategorie:** Passt der Stil-Hinweis zu einer KKL-Kategorie
   (`KklImporter.GenreAusHinweis`: „Blasmusik / Brassband" → **Blasmusik**; weitere: Klassik, Jazz, Rock & Pop,
   Comedy, Filmmusik, Weltmusik, Volksmusik, Musical, Weihnachtsmusik), wird die Liste direkt als
   `…/events?genre=<Kategorie>` gecrawlt — die **Website filtert selbst**, kein LLM-Filter nötig. Passt der
   Hinweis zu keiner Kategorie, wird die Default-Liste geladen und je Event per LLM (`KklEventAsync`) gefiltert.
2. **Discovery:** Liste rendern und die clientseitig nachgeladenen **vivenu-API-Antworten mitschneiden**
   (`ISeitenRenderer.RenderUndSammleAsync`, Filter `vivenu.com/api/events/info`). Direkt-Navigation zur
   `?genre=`-URL löst die gefilterten vivenu-Calls aus (verifiziert: `?genre=Blasmusik` → 10 Blasmusik-Events).
3. **Mapping** (`KklImporter.Parse`, verifiziert): je Event → Titel (`name`), **Datum + Uhrzeit** (`start`, in CH-Zeit),
   **Saal** (`meta.venue`: konzertsaal → **„Weisser Saal"**, luzernersaal → „Luzerner Saal"), **Bild** (`image`),
   **Beschreibung** (`description` Rich-Text → Klartext), Slug (`url`) für die klickbare Quell-URL.
4. **Detailseite rendern → Programm + Besetzung:** je Event die Detailseite rendern, die Tabs **„Programm"** und
   **„Mitwirkende"** anklicken (`ISeitenRenderer.RenderUndTabsAsync`, akzeptiert Cookie-Banner) und den
   Abschnittstext herausschneiden (`KklImporter.Abschnitt` – Überschrift muss auf **eigener Zeile** stehen,
   längster Treffer → ignoriert „Programm" im Fließtext, „Programm & Tickets"-Navigation und „Programmänderungen").
   Den Text strukturiert der LLM (`IExtraktion.KklProgrammAsync`) in **Stücke (mit Komponist:in)**, **alle
   auftretenden Bands** und – nur bei genau einer Band – die **Dirigent:in**. Erkennt beide Komponisten-Formate
   („Name (1898–1937)" mit Lebensdaten und „Komponist: Titel" mit Doppelpunkt); Kategorie-Überschriften
   („TESTSTÜCK", „SELBSTWAHLSTÜCKE"), Gespräche/Pausen/Vorspann sind keine Stücke. **Wettbewerbe/Contests** (mehrere
   Bands, gemeinsames Teststück + Selbstwahlstücke) werden so korrekt erfasst.
5. **Konzert-Fund** je Event: Datum, Name, Ort „KKL Luzern, <Saal>" (ohne Saal nur „KKL Luzern"), Beschreibung,
   Bild, **Programm** (`ProgrammZeileDaten`; Stück-Band-Zuordnung nur bei genau einer Band), **alle Bands**
   (+ Dirigent:in bei Einzelband) als rangslose `RangZeileDaten` → mappt auf `KonzertBand` (+ `KonzertPerson`
   Dirigent). **ExternKey = vivenu-Event-ID** → Dedup über Läufe (§7).

**Grenzen:** nur **zukünftige** Events (kein öffentliches Archiv); pro Event ein zusätzlicher Detail-Render
(~5–10 s); Solist:innen werden (noch) nicht als eigene Mitwirkende übernommen (nur Bands + Dirigent:in bei
Einzelband); auf Railway noch im Betrieb zu prüfen (realer Render-UA umgeht den Vercel-Checkpoint lokal; Vercel
könnte zusätzlich Headless fingerprinten). Hinweis: Ein bereits **übernommener** Fund wird beim erneuten Lauf per
ExternKey übersprungen – um ihn mit verbesserter Extraktion neu zu ziehen, den Fund löschen und neu crawlen.

### 4.4 Veranstalter: Eventfrog.ch (Public API) — Spezial-Crawler

**Ziel:** Schweizweit **neue** Blasmusik-Konzerte finden, die (noch) nicht über eigene Vereins-Crawls (§4.1
BandDomain) oder andere Quellen bekannt sind. Anders als bei KKL (§4.3, ein einzelner Veranstaltungsort)
deckt Eventfrog **viele tausend Veranstalter schweizweit** ab — entsprechend unspezifischer ist das
Rohmaterial, entsprechend wichtiger die Filterstufen vor der Fund-Erzeugung.

**Quelle:** Eventfrog **Public API** (dokumentiert unter `docs.api.eventfrog.net/#publicapi-v1`), read-only.
Zugriff per API-Key vom Typ „Public API", erzeugt im Eventfrog-Cockpit unter Einstellungen → API-Keys.
Reine REST-API — **kein Rendering nötig** (im Unterschied zu KKL entfällt Playwright hier vollständig).

**Unterschied zu KKL (entscheidend für den Handler-Entwurf):** KKL filtert Genre serverseitig
(`?genre=Blasmusik`); Eventfrog kennt nur die grobe Rubrik **„Konzerte"** (Rubrik-ID vermutlich `908` —
vor dem ersten Lauf über den Rubriken-Endpoint der API verifizieren und cachen, nicht hart codieren). Eine
Blasmusik-Filterung muss deshalb **clientseitig** erfolgen — Aufbau analog dem generischen
Seiten-Filter-Prinzip aus §8 (billige Heuristik zuerst, LLM nur bei Bedarf), nicht analog KKLs
direktem Genre-Parameter.

**Pipeline (Vorschlag `EventfrogImporter` + `CrawlRunner.EventfrogImportierenAsync`, Aufbau analog
`KklImporter`):**
1. **Rubrik-Filter (serverseitig):** Events der Rubrik „Konzerte" abrufen, paginiert, Zeitraum „ab heute" bis
   konfigurierbarem Horizont (z. B. +6 Monate).
2. **Blasmusik-Vorfilter (billig, Heuristik zuerst):** Titel/Beschreibung/Veranstalter-Name gegen eine
   Keyword-Liste prüfen (`Blasmusik, Blasorchester, Musikgesellschaft, Musikverein, Stadtmusik, Harmonie,
   Brass Band, Jugendmusik, Bürgermusik, Feldmusik, Fanfare, Spielmannszug, Tambouren, Blaskapelle` …).
   Eindeutige Vereinstyp-Treffer („Musikgesellschaft", „Blasorchester") erzeugen **ohne LLM** direkt einen
   Fund (Kostenkontrolle). Mehrdeutige Treffer (z. B. „Harmonie", „Fanfare" — auch ausserhalb der Blasmusik
   gebräuchliche Wörter) gehen an einen **LLM-Filter** je Event (`IExtraktion.EventfrogEventAsync`, analog
   `KklEventAsync`), grounded auf Titel+Beschreibung, statt zu raten.
3. **Regionsfilter (Deutschschweiz):** Location-Objekt der API auswerten (Ort, ggf. PLZ). Zuordnung zu Kanton
   über die bereits als offen geführte **Ort→Kanton-Anreicherung** (§4.1, Phase C4) — kein neuer Mechanismus,
   sondern Wiederverwendung. Bis diese existiert: Übergangslösung über PLZ-Bereiche/Ortsnamen-Liste
   Deutschschweizer Kantone (fehleranfällig bei zweisprachigen Kantonen FR/VS/BE-Jura).
4. **Konzert-Fund:** Datum (+ optionale **Uhrzeit** aus der Event-Startzeit), Titel, Ort, Veranstalter-Name (sofern von der API geliefert), Quell-URL,
   **Konfidenz** (Hoch bei eindeutigem Keyword-Treffer, Mittel/Tief bei LLM-Filter-Treffer) — nutzt das
   bestehende `CrawlFund.Konfidenz`-Feld, kein neues Feld nötig. **Programm/Stücke** liefert Eventfrog nicht
   (reine Ticketing-Plattform ohne Werkangaben) — im Unterschied zu KKL bleibt das Programm hier grundsätzlich
   **leer**; ein Programm käme frühestens über einen kaskadierenden Zweitlauf auf die Vereins-Webseite
   (Zweiter-Durchgang-Muster aus §4.1), sofern diese aus dem Fund erkennbar/verlinkt ist.
5. **Band-Zuordnung:** Vereinsname aus Titel/Veranstalter-Feld wird — wie bei den übrigen Quelltypen — gegen
   `Band`/`BandAlias` gematcht (Find-or-create beim Übernehmen); ohne eindeutigen Treffer bleibt das Konzert
   bandlos, der Admin ordnet in der Review zu.
6. **Dedup über Läufe:** `ExternKey = Eventfrog-Event-ID` — exakt das bestehende Muster aus §7 (analog
   vivenu-Event-ID bei KKL). Jeder erneute Lauf liefert dadurch automatisch **nur neue** Konzerte als offene
   Funde; bereits übernommene oder verworfene Events werden übersprungen bzw. aktualisiert statt verdoppelt.
   Damit ist die gewünschte Eigenschaft „von Zeit zu Zeit starten und nur neuere Konzerte als Funde erhalten"
   **ohne zusätzliches Datenmodell** abgedeckt — es braucht keine separate Sichtungstabelle, `CrawlFund` +
   `ExternKey` reichen.

**Offene Punkte (vor Umsetzung zu klären):**
- Tatsächliche Rubrik-ID(s) und Query-Parameter-Name der Public API verifizieren (Website nutzt
  `?rubrics=908`; ob die API denselben Parameter/Wert erwartet, ist zu testen).
- Zuverlässigkeit des Location-Objekts (liefert es Kanton direkt, oder nur Freitext-Ort/Koordinaten?) —
  entscheidet, ob die Ort→Kanton-Anreicherung (C4) Voraussetzung wird oder ein einfacherer Zwischenschritt reicht.
- Rate-Limits/Antwortgrösse der Public API bei einer schweizweiten Alle-Konzerte-Abfrage (deutlich höheres
  Volumen als bei KKL); ggf. serverseitig zusätzlich nach Datum/Region einschränken statt alles zu laden und
  erst clientseitig zu filtern.
- Keyword-Liste ist ein lebendes Artefakt (vgl. §10) — Pflege anhand beobachteter Fehlklassifikationen vorsehen.
- Verhältnis zu Musiktreff.info als mögliche zweite Quelle (nur RSS, kein Rubrik-/Regionsfilter) — falls
  gewünscht, eigener Quelltyp/Handler, nicht Teil dieses Abschnitts.

### 4.5 YouTube-Videos pro Band (Band-Admin, on-demand) — Direkt-Handler

**Ziel:** Zu einer bestehenden Band deren YouTube-Auftritte finden und als `Video`-Datensätze übernehmen.
Zwei Wege, beide mit LLM-Erkennung von **Stück + Komponist:in aus dem Videotitel**
(`IExtraktion.VideoTitelAnalysierenAsync`, grounded auf den Titel — nicht raten):

1. **Einzel-Link** (`/admin/bands/{id}/video`, `BandVideoHinzufuegen`): Admin fügt einen YouTube-Link ein →
   Titel via oEmbed (`YouTubeMetadataService`, kein Key) → LLM schlägt Stück/Komponist:in vor → nach
   Prüfung speichern. Erzeugt sofort ein **genehmigtes** `Video` (Band-Admin ist für die eigene Band
   vertrauenswürdig).
2. **Suche pro Band** (`/admin/bands/{id}/videos-suchen`, `BandVideosSuchen` + `BandVideoCrawlService`):
   holt Kandidaten bei der YouTube Data API (`YouTube:ApiKey`), lässt je Treffer das LLM Stück/Komponist:in
   vorschlagen und legt neue Treffer als `BandVideoFund` (Status Offen) ab. **Quelle bevorzugt der Kanal:**
   ist an der Band ein YouTube-Link hinterlegt (`BandLink` Typ `YouTube`, z. B. `youtube.com/@Handle`,
   `/channel/UC…`, `/user/…`), werden gezielt **dessen Uploads** durchgegangen (`channels.list` →
   Uploads-Playlist → `playlistItems.list`); nur ohne Link (oder wenn der Kanal 0 Videos liefert) fällt der
   Handler auf die **Namenssuche** (`search.list` über den Bandnamen) zurück. Die Review zeigt Thumbnail +
   **Video-Preview** (Embed) und **editierbare** Felder Stück/Komponist:in; der Admin entscheidet je Fund
   Übernehmen (→ `Video`, find-or-create Stück/Person wie bei der Konzert-Erfassung via `VideoErfassung`)
   oder Ablehnen.

**Konzert-Zuordnung (beide Wege, Entscheid 2026-07-13):** Beim Erfassen/Übernehmen wird das Video –
falls erkennbar – dem passenden vergangenen Konzert der Band zugeordnet (`Video.KonzertId`). Logik in
`VideoErfassung.BandKonzerteAsync`/`EindeutigesKonzert`: hat **genau ein** nicht-künftiges Konzert der Band
das gewählte Stück im Programm, wird es **vorgeschlagen** (Feld „Konzert (optional)" vorbelegt, Hinweis
„Automatisch erkannt …"); bei 0 oder mehreren Treffern bleibt es leer und der Admin **wählt** aus der
(neueste-zuerst-)Liste der Band-Konzerte (passende mit ✓ markiert). Der Abgleich läuft in-memory
(Titel + `StueckAlias`), reagiert also live auf Änderungen am Stück-Feld. So landet das Video auf der
Konzert-Detailseite und Bewertungen/Notizen binden an den Konzerttag.

**On-demand statt Sammellauf (Entscheid):** ausgelöst pro Band auf Knopfdruck. Grund: die YouTube-Suche
kostet **100 Kontingent-Einheiten/Aufruf** (Default 10 000/Tag ≈ 100 Band-Suchen/Tag) — ein Sammellauf über
alle Bands wäre kontingent-limitiert und müsste über Tage verteilt werden.

**Kanal vor Namenssuche (Entscheid, 2026-07-13):** Der Kanal-Weg ist **präziser** (genau die Uploads dieser
Band statt namensähnlicher Fremdtreffer) und **günstiger** (`channels.list` + `playlistItems.list` je
1 Einheit statt 100 für die Suche). Deshalb: Kanal-Link, wenn vorhanden — die Namenssuche bleibt Fallback
für Bands ohne hinterlegten Kanal. Empfehlung fürs Datenpflegen: bei den Bands den YouTube-Kanal als
`BandLink` erfassen.

**Inkrementell („nur Neueres"):** vor dem Anlegen wird jede Video-ID gegen bereits erfasste `Video`s der
Band **und** bereits vorhandene `BandVideoFund`s (egal ob offen/entschieden) geprüft; Duplikate werden per
Unique-Index `(BandId, ExternId)` zusätzlich hart verhindert. Ein erneuter Suchlauf liefert daher nur
wirklich neue Treffer; einmal übernommene/abgelehnte Videos tauchen nicht wieder auf. Analog zum
`ExternKey`-Dedup der übrigen Quellen (§7), hier über die dedizierte Sichtungstabelle statt `CrawlFund`,
weil die Funde **standalone Band-Videos** ohne Konzert-/Lauf-Bezug sind.

## 5. Datenmodell (neue Entitäten, isoliert vom Kernmodell)

```
CrawlQuelle                         (Seed: Band-Domain, Dokument/PDF oder Event)
├── Id (Guid)
├── Typ (enum: BandDomain / Dokument / Event / Wettbewerb / Veranstalter)   ← Wettbewerb=SBBW (§4.2), Veranstalter=KKL (§4.3) / Eventfrog (§4.4)
├── BandId (FK → Band?)             ← Zielband (bei BandDomain; sonst optional)
├── StartUrl (string)               ← Domain-Start, PDF-/Dokument-Link oder Event-Seite
├── Domain (string?)                ← bei BandDomain; Crawler bleibt darauf
├── BrauchtRendering (bool)         ← Event/SPA: per Headless-Browser rendern (C2)
├── Anforderungen (flags)           ← Bitset: KonzertBrauchtStueck(1), VorstandCrawlen(2),
│                                      MukoCrawlen(4) — alle umgesetzt
├── ExtraktionsHinweis (string?)    ← Freitext-Zusatzanweisung ans LLM, vor jedem Lauf editierbar
│                                     (z. B. „Nur Konzerte ab 2023 …"); wirkt auch als Filter
├── MaxTiefe (int, Default 2)       ← nur BandDomain
├── MaxSeiten (int, Default 100)    ← nur BandDomain
├── Aktiv (bool)
├── ErstelltAm (DateTime)
└── LetzterLaufAm (DateTime?)

CrawlLauf                           (ein Durchlauf einer Quelle)
├── Id (Guid)
├── QuelleId (FK)
├── Status (enum: Laufend / Fertig / Fehler / Abgebrochen)
├── StartAm (DateTime) · EndeAm (DateTime?)
├── SeitenBesucht (int) · FundeAnzahl (int)
└── Meldung (string?)               ← Fehler/Zusammenfassung

CrawlFund                           (Kandidat zur Übernahme)
├── Id (Guid)
├── LaufId (FK)
├── Typ (enum: Konzert / Leitung / Stück / Komponist / Band / Sonstiges)
├── QuellUrl (string)               ← Provenienz
├── AbgerufenAm (DateTime)
├── DatenJson (string)              ← strukturierter Vorschlag (Konzert+Programm bzw. Person+Rolle)
├── Konfidenz (enum?: Hoch/Mittel/Tief)  ← optional, aus Heuristik/LLM
├── DublettHinweis (string?)        ← „existiert evtl. schon als …"
├── Status (enum: Offen / Übernommen / Verworfen)
└── EntschiedenAm (DateTime?)

(optional) CrawlSeite               (Dedup/Politeness über Läufe)
├── Id · QuelleId (FK) · Url · InhaltsHash · AbgerufenAm · Relevant (bool)

BandVideoFund                       (YouTube-Kandidat pro Band, §4.5 — eigene Sichtungstabelle)
├── Id (Guid)
├── BandId (FK → Band, Cascade)     ← Zielband; Band-Löschung räumt die Funde mit weg
├── ExternId (string)               ← YouTube-Video-ID; Unique-Index (BandId, ExternId) = Dedup
├── Titel (string) · KanalName (string?)
├── StueckVorschlag (string?)       ← vom LLM aus dem Titel erkannt, in der Review editierbar
├── KomponistVorschlag (string?)    ← dito
├── Status (enum CrawlFundStatus: Offen / Übernommen / Verworfen)   ← Status hält „nur Neueres"
├── GefundenAm (DateTime) · EntschiedenAm (DateTime?)
└── ErgebnisVideoId (Guid?)         ← bei Übernahme: erzeugtes Video
```

> `DatenJson` hält den Vorschlag flexibel (z. B. ein Konzert mit Programmzeilen). Die Review-UI
> deserialisiert ihn und mappt beim Übernehmen auf die bestehenden Import-Eingabe-Records
> (`KonzertErfassungService.Eingabe` etc.).

## 6. Steuerung (Einflussnahme)

- **Seed-Verwaltung** `/admin/crawler`: Bands + Start-URLs erfassen/aktivieren, Limits (Tiefe/Seiten) setzen.
- **Läufe** werden **über** den Quellen angezeigt (Fortschritt beobachten, „Aktualisieren") und sind **einzeln löschbar** (Funde kaskadieren mit; übernommene Daten bleiben).
- **Lauf starten/stoppen**, Fortschritt & Log sehen.
- **Allowlist-Domain** je Quelle (kein Abwandern). **URL-Stichwortfilter** konfigurierbar.
- **Discovery mit Bremse:** Gefundene Links zu *anderen* Vereinen erscheinen als **Quellen-Vorschläge**
  (eigene Liste) – der Admin entscheidet, welche neue `CrawlQuelle` werden.

## 7. Review & Übernahme

- `/admin/crawler/funde`: Kandidaten je Lauf/Band, gefiltert nach Typ/Status. Pro Kandidat eine
  **benutzerfreundliche Zusammenfassung** je Typ (Dirigent:in mit Band/Zeitraum; Konzert mit Datum/Ort und
  Programmzeilen „Stück — Komponist, arr. … · Band" in Reihenfolge; Stück; Komponist:in), Quell-Link,
  Dublett-Hinweis, **Übernehmen** / **Verwerfen**. Das rohe **JSON** ist nur noch einblendbar und dort
  editierbar (z. B. fehlendes Datum ergänzen) – diese Daten werden beim Übernehmen verwendet. Nach jeder
  JSON-Änderung wird die **Vorschau neu berechnet** (Feld verlassen oder Button „Vorschau neu berechnen");
  ist das JSON **kaputt**, erscheint eine klare Fehlermeldung, „Übernehmen" ist gesperrt und Massen-Übernahme
  überspringt solche Funde – so ist immer sichtbar, was beim Übernehmen tatsächlich gilt.
- **Volltext-Suche** über die Funde (Name/Ort/URL – ILIKE auf DatenJson/QuellUrl), um z. B. Webseiten-Funde
  einzugrenzen. **Massen-Aktionen** (auf den aktuellen Filter): „Alle angezeigten übernehmen",
  „Alle offenen verwerfen", „Alle angezeigten löschen".
- **Dedup über Läufe (`CrawlFund.ExternKey`):** Hat ein Fund einen stabilen Quell-Schlüssel (z. B. vivenu-Event-ID),
  überspringt ein erneuter Lauf bereits **übernommene oder verworfene** Gegenstände und **aktualisiert** offene
  statt sie zu verdoppeln. So zeigt ein Wiederholungslauf nur wirklich Neues; „nicht übernehmen" bleibt respektiert.
- **Konzert-Dedup generell:** `KonzertErfassungService.ErfasseOderAktualisiereAsync` macht Find-or-create.
  Identität = **Datum + Name + Ort**; hat die Eingabe zusätzlich Band-Angaben, muss auch **mindestens eine Band
  übereinstimmen** – sonst sind es verschiedene Konzerte (z. B. mehrere „Jahreskonzerte" am selben Samstag, ob in
  verschiedenen Sälen/Orten oder gar im selben Saal mit anderen Bands). Wiederholtes Übernehmen verdoppelt nichts.
- **Reaktivieren:** Ein entschiedener Fund lässt sich **wieder öffnen** und **erneut übernehmen** – verworfene
  („doch übernehmen") wie auch bereits übernommene (z. B. wenn das Ziel im CRUD gelöscht wurde). Idempotent.
- **Anzahl im Crawler-Fenster** = nur **offene** Funde je Lauf (übernommene/verworfene zählen nicht mehr mit).
- **Übernehmen** ruft die Find-or-create-Services → keine Dubletten; Quell-URL bleibt als Provenienz erhalten.
  Übernahme-Pfade: Konzert → `KonzertErfassungService`; Leitung → `BandMitgliedschaft` (Funktion „Dirigent",
  optional Von/Bis-Jahr); Stück → `Stueck` (+ `StueckBeitrag` Komponist/Arrangeur); Komponist:in → `Person`;
  **Verein → `Band`** (Abgleich über Name/Alias; füllt leere Stammdaten – Land, Webseite, Kategorie,
  Stärkeklasse, Gründungsjahr, Geschichte – und ergänzt `BandAlias` + `BandLink`-Social-Links **inkl.
  offizieller E-Mail**);
  **Webseite → inaktive BandDomain-Quelle (Vorschlag)**.
- **Band-Admin-Einladung aus gefundenem Kontakt (Phase 2 A):** Eine übernommene Vereins-**E-Mail**
  (`BandLink` Typ `EMail`) erzeugt **keine** automatische Mail. Sie erscheint als **Vorschlag** auf
  `/admin/band-einladungen`; der globale Admin **prüft die Band** und entscheidet manuell
  Einladen / Nicht einladen (Details Spezifikation.md, Abschnitt BandAdministrator). So geht keine
  Einladung an eine halbfertige/falsche Band raus.
  Bei **BandDomain**-Funden wird das Konzert immer der Quell-Band zugeordnet. Ebenso werden **Personen**
  von einer Vereinsseite (Dirigent, Vorstand, Muko) tendenziell der **Quell-Band** zugeordnet (Quell-Band
  vor evtl. fehlerhaft erkanntem Namen). Hat eine BandDomain-Quelle keine Ziel-Band, wird sie zu
  Lauf-Beginn aus der Domain bestimmt/angelegt (Abgleich über Webseite-Host), damit eine Zuordnung möglich ist.

## 8. Extraktion im Detail

1. **Seiten-Filter (gratis):** `SeitenFilter` (Keyword-Triage in URL/Text) + `CrawlHtmlHelfer` (Hauptinhalt
   bereinigen, interne Links ernten). Nur relevante Seiten gehen ans LLM (Kostenkontrolle).
2. **LLM-Extraktion:** Bereinigter Seiten-/PDF-Text → `IExtraktion.ExtrahiereAsync(ExtraktionsAnfrage)`.
   Implementierung `MistralExtraktion` (Chat-Completions, `response_format: json_object`). Auswahl per
   Konfiguration: `Crawler:Llm:Provider` (= `mistral`), `…:ApiKey` (user-secrets/ENV, **nie** eingecheckt),
   `…:Model`, `…:TagesLimit`, `…:LogCalls`. Liefert `ExtrahierterFund`-Liste (Typ + DatenJson + Konfidenz).
   **Prompt-Regeln:** Fakten wörtlich, nicht raten; Komponist:in vs. **Arrangeur:in** trennen („arr. X");
   Personennamen „Vorname Nachname"; `reihenfolge` = Startzeit als Zahl (14:40 → 1440), sonst fortlaufend;
   Datum nicht mit Nullen auffüllen; enthält der **Admin-Hinweis** eine Einschränkung, wirkt er als **Filter**
   (nur passende Funde) – inkl. Merkmalen wie **Stärkeklasse** (z. B. „nur Höchstklasse"), **Land**,
   **Kategorie/Besetzung**, Jahr, Ort, sofern das Merkmal auf der Seite steht (verifiziert). Was NICHT auf der
   Seite steht (z. B. Kanton zu einem Ort) kann das LLM nicht filtern → dafür bliebe Ort→Kanton (C4). Bei BandDomain ist die Quell-Band Standard-Band, wenn keine genannt. Auf der eigenen
   Vereinsseite wird zusätzlich ein **Vereins-Block** extrahiert (`verein`: Name, Aliase, Land, Webseite,
   Gründungsjahr, Kategorie, Stärkeklasse, Geschichte, Social-Links) → Band-Fund. Das **Logo** (`BildUrl`)
   kommt nicht vom LLM (der sieht nur Text), sondern **heuristisch aus dem HTML** (`og:image` →
   `<img>` mit „logo" → `apple-touch-icon`).
3. **Robustheit:** Tolerantes Parsen der LLM-Antwort (z. B. Datum „1935-00-00" → 1935-01-01, Zahl als String) –
   kein Crash bei unsauberen Werten.
4. **Kostenkontrolle:** nur relevante Seiten ans Modell; Text-Obergrenze je Aufruf; optional Tageslimit.

## 9. Umsetzungs-Reihenfolge

> **Hinweis:** Die Reihenfolge wurde gegenüber dem ursprünglichen Plan angepasst — **GUI/Plumbing zuerst,
> LLM früh vorgezogen** (C3 vor C2). C1 und C3 sind umgesetzt; C2 und C4 offen (siehe §0).

**Phase C1 – Grundgerüst (HTML + PDF) ✅ umgesetzt:** CrawlQuelle/-Lauf/-Fund-Modell + Migration; Fetch mit
robots.txt/Rate-Limit/Domain-Grenze **und PDF-Text-Extraktion** (PdfPig); Seiten-Filter; Quelltypen
**BandDomain** + **Dokument/PDF**; Orchestrator (Queue + IHostedService, BFS, Dedup); Admin-Seiten
(Seeds, Lauf, Funde-Review); Übernahme über bestehende Find-or-create-Services.

**Phase C3 – LLM-Extraktion ✅ umgesetzt (vorgezogen):** `IExtraktion`-Abstraktion + `MistralExtraktion`
(`mistral-large-latest`, JSON-Modus); ersetzt die Heuristik als Fund-Produzent. Konfidenz, tolerantes Parsen,
Arrangeur-Trennung, Reihenfolge/Jahre, Admin-Hinweis-Filter, optionales Call-Logging.

**Phase C2 – JS-Rendering, Event-Quellen & kaskadierende Crawls (teilweise ✅):** Headless-Browser
(Playwright) ✅; Quelltyp **Event** mit Rendering ✅; Programm-Extraktion „(Lokal, Datum) → ein Konzert"
übernimmt das LLM (gruppiert nach Datum+Ort) ✅; **Vereins-Link-Ernte** → inaktive **BandDomain-Folgeaufträge**
als Vorschlag ✅. **Offen:** **Join** Rangliste-PDF ↔ Spielplan über Vereinsnamen; **Rück-Zuordnung** der im
zweiten Durchgang gefundenen Stücke ans passende (Lokal,Datum)-Konzert; automatisches Füllen von `Band.Webseite`.

**Phase C4 – Ausbau (optional):** **Ort→Kanton-Anreicherung** für Regionfilter („Innerschweiz");
Feldfilter-UI (Rang/Kategorie/Land); Discovery-Vorschläge anderer Bands; Verbands-/Verzeichnis-Quellen;
geplante Läufe; **später** Mitglieder mit Datenschutz-Schranken.

**Phase C5 – Wettbewerb SBBW (§4.2):** Quelltyp **Wettbewerb** + Spezial-Handler `SbbwImporter`.
- **Schritt 1 ✅** Datenmodell + Migration `VideoPlattformUndKonzertRang`: `KonzertBand.Rang/Punkte`,
  `Video.Plattform/ExternId` (+ `EmbedUrl`); zentraler `VideoEinbettung`-Helper (Player je Plattform).
- **Schritt 2a ✅** PDF → Rangliste: `IExtraktion.SbbwRanglisteAsync` (Mistral, JSON-Modus) strukturiert das
  Jahres-PDF (PdfPig-Text) je Kategorie; `CrawlRunner.SbbwImportierenAsync` legt je (Jahr, Kategorie) einen
  **Konzert-Fund** an (Datum, Aufgabestück+Komponist, Rang/Band/Kanton/Dirigent/Punkte, Selbstwahl-Titel
  best-effort). Übernahme: `KonzertErfassungService` + `RaengeUebernehmenAsync` → Konzert + KonzertBand[Rang/Punkte]
  + KonzertStueck + KonzertPerson[Dirigent]. Verifiziert an results_2025.pdf (6 Kategorien, Endränge korrekt).
- **Schritt 2b ✅** Video-Unterseiten (`<jahr>-ch-elite|1st-2nd|3rd-4th`) werden zu einem Outline
  **linearisiert** (`CrawlHtmlHelfer.VideoSeiteOutline`: iframe → `[[VIDEO:id]]`-Marker im Textfluss) und vom
  **LLM** (`IExtraktion.SbbwVideosAsync`) je Video zu Kategorie/Band/Stück zugeordnet. Das ist robust gegen die
  **uneinheitlichen** Seiten (manche Captions nennen Bandnamen, andere nur Selbstwahl-Titel) – verifiziert:
  korrekte Band↔Video-Paarung auf beiden Layouts. Die Videos (Plattform InfomaniakVod) hängen am Konzert-Fund
  (`KonzertVideoDaten`); Übernahme erzeugt `Video`-Datensätze (Stück via Titel, Band falls eindeutig).
- **Schritt 3 ✅** Konzert-Detailseite rendert bei Wettbewerben (KonzertBand mit `Rang`) eine **Rangliste-Tabelle**
  (Rang | Band | Dirigent:in | Stücke | Punkte, nach Rang sortiert); Band, **Dirigent:in** und **Komponist:in**
  je Stück sind verlinkt/klickbar. Pro platzierter Band ein Video-Block mit **Standbild**: Infomaniak-VOD-Poster
  ist aus der Embed-ID ableitbar (`https://api.infomaniak.com/2/vod/res/shares/<id>.preload.jpeg`) →
  `VideoEinbettung.Thumbnail`. **Normale Konzerte** (ohne Rang) zeigen das Programm ebenfalls als **Tabelle**
  (Stück | Komponist:in | Band, eine Zeile pro Stück, alles klickbar). Am echten SBBW-2025-Konzert verifiziert.

**Phase C6 – Veranstalter: Eventfrog.ch (offen, §4.4):** Quelltyp **Veranstalter**, zweiter Handler neben
`KklImporter`. API-Key/Rubrik-ID verifizieren; `EventfrogImporter` (Rubrik-Abruf, Keyword-Vorfilter,
LLM-Fallback-Filter bei mehrdeutigen Treffern, Kanton-Regionsfilter — wiederverwendet C4 sobald vorhanden,
bis dahin Übergangslösung über Ortsliste); Dedup über `ExternKey` (Eventfrog-Event-ID) nach bestehendem
Muster (§7); kein Programm/keine Stücke (nur bei kaskadierendem Zweitlauf auf die Vereins-Webseite, §4.1).

## 10. Offene Punkte / Risiken

- **LLM-Anbieter & Budget** – **entschieden:** Mistral `mistral-large-latest`; Kosten gering
  (~$0.01–0.02/PDF), Tageslimit konfigurierbar (`Crawler:Llm:TagesLimit`).
- **Qualität/False Positives** der Extraktion – mitigiert durch Pflicht-Review.
- **Rechtliches** – robots.txt, Quellen-Provenienz, kein Mitglieder-Scraping vorerst.
- **Heterogene Seiten** – manche Vereine haben kein brauchbares HTML (PDF-Programme, Social-only) → out of scope.
- **Eventfrog-Keyword-Filter (§4.4)** – schweizweiter Konzert-Rubrik-Abruf ohne native Blasmusik-Kategorie
  → Keyword-/LLM-Filter kann sowohl False Positives (generische Wörter wie „Harmonie") als auch False
  Negatives (Vereine ohne Treffer-Wort im Titel) liefern; mitigiert durch Konfidenz-Flag + Pflicht-Review,
  gleiches Prinzip wie bei der generischen Extraktion.
- **Wartung** – Seiten ändern sich; Heuristiken müssen pflegbar/abschaltbar bleiben.
- **Selbstwahlstück-Komponist:in (SBBW, §4.2) – umgesetzt (`KomponistSuche`):** Web-Suche → Snippets →
  LLM-Extraktion (grounded, kein Raten). **Provider = Google Programmable Search JSON API**, aktiv sobald
  API-Key + Such-ID gesetzt sind (Gratis-Kontingent 100/Tag genügt). Config: `Crawler:KomponistSuche:GoogleCx`
  (Pflicht) + API-Key `Crawler:KomponistSuche:GoogleApiKey` **oder ersatzweise der vorhandene `YouTube:ApiKey`**
  (gleiches Google-Projekt; Custom Search API aktivieren + für den Key erlauben). In **Railway** mit `__` statt
  `:` (z. B. `Crawler__KomponistSuche__GoogleCx`). Ohne Key/CX ist die Suche inaktiv → Komponist bleibt leer. **Verworfene Alternativen (getestet):** reines LLM-Wissen
  halluziniert (3× „Peter Graham"); DuckDuckGo-Scraping wird bot-geblockt (HTTP 202 Challenge); MusicBrainz/
  Wikipedia liefern für Wettstücke falsche Treffer. Grounded-LLM mit echtem Such-Snippet liefert dagegen korrekt
  (z. B. „Mnemosyne Phrases" → Torstein Aagaard-Nilsen). Aufgabestück-Komponist kommt weiter direkt aus dem PDF.
- **SBBW-Video-Lizenz/Einbettung** – Infomaniak-VOD-iframes sind öffentlich einbettbar; Provenienz (Quell-URL)
  wird wie bei YouTube geführt. Falls der Anbieter Einbettung sperrt, bleibt der Link als Verweis.
