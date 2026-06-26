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
geplante Läufe), `Crawler:Llm:TagesLimit` durchsetzen, Rendering in Prod aktivieren.

## 1. Ziel & Abgrenzung

**Ziel:** Den manuellen Erfassungsaufwand senken, indem öffentlich verfügbare Strukturdaten von
Vereins-Webseiten (Konzertprogramme, Leitung) automatisch vorgeschlagen werden. **Der Mensch entscheidet** –
nichts wird automatisch publiziert.

**Im Scope (Start):**
- **Konzerte** (Datum, Name, Ort) inkl. **Programm** (Stück + Komponist:in) und **Band**.
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
Stück ein `KonzertStueck` (Stück + Komponist:in). Das bildet das HarmoniQ-Konzertmodell 1:1 ab.

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

> **Modell-Lücke (bewusst):** HarmoniQ kennt **kein Rang-/Wettbewerbs-Kategorie-Feld**. Rang & Klasse aus
> Ranglisten werden daher **nicht gespeichert** (höchstens als Notiz) – relevant nur als *Filterkriterium*
> bei der Übernahme, nicht als Zielfeld. Für den Kern (Konzerte + Vereine + Stücke) ist das ohne Belang.

## 5. Datenmodell (neue Entitäten, isoliert vom Kernmodell)

```
CrawlQuelle                         (Seed: Band-Domain, Dokument/PDF oder Event)
├── Id (Guid)
├── Typ (enum: BandDomain / Dokument / Event)
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
  editierbar (z. B. fehlendes Datum ergänzen) – diese Daten werden beim Übernehmen verwendet.
- **Volltext-Suche** über die Funde (Name/Ort/URL – ILIKE auf DatenJson/QuellUrl), um z. B. Webseiten-Funde
  einzugrenzen. **Massen-Aktionen** (auf den aktuellen Filter): „Alle angezeigten übernehmen",
  „Alle offenen verwerfen", „Alle angezeigten löschen".
- **Übernehmen** ruft die Find-or-create-Services → keine Dubletten; Quell-URL bleibt als Provenienz erhalten.
  Übernahme-Pfade: Konzert → `KonzertErfassungService`; Leitung → `BandMitgliedschaft` (Funktion „Dirigent",
  optional Von/Bis-Jahr); Stück → `Stueck` (+ `StueckBeitrag` Komponist/Arrangeur); Komponist:in → `Person`;
  **Verein → `Band`** (Abgleich über Name/Alias; füllt leere Stammdaten – Land, Webseite, Kategorie,
  Stärkeklasse, Gründungsjahr, Geschichte – und ergänzt `BandAlias` + `BandLink`-Social-Links);
  **Webseite → inaktive BandDomain-Quelle (Vorschlag)**.
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

## 10. Offene Punkte / Risiken

- **LLM-Anbieter & Budget** – **entschieden:** Mistral `mistral-large-latest`; Kosten gering
  (~$0.01–0.02/PDF), Tageslimit konfigurierbar (`Crawler:Llm:TagesLimit`).
- **Qualität/False Positives** der Extraktion – mitigiert durch Pflicht-Review.
- **Rechtliches** – robots.txt, Quellen-Provenienz, kein Mitglieder-Scraping vorerst.
- **Heterogene Seiten** – manche Vereine haben kein brauchbares HTML (PDF-Programme, Social-only) → out of scope.
- **Wartung** – Seiten ändern sich; Heuristiken müssen pflegbar/abschaltbar bleiben.
