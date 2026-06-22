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
  Stück → `Stueck` + `StueckBeitrag`; Komponist:in → `Person`.

**Entscheide:**
- **LLM-Anbieter: Mistral „La Plateforme", Modell `mistral-large-latest`** (Interface bleibt anbieter-neutral).
  Kosten bei der Last vernachlässigbar (~$0.01–0.02 pro PDF) → Qualität entscheidet.
- **Heuristik wurde als *Fund-Produzent* verworfen** (zu fragil); sie dient nur noch als billiger **Seiten-Filter**
  (Keyword-Triage vor dem LLM). Die LLM-Extraktion (ursprünglich C3) wurde **vorgezogen**.
- **Arrangeur:in** wird getrennt von Komponist:in extrahiert (eigener `StueckBeitrag` mit `StueckRolle.Arrangeur`).
- **Diagnose:** LLM-Calls/Antworten optional protokollierbar (`Crawler:Llm:LogCalls`).

**Noch offen:** C2 (JS-Rendering/Event/Kaskade), C4 (Wikipedia-Anreicherung, Region-Filter, geplante Läufe).

## 1. Ziel & Abgrenzung

**Ziel:** Den manuellen Erfassungsaufwand senken, indem öffentlich verfügbare Strukturdaten von
Vereins-Webseiten (Konzertprogramme, Leitung) automatisch vorgeschlagen werden. **Der Mensch entscheidet** –
nichts wird automatisch publiziert.

**Im Scope (Start):**
- **Konzerte** (Datum, Name, Ort) inkl. **Programm** (Stück + Komponist:in) und **Band**.
- **Dirigent:innen / Leitung** einer Band.

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
├── Typ (enum: Konzert / Leitung / Stück / Komponist / Sonstiges)
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
- **Massen-Aktionen:** „Alle offenen verwerfen" und „Alle angezeigten löschen" (bezogen auf den aktuellen Filter).
- **Übernehmen** ruft die Find-or-create-Services → keine Dubletten; Quell-URL bleibt als Provenienz erhalten.
  Übernahme-Pfade: Konzert → `KonzertErfassungService`; Leitung → `BandMitgliedschaft` (Funktion „Dirigent",
  optional Von/Bis-Jahr); Stück → `Stueck` (+ `StueckBeitrag` Komponist/Arrangeur); Komponist:in → `Person`.
  Bei **BandDomain**-Funden wird das Konzert immer der Quell-Band zugeordnet.

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
   (nur passende Funde). Bei BandDomain ist die Quell-Band Standard-Band, wenn keine genannt.
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

**Phase C2 – JS-Rendering, Event-Quellen & kaskadierende Crawls (offen):** Headless-Browser (Playwright);
Quelltyp **Event**; Programm-Extraktion mit Regel **„(Lokal, Datum) → ein Konzert"**; **Join** Rangliste-PDF ↔
Spielplan über Vereinsnamen; **Vereins-Link-Ernte** (z. B. `emf26.ch/vereine`) → `Band.Webseite` +
vorgeschlagene **BandDomain-Folgeaufträge**. (Deckt EMF-Spielplan/-Vereine, WMC.)

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
