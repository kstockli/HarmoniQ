# HarmoniQ – Spezifikation

> **Name:** Die App heisst **HarmoniQ** (Anzeige-Name). Geplanter Host: `harmoniq.q-no.ch`
> (Subdomain), optional später eigene Domain `harmoniq.ch`. Das interne .NET-Projekt heisst
> aus historischen Gründen weiterhin `MusicRater.Web` (nur technischer Name, kein Umbenennungsbedarf).

## 1. Projektidee

Eine Web-Applikation, auf der Fans von Blasmusik-Komponisten (Startpunkt: **John Mackey**) Musikstücke durchsuchen und eingebettete YouTube-Aufnahmen desselben Stücks nebeneinander sehen und bewerten können. Die Plattform soll auf weitere Komponisten und Bands erweiterbar sein.

**Langfrist-Vision (IMDb für Musikstücke):** Statt isolierter „Komponisten" steht eine
**Person** im Zentrum, die mehrere **Rollen** haben kann (Komponist:in, Dirigent:in,
Musikant:in). Aufnahmen (Videos) erhalten eine **Besetzungsliste** wie der Cast eines
Films: wer dirigiert, wer spielt welches **Instrument** auf welcher **Stimme**
(z. B. „1. Klarinette"). So entsteht ein vernetztes Nachschlagewerk – von der Person über
ihre Werke/Auftritte bis zur einzelnen Aufnahme. Siehe **Abschnitt 5 (Datenmodell)**.

---

## 2. Technologie-Stack

| Komponente | Wahl | Begründung |
|---|---|---|
| UI-Framework | **Blazor Server** (.NET 10 LTS) | C#-Entwickler, kein JavaScript nötig, einfacher Einstieg |
| UI-Bibliothek | **MudBlazor** | Material Design, Blazor-native Komponenten, einfach wartbar |
| Backend | Integriert in Blazor Server (SignalR) | Kein separates API-Projekt notwendig |
| Datenbank (Dev) | **SQLite** | Zero-Config, Datei-basiert |
| Datenbank (Prod) | **PostgreSQL** | Robust, kostenlos bei vielen Hostern |
| ORM | **Entity Framework Core** | Migrationen, LINQ, gut bekannt in .NET |
| Authentifizierung | **ASP.NET Core Identity + Google OAuth** | Login mit Google-Account |
| YouTube-Einbettung | YouTube IFrame API (JS Interop) | Offizielle Methode |
| Web-Scraping | **HtmlAgilityPack** oder **AngleSharp** | Einlesen von Komponisten-Webseiten |
| YouTube-Suche | **YouTube Data API v3** | Halbautomatische Video-Suche |

### Warum nicht Shared Hosting (z. B. servertown.ch)?
Shared Hosting unterstützt üblicherweise nur PHP/MySQL. Blazor Server benötigt eine laufende .NET-Laufzeitumgebung.

**Empfohlene Hoster:**
- **servertown.ch (Container)** – Schweizer Hoster, Daten bleiben in der Schweiz, Docker-Container-Support ← *Empfehlung für CH*
- **Railway.app** – einfachste Lösung, GitHub-Deployment, kostenloser Einstiegsplan
- **Render.com** – ähnlich Railway, guter Free-Tier
- **Azure App Service** – Microsofts eigene Cloud, native .NET-Unterstützung

**Dockerfile:** Visual Studio 2026 generiert es automatisch (Rechtsklick → "Docker Support hinzufügen"). Kein manueller Aufwand.

---

## 3. Benutzer-Rollen

| Rolle | Beschreibung |
|---|---|
| **Anonym** | Kann Stücke/Videos ansehen und **einmal pro Video voten** (Cookie-basiert, kein Login nötig) |
| **User (Login: lokal / Google / Microsoft)** | Zusätzlich: Video-/Mitwirkungs-Vorschläge, eigene Bewertungen verwalten, eigene Person pflegen, **Freundschaften** knüpfen |
| **Admin** | Vollzugriff: Daten erfassen, Importe starten, Vorschläge freigeben |

### Voting ohne Login
- Ein anonymer Vote wird per **Browser-Cookie** (UUID) gespeichert
- Derselbe Browser kann pro Video nur einmal voten
- Einschränkung: Wer Cookies löscht, kann erneut voten — bewusst akzeptierter Kompromiss
- Eingeloggte User überschreiben den anonymen Vote (Verknüpfung per Session)

---

## 4. Funktionale Anforderungen

### 4.1 Öffentliche Bereiche (ohne Login)

- **Startseite:** Featured Komponist / Stück des Monats, Statistiken
  - *(NEU, geplant)* **QR-Code ganz unten**: zeigt einen QR-Code auf `https://harmoniq.q-no.ch`,
    damit man die App **von Smartphone zu Smartphone** spontan weitergeben kann (App öffnen → unten
    zeigen → abscannen → installieren). Erzeugung serverseitig/clientseitig ohne Personenbezug;
    URL ist konstant, daher cachebar. Sinnvolle Ergänzung gerade in der offenen Anfangsphase
    (niederschwellige Verbreitung, „bring a friend").
- **Komponisten-Übersicht:** Liste aller erfassten Komponisten
- **Stück-Liste:** Alle Stücke eines Komponisten, filterbar/sortierbar nach Jahr, Schwierigkeit, Bewertung
- **Stück-Detailseite:**
  - Informationen zum Stück
  - Eingebettete YouTube-Videos (Liste, einzeln abspielbar)
  - Durchschnittsbewertungen pro Video mit Balken-Visualisierung
  - Eigenen Vote abgeben (anonym via Cookie)
- **Band-Übersicht:** Alle erfassten Bands mit ihren Videos
- **Komponisten-Fenster bleibt bestehen** (das reine `/komponisten`) — ändert sich durch Phase 6 nicht.
- *(geplant, Phase 6)* **Personen-Fenster** (`/personen`): Liste aller Personen (Komponist:innen,
  Dirigent:innen, Musikant:innen), **flexibel filterbar**, u. a.:
  - hat dieses **Stück** schon gespielt
  - hat schon Stücke **dieses Komponisten** gespielt
  - hat schon in **dieser Band** mitgewirkt
  - nach **Rolle** (Komponist/Dirigent/Musikant) und nach **Instrument**
  - Kombinierbar; Ergebnis verlinkt auf die Personen-Detailseite (Werke + Auftritte, IMDb-artig).

### 4.2 Bereiche mit Login (Google **oder** lokales Konto)

- Eigene Bewertungen anzeigen (Seite **„Meine Bewertungen"**, `/account/profil`)
- **Video-Vorschlag einreichen:** Button „Video vorschlagen" direkt auf der Stück-Detailseite
  → Dialog mit YouTube-Link (+ optional Band/Titel) → Vorschlag landet mit Status
  *Ausstehend* in der Review-Queue (`VorgeschlagenVon` = aktueller Benutzer)
- Vorschlagsstatus verfolgen *(geplant: Anzeige der eigenen Vorschläge im Profil)*
- *(geplant, Phase 6)* **Mitwirkungs-Vorschläge:** „Person XY dirigiert hier" / „Person Z
  spielt 1. Oboe" → Review-Queue → bei Genehmigung werden die Verknüpfungen gesetzt
- *(geplant, Phase 6)* **Richtigstellung melden:** Freitext-Hinweis auf Fehler (Video/Stück/Person/Band)

> **Hinweis Login (aktualisiert):** Implementiert sind **lokale Konten** (E-Mail/Passwort) sowie
> **Google-** und **Microsoft-Login**. E-Mail-Bestätigung ist **aktiv** (`RequireConfirmedAccount = true`);
> Login erst nach Bestätigung, externe (verifizierte) Logins werden auto-bestätigt. Mailversand in
> Produktion via **Resend HTTPS-API** (Railway blockt SMTP).

### 4.3 Admin-Bereich (`/admin`)

#### Datenpflege (manuell) — *umgesetzt*
- Komponisten erfassen / bearbeiten / löschen (`/admin/komponisten`)
- Stücke erfassen / bearbeiten / löschen (`/admin/stuecke`, mit Suche)
- Bands erfassen / bearbeiten / löschen (`/admin/bands`; beim Löschen bleibt das Video erhalten, nur die Band-Zuordnung wird gelöst)
- Videos verwalten (`/admin/videos`): Stück-Autocomplete, Band-Auswahl, **YouTube-Link-Erkennung** (volle URL → ID), **Duplikat-Prüfung**, Status setzen
- **Bewertungen verwalten** (`/admin/bewertungen`): einzelne Bewertungen bearbeiten oder löschen (z. B. Spam entfernen)

> **Video-Titel ist optional:** Wird beim Erfassen/Vorschlagen kein Titel angegeben, holt
> die App ihn automatisch über die **YouTube-oEmbed-Schnittstelle** (kein API-Key nötig).

#### Import-Assistent (halbautomatisch) — *Kern-Feature*
Dreistufiger Workflow:

```
Schritt 1: Stücke einlesen
  → URL der Komponisten-Webseite eingeben
  → App scannt Seite (HtmlAgilityPack)
  → Liste gefundener Stücke zur Kontrolle anzeigen
  → Admin wählt aus / korrigiert / bestätigt

Schritt 2: YouTube-Suche
  → Für jedes bestätigte Stück: automatische Suche via YouTube Data API
  → Vorgeschlagene Videos werden angezeigt (Thumbnail, Titel, Kanal, Dauer)
  → Admin wählt aus, welche Videos übernommen werden

Schritt 3: Speichern
  → Bestätigte Stücke + Videos werden in DB gespeichert
  → Protokoll: was wurde importiert, was übersprungen
```

#### Review-Queue (`/admin/vorschlaege`) — *umgesetzt*
- Eingehende Video-Vorschläge (Status *Ausstehend*) mit Thumbnail + Vorschlagendem anzeigen
- **Genehmigen** (→ Status *Genehmigt*, wird öffentlich sichtbar) oder **Ablehnen** (→ *Abgelehnt*)

#### Personen-/Stamm-Daten & Bewilligungen — *geplant (Phase 6)*
- **CRUD Personen** (`/admin/personen`): Personen + ihre Rollen + mögliche Instrumente
- **CRUD Instrumente & Stimmen** (`/admin/instrumente`): Instrument-Tabelle und je Instrument
  die Stimmen pflegen (inkl. Autocomplete-mit-Anlegen bei der Erfassung)
- **Mitwirkungs-Bewilligung:** vorgeschlagene `VideoMitwirkung`-Einträge (Status *Ausstehend*)
  prüfen → bei Genehmigung werden Person↔Video / Instrument / Stimme verknüpft
- **Vorschlag-Bewilligung:** die bestehende Video-Vorschlags-Review-Queue (s. o.) wird um die
  Mitwirkungs-Vorschläge erweitert (gemeinsame oder getrennte Queue)
- **Richtigstellungen bearbeiten:** Liste der `Richtigstellung`-Einträge (Status *Offen*),
  Admin kann **antworten** (`Antwort`/`AntwortAm`) und auf *Erledigt*/*Abgelehnt* setzen

---

## 5. Datenmodell (IMDb-Stil: Personen, Werke & Aufnahmen)

> **Quelle des Modells:** Die eigenen Entitäten (Stück, Band, Video, Bewertung, Person, …)
> sind als C#-Klassen in `src/HarmoniQ.Web/Data/Models/` definiert; der DbContext in
> `src/HarmoniQ.Web/Data/ApplicationDbContext.cs`. **Benutzer und Rollen** stammen
> **nicht** aus eigenem Code, sondern aus **ASP.NET Core Identity** (`IdentityDbContext`).
> Die SQLite-Tabellen `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserLogins`
> usw. enthalten daher **viele weitere, von Identity verwaltete Spalten** (z. B.
> `NormalizedEmail`, `PasswordHash`, `SecurityStamp`, `LockoutEnd`) — unten sind nur die
> für diese App relevanten Felder aufgeführt. Maßgeblich ist immer das Identity-Schema,
> nicht diese vereinfachte Darstellung.

### Konzept
Zentrale Entität ist **Person**. Eine Person kann **mehrere Rollen** haben
(Komponist:in, Dirigent:in, Musikant:in, **Zuhörer:in**) – einen separaten „Komponist"-Typ gibt es nicht.

> **Rolle „Zuhörer:in" (NEU):** Nicht jede Person macht aktiv Musik. Wer die App nur nutzt,
> um Aufnahmen zu entdecken/zu bewerten und sich mit anderen zu vernetzen, ist **Zuhörer:in**.
> Das ist v. a. der typische Start-Status von neu registrierten Konten ohne musikalische Tätigkeit
> (siehe Onboarding, Abschnitt 5 „Freundschaften & Onboarding"). Eine Person kann später zusätzlich
> Musikant:in/Dirigent:in werden – die Rollen schließen sich nicht aus.
> **Default-Sichtbarkeit Zuhörer:in = `NurInitialen`** (extern nur Initialen; voll für Freund:innen
> und Bandkolleg:innen) – konsistent mit Musikant:innen.

> ⚠️ Zwei verschiedene „Rollen"-Begriffe nicht verwechseln: **App-Benutzerrollen**
> (Anonym / User / Admin, Abschnitt 3, via ASP.NET Identity) steuern Zugriffsrechte.
> **Personen-Rollen** (Komponist / Dirigent / Musikant, hier) beschreiben die Tätigkeit
> einer Person im Musik-Kontext und haben nichts mit Login/Berechtigungen zu tun. Werke und Aufnahmen
verweisen auf Personen – ähnlich wie IMDb Filme mit Cast & Crew verknüpft:

- **Stück** ↔ Personen über **StückBeitrag** (Komponist:in / Arrangeur:in / Bearbeiter:in) — mehrere möglich.
- **Video** ↔ Personen über **VideoMitwirkung** (Besetzungsliste): wer **dirigiert**, wer **spielt** (mit Instrument & Stimme).
- **Musikant:in** hat eine Liste möglicher **Instrumente**.
- **Stimme** gehört zu einem **Instrument** (Klarinette → „1. Klarinette", „2. Klarinette", „Es-Klarinette").

### Entitäten

```
Person                              (ersetzt „Komponist")
├── Id (Guid)
├── Name (string)
├── Sichtbarkeit (enum)            ← Datenschutz-Stufe der Personendaten, siehe unten
│                                    Default: Öffentlich (Komponist/Dirigent), NurInitialen (Musikant/Zuhörer)
├── Biografie (string?)
├── BildUrl (string?)
├── Geburtsjahr (int?)
├── BenutzerId (FK → User?, UNIQUE) ← optional: „das bin ich"-Verknüpfung zum eingeloggten Konto
├── Rollen [n] → PersonRolle        ← Komponist / Dirigent / Musikant / Zuhörer
├── Instrumente [n:m] → Instrument  ← nur relevant für Musikant:innen (PersonInstrument)
├── Links [1:n] → PersonLink        ← mehrere Links statt einzelner Webseite
├── StückBeiträge [1:n]
└── VideoMitwirkungen [1:n]

Sichtbarkeit (enum)                 (steuert, wie viel von einer Person öffentlich gezeigt wird)
├── Öffentlich       ← voller Name sichtbar (Default für Komponist:in / Dirigent:in)
├── NurInitialen     ← nur Initialen zeigen, z. B. „K. S." (Default für Musikant:in)
└── NichtBekannt     ← anonym, Anzeige als „?" (z. B. unbekannte/ungenannte Mitwirkende)

PersonLink                          (Detail-Tabelle: beliebig viele Links je Person)
├── Id (Guid)
├── PersonId (FK)
├── Url (string)                   ← bei EMail die Adresse, bei Mobile die Nummer
└── Typ (enum: Webseite / Instagram / X / Facebook / YouTube / EMail / Mobile / Sonstige)

> **E-Mail-Sync (UMGESETZT):** Ist die Person mit einem Konto verknüpft (`BenutzerId` gesetzt),
> wird der `EMail`-Link automatisch mit der Konto-E-Mail synchronisiert (`PersonLinkSync`):
> bei Genehmigung der Verknüpfung, bei E-Mail-Änderung des Kontos und beim Öffnen von
> `/account/person`. In „Meine Person" ist die E-Mail daher schreibgeschützt; `Mobile` ist frei
> editierbar. Anzeige: `EMail` als `mailto:`, `Mobile` als `tel:`-Link.

PersonRolle                         (welche Rollen kann die Person grundsätzlich?)
├── PersonId (FK)
├── Rolle (enum: Komponist / Dirigent / Musikant / Zuhörer)
└── PK (PersonId, Rolle)

Instrument                          (Nachschlage-Tabelle)
├── Id (Guid)
├── Name (string, eindeutig)        ← z. B. "Klarinette", "Trompete", "Schlagzeug"
├── Stimmen [1:n] → Stimme
└── Familie (enum?: Holz/Blech/Schlagwerk/… – optional)

Stimme                              (Nachschlage-Tabelle, gehört zu einem Instrument)
├── Id (Guid)
├── InstrumentId (FK)
├── Bezeichnung (string)            ← z. B. "1. Klarinette", "2. Klarinette", "Solo", "Es-Klarinette"
└── PK/Unique (InstrumentId, Bezeichnung)

PersonInstrument                    (n:m – mögliche Instrumente einer Musikant:in)
├── PersonId (FK)
├── InstrumentId (FK)
└── PK (PersonId, InstrumentId)

Band
├── Id (Guid)
├── Name (string)
├── Land (string?)
├── Webseite (string?)
├── BildUrl (string?)               ← NEU: Band-Logo/Foto (optional)
├── Videos [1:n]
└── Mitgliedschaften [1:n] → BandMitgliedschaft

Stück                               (kein KomponistId mehr → über StückBeitrag)
├── Id (Guid)
├── Titel (string)
├── Jahr (int?)
├── Schwierigkeitsgrad (enum: Leicht / Mittel / Schwer / SehrSchwer / Unbekannt)
├── Besetzung (string?)            ← z. B. "Blasorchester"
├── Beschreibung (string?)
├── OriginalUrl (string?)          ← Quell-URL beim Import
├── Beiträge [1:n] → StückBeitrag  ← Komponist:in / Arrangeur:in / Bearbeiter:in
└── Videos [1:n]

StückBeitrag                        (wer hat zum Stück beigetragen – mehrere möglich)
├── Id (Guid)
├── StückId (FK)
├── PersonId (FK)
└── Rolle (enum: Komponist / Arrangeur / Bearbeiter)

Video
├── Id (Guid)
├── StückId (FK)
├── BandId (FK?)                   ← nullable, falls Band unbekannt
├── KonzertId (FK?)               ← NEU, nullable: optionaler Verweis auf das Konzert/den Auftritt
├── YouTubeVideoId (string)        ← nur die 11-stellige ID (aus URL extrahiert)
├── Titel (string)                 ← optional bei Eingabe; sonst autom. via YouTube-oEmbed
├── AufnahmeDatum (DateOnly?)
├── Ort (string?)                  ← optional, Aufnahme-Ort (z. B. "KKL Luzern")
├── Anlass (string?)               ← optional (z. B. "WMC Kerkrade 2022")
├── ErstelltAm (DateTime)          ← Erfassungszeitpunkt (für "zuletzt hinzugefügt")
├── Status (enum: Ausstehend / Genehmigt / Abgelehnt)
├── VorgeschlagenVon (FK → User?)  ← null = Admin erfasst
├── Bewertungen [1:n]
└── Mitwirkungen [1:n] → VideoMitwirkung   ← „Cast & Crew" der Aufnahme

VideoMitwirkung                     (eine Zeile der Besetzungsliste)
├── Id (Guid)
├── VideoId (FK)
├── PersonId (FK)
├── Rolle (enum: Dirigent / Musikant)
├── InstrumentId (FK?)             ← bei Musikant:in; bei Dirigent:in null
├── StimmeId (FK?)                 ← bei Musikant:in optional (z. B. "1. Klarinette")
├── Anmerkung (string?)            ← Freitext, z. B. "Solo", "als Gast"
├── Status (enum: Ausstehend / Genehmigt / Abgelehnt)   ← für User-Vorschläge
└── VorgeschlagenVon (FK → User?)  ← null = vom Admin erfasst

BandMitgliedschaft                  (Person ↔ Band über die Zeit, alles optional außer Verweise) — UMGESETZT
├── Id (Guid)
├── PersonId (FK)
├── BandId (FK)
├── InstrumentId (FK?)             ← optional (welches Instrument die Person in dieser Band spielt)
├── VonJahr (int?)                 ← optional
├── BisJahr (int?)                 ← optional (null = bis heute / unbekannt)
├── Funktion (string?)             ← Freitext, z. B. "Chefdirigent", "Präsident", "Registerleitung"
└── IstAktiv [NotMapped]           ← berechnet: BisJahr == null

> Hinweis: Statt eines Rolle-Enums (Dirigent/Musikant) wird hier ein freies `Funktion`-Feld
> verwendet – die grundsätzliche Rolle einer Person steckt bereits in `PersonRolle`, und
> Band-Funktionen sind vielfältiger (Vorstand, Registerleitung …). `StimmeId` wurde weggelassen
> (für eine Mitgliedschaft i. d. R. nicht relevant; Stimmen gehören zur konkreten VideoMitwirkung).

BandbeitrittAntrag                  (Vorschlag „Band beitreten" – UMGESETZT)
├── Id (Guid)
├── PersonId (FK)
├── BandId (FK)
├── InstrumentId (FK?)             ← optional
├── BeantragtVon (FK → User?)      ← antragstellendes Konto
├── Status (enum: Offen / Genehmigt / Abgelehnt)   ← (PersonAnspruchStatus)
├── ErstelltAm (DateTime)
└── EntschiedenAm (DateTime?)

> **Warum ein Antrag?** Eine Bandmitgliedschaft macht (über die viewer-abhängige Sichtbarkeit)
> alle Mitglieder dieser Band für die Person voll sichtbar. Damit sich niemand selbst in
> beliebige Bands einträgt und so alle Personen „aufdeckt", darf eine verknüpfte Person über
> `/account/person` eine Mitgliedschaft nur **vorschlagen** (`BandbeitrittAntrag`, Offen);
> ein:e **Admin** bestätigt sie unter `/admin/bandantraege` (→ erzeugt `BandMitgliedschaft`).
> Admins können auf der Personenseite Bands auch **direkt** hinzufügen (ohne Antrag).

Freundschaft                        (NEU – gegenseitige Verbindung zweier Personen)
├── Id (Guid)
├── AnfragerPersonId (FK → Person)  ← wer die Anfrage gestellt hat (verknüpfte Person des Kontos)
├── EmpfaengerPersonId (FK → Person)← wer angefragt wird
├── Status (enum: Offen / Bestätigt / Abgelehnt)
├── ErstelltAm (DateTime)
└── EntschiedenAm (DateTime?)
CONSTRAINT: UNIQUE (AnfragerPersonId, EmpfaengerPersonId)
CONSTRAINT: AnfragerPersonId <> EmpfaengerPersonId

> **Wirkung (analog Bandkolleg:innen):** Ist eine Freundschaft **Bestätigt**, sehen die
> beiden Personen einander **immer voll** – Name *und* Bild –, unabhängig von der
> Sichtbarkeits-Einstellung der Gegenseite (siehe „Viewer-abhängige Sichtbarkeit"). Eine
> Anfrage setzt eine **verknüpfte Person** beim Anfrager voraus (man knüpft als „ich" an).
> Die Beziehung ist **symmetrisch**: für die Sichtbarkeits-Prüfung zählt jedes bestätigte
> Paar in beide Richtungen. Eine abgelehnte Anfrage kann später erneut gestellt werden
> (alter Eintrag wird überschrieben/neu angelegt).

Konzert                             (NEU – ein Auftritt/Event, an dem eine oder mehrere Bands mitwirken)
├── Id (Guid)
├── Datum (DateOnly)               ← PFLICHT
├── Name (string?)                 ← optional, z. B. "Jahreskonzert 2025", "Eidg. Musikfest"
├── Ort (string?)                  ← optional, Standort/Lokal, z. B. "KKL Luzern"
├── Beschreibung (string?)         ← optional
├── BildUrl (string?)              ← NEU: Plakat/Foto des Konzerts (optional)
├── Bands [n:m] → KonzertBand      ← teilnehmende Bands (eine bis mehrere)
├── Programm [1:n] → KonzertStueck ← gespielte Stücke (welche Band welches Stück) — NEU
├── Mitwirkende [1:n] → KonzertPerson ← beteiligte/anwesende Personen mit Rolle — NEU
└── Videos [1:n]                   ← Videos, die auf dieses Konzert verweisen (Video.KonzertId)

> **Namensentscheid:** Entität heißt **`Konzert`** (klar und gebräuchlich). „Auftritt"/„Event"
> wäre breiter (Umzug, Probe …); falls künftig nötig, lässt sich ein optionales `Typ`-Enum
> (Konzert / Wertungsspiel / Sonstiges) ergänzen, ohne das Modell umzubauen. Vorerst bewusst
> schlank: nur Datum (Pflicht) + optional Name/Ort.

KonzertBand                         (n:m – welche Bands beim Konzert mitwirken)
├── KonzertId (FK)
├── BandId (FK)
└── PK (KonzertId, BandId)

KonzertStueck                       (NEU – Programm: welches Stück wurde von welcher Band gespielt)
├── Id (Guid)                       ← Surrogat-PK (ein Stück kann mehrfach vorkommen, z. B. zwei Bands)
├── KonzertId (FK)
├── StueckId (FK)
├── BandId (FK?)                    ← welche Band das Stück spielte (optional, falls unbekannt)
├── Reihenfolge (int?)              ← optionale Position im Programm
└── CONSTRAINT: UNIQUE (KonzertId, StueckId, BandId)

> **Bezug zu Video:** `KonzertStueck` ist das **Programm** (was gespielt wurde) – unabhängig davon,
> ob eine Aufnahme existiert. Ein `Video` (mit `KonzertId`, `StueckId`, `BandId`) ist die konkrete
> Aufnahme eines Programmpunkts; nicht jeder Programmpunkt hat ein Video, und das Programm bleibt
> auch ohne Videos erfasst.

KonzertPerson                       (NEU – n:m Person ↔ Konzert mit Rolle)
├── Id (Guid)
├── KonzertId (FK)
├── PersonId (FK)
├── Rolle (enum PersonRolleTyp: Komponist / Dirigent / Musikant / Zuhörer)
├── BandId (FK?)                    ← optional: mit welcher Band die Person auftrat
└── CONSTRAINT: UNIQUE (KonzertId, PersonId, Rolle)

> **Rolle pro Konzert:** Die Rolle ist **kontextabhängig** – dieselbe Person kann an einem Konzert
> als Musikant:in auftreten, an einem anderen als Zuhörer:in dabei sein. Bei der Erfassung wird
> **die übliche Rolle der Person vorgeschlagen** (ihre primäre `PersonRolle`), ist aber überschreibbar
> (z. B. Musiker:in geht als Zuhörer:in hin). Durch `UNIQUE (KonzertId, PersonId, Rolle)` kann eine
> Person am selben Konzert auch in mehreren Rollen geführt werden (z. B. Dirigent:in *und* Komponist:in).

> **Beispiel (wie vom User beschrieben):** Konzert „Jahreskonzert 2025" mit **drei** Bands
> (3 × `KonzertBand`). Band A hat **kein** Video an diesem Konzert; Band B hat **mehrere**
> Videos (je ein Stück); Band C hat **genau ein** Video. Die Videos hängen über `Video.KonzertId`
> am Konzert und über `Video.BandId` an ihrer Band – beide Bezüge sind unabhängig, daher sind
> alle drei Fälle abbildbar. Eine Konzert-Detailseite gruppiert die Videos nach Band; Bands ohne
> Video erscheinen trotzdem als Teilnehmerinnen.

> **Niederschwellige Erfassung (entschieden):** Im **Video-Dialog** wird das Konzert per
> **Autocomplete-mit-Anlegen** gewählt – bestehendes Konzert auswählen *oder* per **Datum (+ optional
> Name/Ort)** neu anlegen (analog Instrument/Stimme). Kein separater Admin-Schritt nötig; Konzerte
> entstehen organisch beim Video-Erfassen.
>
> **Automatische Band-Zuordnung (entschieden):** Verweist ein Video auf ein Konzert, wird die **Band
> des Videos automatisch** als `KonzertBand`-Teilnehmerin eingetragen (idempotent, keine Dublette).
> Zusätzliche Bands ohne Video können im `/admin/konzerte`-CRUD manuell ergänzt werden.
>
> **Konzert-Erfassungs-Wizard (NEU, geplant):** Ein eigenes GUI (`/admin/konzerte/erfassen`), mit dem
> man ein **ganzes Konzert in einem Rutsch** erfasst – fehlende Stammdaten werden dabei **bei Bedarf
> angelegt** (Find-or-create, keine Dubletten):
> 1. **Konzert-Kopf:** Datum (Pflicht), optional Name/Ort/Bild.
> 2. **Programm:** Liste von Zeilen, je Zeile **Stück** (Autocomplete-mit-Anlegen) + **Komponist:in**
>    (Autocomplete-mit-Anlegen, wird beim neuen Stück als `StueckBeitrag` gesetzt) + **Band**
>    (Autocomplete-mit-Anlegen) → erzeugt je Zeile einen `KonzertStueck`-Eintrag; die genannten Bands
>    werden zugleich als `KonzertBand` geführt.
> 3. **Mitwirkende (optional):** Personen + Rolle (übliche Rolle vorgeschlagen, überschreibbar; z. B.
>    „Zuhörer:in") → `KonzertPerson`.
> Speichern legt alles transaktional an (Konzert, neue Stücke/Komponist:innen/Bands, KonzertStueck,
> KonzertBand, KonzertPerson). Videos können später wie gehabt am Konzert ergänzt werden.

Richtigstellung                     (Freitext-Hinweis/Korrektur von eingeloggten Usern)
├── Id (Guid)
├── BetrifftTyp (enum: Video / Stück / Person / Band / Konzert)
├── BetrifftId (Guid)              ← Verweis auf das gemeinte Objekt
├── Text (string)                  ← die eigentliche Richtigstellung (Freitext)
├── EingereichtVon (FK → User)
├── ErstelltAm (DateTime)
├── Status (enum: Offen / Erledigt / Abgelehnt)
├── Antwort (string?)              ← Antwort/Notiz des Admins
└── AntwortAm (DateTime?)          ← wann der Admin geantwortet hat

Bewertung
├── Id (Guid)
├── VideoId (FK)
├── BenutzerId (FK?)              ← null = anonymer Vote
├── AnonymerCookieId (string?)    ← UUID aus Cookie, wenn nicht eingeloggt
├── GesamtEindruck (int 1–5)
├── Präzision (int 1–5)
├── Musikalität (int 1–5)
├── AkustischeQualität (int 1–5)
├── VideoQualität (int 1–5)
├── Kommentar (string?)
└── ErstelltAm (DateTime)

CONSTRAINT: UNIQUE (VideoId, BenutzerId)        -- ein Vote pro User+Video
CONSTRAINT: UNIQUE (VideoId, AnonymerCookieId)  -- ein Vote pro Cookie+Video

Benutzer                          ← ASP.NET Core Identity (AspNetUsers)
├── Id (string)
├── Email (string)                ← Login erst nach E-Mail-Bestätigung möglich
├── EmailConfirmed (bool)
├── (externe Logins Google/Microsoft → AspNetUserLogins; verifiziert → auto-bestätigt)
└── Bewertungen [1:n]

Rollen                            ← ASP.NET Core Identity (AspNetRoles / AspNetUserRoles)
├── "Admin"  → Vollzugriff auf /admin
└── (Standard-Benutzer haben keine Rolle)
   Zuweisung: Beim App-Start werden die in appsettings unter "Admin:Emails"
   konfigurierten Benutzer automatisch der Rolle "Admin" zugeordnet (AdminInitializer);
   zusätzlich befördert eine ClaimsTransformation Admin-Mails sofort beim Login.
```

> **Prinzip „möglichst viel optional":** Außer den Pflicht-Verknüpfungen (Fremdschlüssel)
> und Namen/Titeln ist praktisch jedes Feld optional. Eine Aufnahme darf ganz ohne
> Besetzung/Ort/Datum existieren; eine Mitwirkung darf nur „Person + Rolle" ohne
> Instrument/Stimme sein; eine Band-Mitgliedschaft braucht keine Jahresangaben.

### Datenschutz – Sichtbarkeit von Personen
Das Feld `Person.Sichtbarkeit` steuert die öffentliche Anzeige des Namens:
- **Öffentlich:** voller Name (Default für Komponist:in / Dirigent:in – meist öffentliche Personen)
- **NurInitialen:** nur Initialen, z. B. „K. S." (Default für Musikant:in – schützt Orchester-Mitglieder)
- **NichtBekannt:** Anzeige als „?" (unbekannte/ungenannte Mitwirkende)

Die Anzeige-Logik (voller Name / Initialen / „?") wird zentral angewendet (`PersonenSicht`),
wo immer ein Personenname dargestellt wird. Admins sehen intern stets den vollen Namen.

**Viewer-abhängige Sichtbarkeit (UMGESETZT):** Ist ein:e Benutzer:in eingeloggt und mit einer
Person verknüpft, werden **Bandkolleg:innen** (Personen, die mit der eigenen Person in mind. einer
Band sind) **immer voll** angezeigt – Name *und* Bild –, unabhängig von deren Sichtbarkeits-Einstellung.
Für alle anderen Betrachter gilt die persönliche Einstellung. Zusätzlich:
- **Bilder** erscheinen nur bei effektiver Sichtbarkeit „Öffentlich" (Außenstehende sehen höchstens Initialen, kein Foto).
- In der **Personen-Übersicht** (`/personen`) werden effektiv „NichtBekannt"-Personen **herausgefiltert**.
- **Bestätigte Freundschaften** (NEU) wirken wie Bandkolleg:innen: befreundete Personen sehen
  einander **immer voll** (Name + Bild), unabhängig von der Sichtbarkeits-Einstellung. Die zentrale
  Sichtprüfung (`PersonenSicht`) prüft also: Admin → bandkollegial → befreundet → sonst Einstellung.

### Freundschaften & Onboarding *(NEU – geplant)*
**Freundschaften.** Zwei Personen können sich verbinden (Entität `Freundschaft`): ein:e eingeloggte:r
Benutzer:in mit verknüpfter Person stellt eine **Anfrage** an eine andere Person; diese **bestätigt**
oder **lehnt ab** (gegenseitig). Erst nach Bestätigung sehen beide einander voll (s. o.). UI:
- Button **„Befreunden"** auf der Personen-Detailseite (sofern eigene Person verknüpft, nicht man selbst,
  noch keine offene/bestätigte Verbindung). Sonst Status-Chip („Anfrage gesendet" / „Befreundet").
- Seite **`/account/freunde`**: eingehende Anfragen bestätigen/ablehnen, eigene Freundesliste, gesendete Anfragen.

**Onboarding für Konten ohne verknüpfte Person.** Wer sich einloggt, aber (noch) keine Person verknüpft
hat, wird **niederschwellig aufgefordert**, sich zu erfassen – ein Hinweis-Banner/Dialog führt zu einem
kurzen Assistenten:
1. **Bestehende Person finden:** Wählt man eine **Band** und tippt den **Namen**, werden **bereits
   vorhandene Personen** dieser Band vorgeschlagen (Autocomplete) → „Das bin ich" (→ `PersonAnspruch`,
   Admin-Bestätigung wie gehabt).
2. **Sonst neu anlegen:** Findet sich nichts, legt man eine neue Person an (Default-Rolle **Zuhörer:in**;
   Musikant:in/Band optional ergänzbar). Bei verknüpftem Konto wird die E-Mail synchronisiert.

> **Bewusst offene Anfangsphase:** Damit die App früh „lebt", wird bewusst **wenig erzwungen** –
> z. B. dürfen Freundschaftsanfragen ohne Admin-Freigabe laufen, und das Onboarding ist optional/
> überspringbar. Die Anti-Impersonation-Bestätigung bleibt nur dort, wo es um das **Aufdecken**
> geschützter Personendaten geht (Person-Verknüpfung, Band-Beitritt).

**Aktivitäts-Feed (NEU – der Sozial-Hebel).** Auf der Startseite (für eingeloggte Nutzer:innen) bzw.
unter `/account/freunde` ein **Feed** der jüngsten Aktivitäten der eigenen **Freund:innen** und
**Bandkolleg:innen**: „X hat Video Y **bewertet**", „X hat Video Y **hinzugefügt/vorgeschlagen**",
„X ist jetzt mit Z **befreundet**", „X **wirkt mit** in Video Y".

> **Umsetzung mit eigener Tabelle `Aktivitaet` (entschieden).** Statt die Ereignisse bei jeder
> Anzeige aus vielen Tabellen zu unionen, wird **append-only** eine `Aktivitaet`-Zeile geschrieben,
> sobald ein Ereignis passiert. Begründung: günstig lesbar (Index auf `Zeitpunkt`), Zustand pro
> Event möglich (gelesen/ungelesen, Push, Zusammenfassen/Throttling), Historie bleibt stabil auch
> wenn die Quelle (z. B. Bewertung) später gelöscht wird, und neue Event-Typen lassen sich einfach
> anhängen. Der Feed selbst ist dann ein simpler, indizierter `WHERE AkteurPersonId IN (Freunde+Bandkollegen)
> ORDER BY Zeitpunkt DESC`-Query. Sichtbarkeit wird respektiert (nur Akteur:innen, die der Betrachter
> sehen darf). **Backfill** bestehender Daten beim Einführen einmalig.

Aktivitaet                          (NEU – append-only Feed-Ereignis; System-Ereignis ODER eigener Beitrag)
├── Id (Guid)
├── AkteurPersonId (FK → Person)    ← wer die Aktivität ausgelöst / den Beitrag geschrieben hat
├── Typ (enum: Beitrag / BewertungAbgegeben / VideoHinzugefuegt / FreundschaftBestaetigt / MitwirkungHinzugefuegt)
├── Text (string?)                  ← NEU: Freitext; PFLICHT bei Typ=Beitrag, sonst optionale Notiz
├── ZielTyp (enum?: Video / Person / Band / Konzert / Stück)  ← bei Typ=Beitrag i. d. R. null
├── ZielId (Guid?)                  ← lose Referenz auf das betroffene Objekt (null bei reinem Beitrag)
├── NebenPersonId (FK → Person?)    ← optional, z. B. die neue Freundin bei FreundschaftBestaetigt
└── Zeitpunkt (DateTime)            ← INDEX (Feed nach Datum absteigend)

> **Eigene Beiträge (`Typ=Beitrag`):** Eingeloggte Nutzer:innen mit verknüpfter Person können über
> ein Eingabefeld im Feed (Startseite / `/account/freunde`) **selbst etwas an ihre Freund:innen &
> Bandkolleg:innen schreiben** – derselbe `Aktivitaet`-Datensatz, nur mit `Text` statt System-Ereignis.
> Sichtbar für dieselbe Gruppe wie der übrige Feed (Freunde + Bandkollegen). `ZielTyp/ZielId` bleiben
> i. d. R. leer, können aber optional auf ein Objekt verweisen (z. B. ein Beitrag *zu* einem Video).
> Bearbeiten/Löschen des eigenen Beitrags möglich; Admin kann moderieren. *(Antworten/Kommentare auf
> Beiträge sind eine mögliche spätere Erweiterung und vorerst nicht modelliert.)*

### Selbst-Verknüpfung „das bin ich" (Person ↔ Benutzerkonto)
`Person.BenutzerId` ist eine **optionale, eindeutige (UNIQUE)** Referenz auf ein eingeloggtes
Konto: eine Person ist höchstens einem Benutzer zugeordnet und umgekehrt. Hat ein:e
eingeloggte:r Benutzer:in sich mit „ihrer" Person verknüpft, darf sie/er **die eigenen
Personendaten selbst pflegen** (Bio, Links, Sichtbarkeit, Band-Mitgliedschaften, Instrumente
usw.) – ohne Admin.
> **Anti-Impersonation:** Damit niemand sich als fremde (z. B. prominente) Person ausgibt,
> wird die Verknüpfung **vom Admin bestätigt** (Verknüpfungs-Antrag → Bewilligung),
> analog zu den Mitwirkungs-Vorschlägen.

**UMGESETZT:** Entität `PersonAnspruch` (PersonId, BenutzerId, Begruendung?, Status
[Offen/Genehmigt/Abgelehnt], ErstelltAm, EntschiedenAm?). Auf der Personen-Detailseite gibt es
für eingeloggte Benutzer:innen den Button **„Das bin ich"** (Dialog mit optionaler Begründung),
sofern die Person noch nicht verknüpft ist und kein offener Antrag besteht; sonst Status-Chip
(„in Prüfung" / „Mit deinem Konto verknüpft"). Admin-Queue `/admin/verknuepfungen`: Genehmigen
(setzt `Person.BenutzerId`, prüft UNIQUE: Konto/Person noch frei) oder Ablehnen.
Nach Verknüpfung: **Selbst-Pflege** der eigenen Personendaten unter `/account/person`
(Name, Bio, Bild-URL, Geburtsjahr, Sichtbarkeit, Links Webseite/Instagram/X/Facebook/YouTube) –
verlinkt auch über „Meine Daten bearbeiten" auf der eigenen Personenseite. **UMGESETZT.**

### Freitext → Tabelle (Erfassung von Instrument/Stimme)
Bei der Erfassung einer Mitwirkung werden Instrument und Stimme über **Autocomplete mit
Anlege-Funktion** gewählt: Existiert der eingegebene Wert, wird er referenziert; ist es ein
**neuer Freitext**, wird automatisch ein neuer Eintrag in der jeweiligen Tabelle
(`Instrument` bzw. `Stimme`, letztere am gewählten Instrument) angelegt und dann referenziert.
So bleibt die Tabelle sauber/normalisiert und wächst organisch mit der Nutzung.

### Beziehungen kompakt
- Person `1—n` PersonRolle · Person `n—m` Instrument (PersonInstrument)
- Person `1—n` PersonLink (Webseite/Instagram/X/Facebook/…)
- Person `0..1—1` User (optionale, eindeutige „das bin ich"-Verknüpfung)
- Instrument `1—n` Stimme
- Stück `1—n` StückBeitrag `n—1` Person
- Video `1—n` VideoMitwirkung `n—1` Person; VideoMitwirkung `n—1` Instrument/Stimme (optional)
- Person `1—n` BandMitgliedschaft `n—1` Band (Person ↔ Band über die Zeit)
- Person `n—m` Person über **Freundschaft** (gegenseitig, mit Status)
- Konzert `n—m` Band über **KonzertBand**; Konzert `1—n` Video (`Video.KonzertId`, optional)
- Konzert `n—m` Stück über **KonzertStueck** (Programm, mit optionaler Band je Programmpunkt)
- Konzert `n—m` Person über **KonzertPerson** (mit Rolle PersonRolleTyp, optionaler Band)
- Person `1—n` Aktivitaet (Akteur); Aktivitaet verweist lose (ZielTyp + ZielId) auf das Objekt
- Richtigstellung verweist lose (Typ + Id) auf Video/Stück/Person/Band/Konzert

### Community-Beiträge: Ergänzungen & Richtigstellungen *(geplant)*
Eingeloggte User können – analog zu den Video-Vorschlägen – auch **Mitwirkungen vorschlagen**:

- *„In diesem Video dirigiert **Person XY**."* → neue `VideoMitwirkung` (Dirigent) mit Status *Ausstehend*.
- *„Hier spielt **Person Z** die **1. Oboe**."* → neue `VideoMitwirkung` (Musikant + Instrument/Stimme) mit Status *Ausstehend*.

Ablauf: Vorschlag landet mit `Status = Ausstehend` und `VorgeschlagenVon` in der **Review-Queue**.
Beim **Genehmigen** setzt der Admin den Status auf *Genehmigt* – damit werden die Verknüpfungen
(Person↔Video, Instrument, Stimme) aktiv und öffentlich sichtbar. *Ablehnen* = *Abgelehnt*.
Existiert die genannte Person/Instrument/Stimme noch nicht, kann sie im Review per
Autocomplete-mit-Anlegen erzeugt werden.

**Richtigstellungen** sind bewusst **textbasiert** (wenig GUI): Auf jeder Detailseite
(Video/Stück/Person/Band) gibt es „Fehler melden / Richtigstellung" → Freitextfeld →
Eintrag in `Richtigstellung` (Status *Offen*). Der Admin liest, korrigiert manuell und
markiert *Erledigt*/*Abgelehnt* (optional mit Notiz). Keine strukturierte Bearbeitung nötig.

### Migration vom aktuellen Modell *(bei Umsetzung)*
1. Tabelle **Person** anlegen; jeden bestehenden **Komponist** als Person (Rolle „Komponist",
   `Sichtbarkeit = Öffentlich`) übernehmen. Vorhandene `Komponist.Webseite` → als **PersonLink**
   (Typ „Webseite") übernehmen; `Komponist.BildUrl`/`Biografie` → Person übernehmen.
2. Für jedes Stück einen **StückBeitrag** (Komponist) auf die migrierte Person setzen; `Stück.KomponistId` entfernen.
3. Neue Tabellen: `PersonRolle`, `PersonLink`, `Instrument`, `Stimme`, `PersonInstrument`,
   `StückBeitrag`, `VideoMitwirkung`, `BandMitgliedschaft`, `Richtigstellung`.
4. `Person.BenutzerId` (UNIQUE, optional) als FK auf `AspNetUsers` ergänzen.
5. Optional: Grund-Stamm an Instrumenten/Stimmen seeden (Blasorchester-typisch).
6. Admin-CRUD + Erfassungs-UI (Autocomplete-mit-Anlegen) ergänzen; Detailseiten „Person"
   (Filmografie-artig: Werke + Auftritte) bauen; zentrale Namens-Anzeige gemäß `Sichtbarkeit`.

---

## 6. UI-Seitenübersicht

```
/                             → Startseite (klickbare Statistik-Kacheln)
/komponisten                  → Komponisten-Liste
/komponisten/{id}             → Komponist-Detail + Stückliste (Filter: Suche, Besetzung, Schwierigkeit, Mit-Video)
/stuecke                      → Gesamtliste aller Stücke (Filter inkl. Band & Komponist; ?band=…, ?komponist=…). Default-Filter: **Mit Video**; sortiert nach Jahr absteigend (neueste zuerst)
/stuecke/{id}                 → Stück-Detail + Videos + Bewertungen + Voting + „Video vorschlagen"
/videos                       → Gesamtliste aller Videos (Filter: Band, Komponist)
/bands                        → Band-Übersicht
/bands/{id}                   → Band-Detail (Aufnahmen + gespielte Stücke + Konzerte der Band)
/konzerte                     → (NEU) Konzert-Übersicht (Liste, nach Datum sortiert)
/konzerte/{id}                → (NEU) Konzert-Detail: Datum/Name/Ort, teilnehmende Bands,
                                 Videos je Band gruppiert (Bands ohne Video trotzdem gelistet)
/account/login                → Login (lokal + Google + Microsoft)
/account/register             → Registrierung (lokales Konto)
/account/profil               → Meine Bewertungen
/account/person               → Eigene Personendaten pflegen
/account/freunde              → (NEU) Freundschaften: Anfragen, eigene Freundesliste
/admin                        → Admin-Dashboard
/admin/komponisten            → CRUD Komponisten
/admin/stuecke                → CRUD Stücke
/admin/bands                  → CRUD Bands
/admin/videos                 → CRUD Videos
/admin/bewertungen            → Bewertungen verwalten (bearbeiten / löschen)
/admin/vorschlaege            → Review-Queue für User-Vorschläge
/admin/import                 → Import-Assistent (3-Schritt-Wizard)
/admin/konzerte               → (NEU) CRUD Konzerte (Datum/Name/Ort/Bild, Bands zuordnen)
/admin/konzerte/erfassen      → (NEU) Konzert-Erfassungs-Wizard (Programm + Mitwirkende, Find-or-create)

— geplant (Phase 6, Personen-/Rollen-Modell) —
/personen                     → Personen-Liste (Komponist:innen, Dirigent:innen, Musikant:innen)
/personen/{id}                → Personen-Detail (Werke + Auftritte, IMDb-artig)
/admin/personen               → CRUD Personen + Rollen + Instrumente
/admin/instrumente            → Verwaltung Instrumente & Stimmen
(/komponisten wird langfristig durch /personen abgelöst)
```
> Zugriffsschutz: alle `/admin/*`-Seiten erfordern die Rolle **Admin** (`[Authorize(Roles="Admin")]`);
> anonyme Zugriffe werden auf die Login-Seite umgeleitet.

---

## 7. Nicht-funktionale Anforderungen

- **Sprache:** Deutsch (UI), Englisch (Code)
- **Responsive:** Mobile-First via MudBlazor
- **Skalierbarkeit:** Datenmodell unterstützt beliebig viele Komponisten von Beginn an
- **Datenschutz:** Bewertungs-Verknüpfung zu Google-Account nur für Admin sichtbar; Cookie-ID nicht personenbezogen

---

## 8. Entwicklungs-Reihenfolge & Umsetzungsstand

### Phase 1 – Grundgerüst ✅ *umgesetzt*
1. Projekt-Setup: Blazor Web App (.NET 10) + MudBlazor + EF Core + SQLite
2. Datenmodell + EF-Migrationen
3. Seed-Daten: John Mackey (alle Werke) + verifizierte Schweizer Aufnahmen

### Phase 2 – Kern-Anzeige ✅ *umgesetzt*
4. Komponisten-, Stück-, Band- und Video-Listen (mit Filtern & Sortierung)
5. Stück-Detailseite mit YouTube-Einbettung (IFrame)
6. Bewertungsanzeige (Durchschnitt als Sterne + Detail-Balken)

### Phase 3 – Voting ✅ *umgesetzt*
7. Anonymes Voting via localStorage-ID (5 Kriterien, ein Vote pro Browser/Video)
8. Login (lokales Konto **und** Google) + Identity-Rollen + SMTP-Mailversand
9. Login-basiertes Voting (Vote an Benutzer statt Cookie gebunden)

### Phase 4 – Admin & Community ✅ *umgesetzt*
10. Admin-Rollenverwaltung + Zugriffsschutz (`/admin`, `AdminInitializer`)
11. Admin-CRUD für alle Entitäten (Komponist, Stück, Band, Video) + Bewertungsverwaltung
12. User-Vorschläge (Dialog auf Stück-Seite) + Review-Queue
> *Offen/optional:* Anzeige der eigenen Vorschläge inkl. Status im Profil.

### Phase 5 – Import-Assistent ✅ *umgesetzt* (`/admin/import`)
13. **Schritt 1 – Stücke sammeln:** Komponist wählen/neu; Webseite scannen (HtmlAgilityPack
    extrahiert Kandidaten aus Links/Listen/Überschriften) **oder** Titel manuell einfügen.
    Da Komponisten-Webseiten **unterschiedlich aufgebaut** sind, werden bewusst viele
    Kandidaten gesammelt und der **Admin kuratiert** (auswählen, Titel korrigieren).
    Beim Übernehmen interpretiert der `StueckParser` jede Zeile: **Jahr** (19xx/20xx) und
    **Schwierigkeit** (deutsch *oder* englisch, z. B. „leicht"/„easy", „sehr schwer"/„really hard")
    werden extrahiert und der Titel bereinigt. Besetzung wird bewusst nicht geraten.
14. **Schritt 2 – Videos finden:** pro Stück Auto-Suche via **YouTube Data API** (falls
    `YouTube:ApiKey` konfiguriert); Suchbegriff = **Komponist + Titel** (ohne Jahr/Schwierigkeit/
    Besetzung). Treffer werden mit **Checkbox** aus-/abgewählt. Ohne API-Key: manueller
    YouTube-Link je Stück.
15. **Schritt 3 – Prüfen & Speichern:** Mit **Duplikat-Vermeidung** (Komponist per Name,
    Stück per Titel+Komponist, Video per YouTube-ID); Band wird aus dem Video-Owner
    ermittelt/angelegt. Ergebnis-Protokoll (neu/übersprungen).
> **Wichtig – Duplikat-Vermeidung:** Der Import-Assistent muss vor dem Speichern
> sicherstellen, dass keine doppelten **Komponisten**, **Stücke** oder **Videos**
> entstehen. Abgleich:
> - Komponist: anhand Name (normalisiert)
> - Stück: anhand Titel + Komponist
> - Video: anhand `YouTubeVideoId` (eindeutig)
> Bereits vorhandene Einträge werden erkannt und nur ergänzt/aktualisiert, nicht neu angelegt.

### Phase 6 – Personen- & Rollen-Modell (IMDb-Stil) ✅ *umgesetzt* — siehe Abschnitt 5
> *(vorgezogen vor das Deployment – das Datenmodell soll vor dem produktiven Launch stehen.)*

16. ✅ *(umgesetzt)* Datenmodell: `Person` (+ `PersonRolle`, `PersonLink`, `StueckBeitrag`).
    **`Komponist` vollständig durch `Person` ersetzt und entfernt** (Entity, Tabelle, `Stück.KomponistId`
    — Migration `KomponistEntfernt`). Alle Seiten lesen Komponist:innen jetzt über `StueckBeitrag`/`Person`;
    `/komponisten` listet Personen mit Rolle Komponist, `/komponisten/{id}` leitet auf `/personen/{id}`.
    ⚠️ **Hinweis Datenverlust:** Der SQLite-Spalten-Drop hat beim Tabellen-Rebuild die `Stuecke`-Zeilen
    (+ abhängige Videos/Bewertungen) nicht erhalten; die DB wurde danach **zurückgesetzt und neu geseedet**
    (John-Mackey-Katalog wiederhergestellt; manuelle Test-Ergänzungen gingen verloren).
    **Lehre:** Vor destruktiven Migrationen auf einer befüllten DB immer `app.db` sichern.
17. ✅ *(umgesetzt)* Nachschlage-Tabellen `Instrument` + `Stimme` (+ `PersonInstrument`).
    **Autocomplete-mit-Anlegen** im Cast-Editor (Instrument/Stimme/Person werden bei Bedarf neu
    angelegt). *Offen:* dediziertes Admin-CRUD für Instrumente/Stimmen.
18. ✅ *(umgesetzt)* `StueckBeitrag` (mehrere Autor:innen je Stück mit Rolle) – Modell + Backfill.
19. ✅ *(umgesetzt)* `VideoMitwirkung` + **Cast-Editor** `/admin/videos/{id}/besetzung`:
    Person + Rolle (+ Instrument/Stimme bei Musikant:in) erfassen/löschen; neue Personen erhalten
    Default-Sichtbarkeit (Musikant → NurInitialen). *Offen:* User-Vorschläge dafür (Punkt 23).
20. ✅ *(umgesetzt)* Personen-Fenster `/personen` mit Filtern (Name, Rolle, Instrument, Kontext
    ?stueck/?komponist/?band) + Personen-Detailseite (Werke + Auftritte). `Person.AnzeigeName`
    (sichtbarkeitsabhängig) + Link-Komfort-Properties (Webseite/Instagram/X/Facebook/YouTube).
21. ✅ *(umgesetzt)* Admin-CRUD **Personen** (`/admin/personen`: Rollen, Links, Sichtbarkeit,
    **Instrumente**) + **Instrumente/Stimmen** (`/admin/instrumente`); Cast-Editor mit Sichtbarkeits-Wahl;
    **Mitwirkungs-Bewilligung** (`/admin/mitwirkungen`) + **Richtigstellungen** (`/admin/richtigstellungen`).
    **Personen-Verwaltung** `/admin/personen/{id}` (ein vereintes Fenster): Stammdaten (Name,
    Sichtbarkeit, Geburtsjahr, Bild, Bio, Rollen, Instrumente, Links) **plus** Bandmitgliedschaften
    (direkt hinzufügen/entfernen) **plus** Mitwirkungen (Video + Rolle/Instrument). Die Personen-Liste
    zeigt zusätzlich die Spalte „Bands". Band-/Instrument-Auswahl per **Autocomplete** (Tippen schlägt
    Vorhandenes vor). „Neu" legt via Dialog an und leitet auf die Bearbeiten-Seite.
    **Admin-Dashboard** zeigt alle ausstehenden Anträge (Video-/Mitwirkungs-Vorschläge,
    Richtigstellungen, Verknüpfungs-/Band-Anträge) mit Zählern; die Admin-Menüpunkte sind mit
    rotem Zähler-Badge markiert, solange etwas offen ist.
22. ✅ *(umgesetzt)* `BandMitgliedschaft` (Person↔Band über Zeit): Entity + Migration; Anzeige auf
    Band-Detailseite („Besetzung", nach Instrument gruppiert, Leitung zuerst) und Personen-Detailseite
    („Bands"). **Admin-CRUD** `/admin/bands/{id}/mitglieder` (Mitglied hinzufügen mit Find-or-create
    für Person/Instrument – keine Dubletten –, Funktion/Zeitraum, „beenden", löschen).
    **Aufnahme-Metadaten** `Video.Ort` + `Video.Anlass` (Migration `VideoOrtAnlass`, editierbar im
    Video-Dialog, Anzeige auf Stück-Detailseite). Erst-Importe: Blasorchester Stadtmusik Luzern
    (86) via `StadtmusikLuzernImport`, Jugendblasorchester Luzern (83, 4 Personen mit Stadtmusik
    geteilt) via `JBLLuzernImport`. Beide Importer sind idempotent; die Startup-Aufrufe wurden nach
    erfolgtem Import wieder entfernt (Klassen bleiben als Provenienz/Re-Import erhalten).
23. ✅ *(umgesetzt)* Community:
    - **Mitwirkungs-Vorschläge** — eingeloggte User schlagen auf der Stück-Detailseite Besetzung
      vor („Besetzung vorschlagen", Status *Ausstehend*); Admin-Review `/admin/mitwirkungen`.
      Gemeinsamer `MitwirkungService` (Autocomplete-mit-Anlegen) für Cast-Editor & Vorschlag.
    - **Richtigstellungen** — „Fehler melden" auf Stück-/Personen-/Band-Detailseiten (Freitext);
      Admin bearbeitet unter `/admin/richtigstellungen` (Antwort + Erledigt/Abgelehnt).

### Phase 7 – Deployment ✅ *umgesetzt*
24. ✅ **PostgreSQL-Migration** (dev + prod komplett auf Postgres 18; nativ unter Windows lokal,
    managed Postgres auf Railway). Siehe `DEPLOY.md` und `Postgres.md`.
25. ✅ **Deployment auf Railway.app** (Dockerfile, `$PORT`, WebSockets, ForwardedHeaders);
    Custom Domain **harmoniq.q-no.ch** mit automatischem TLS.
26. ✅ **OAuth + Mail in Produktion**: Google **und** Microsoft (Multi-Tenant + persönliche Konten),
    E-Mail-Versand via **Resend HTTPS-API** (Railway blockt SMTP); `RequireConfirmedAccount = true`,
    deutsche Identity-Fehlermeldungen, alle Auth-/Profil-Seiten übersetzt & gestaltet.
27. ✅ **PWA**: Web-Manifest, Service Worker, Install-Banner, maskable Icons (Beethoven-5-Motiv).

### Phase 8 – Vernetzung & Konzerte ⏳ *geplant* — siehe Abschnitt 5
> Ziel: aus dem Katalog ein **soziales** Entdeck-Werkzeug machen. Bewusst **offene** Anfangsphase
> (wenig Zwang/Bestätigung), um früh genug Teilnehmer:innen für spannende Vernetzung zu gewinnen.

28. **Person-Typ Zuhörer:in** (`PersonRolle`-Enum + Default-Rolle beim Onboarding).
29. **Freundschaften** (`Freundschaft`-Entity + Migration): „Befreunden"-Button auf der Personenseite,
    `/account/freunde` (Anfragen/Liste), Einbindung in die viewer-abhängige Sichtbarkeit (wie Bandkolleg:innen).
30. **Onboarding** für Konten ohne verknüpfte Person: Hinweis-Banner + Assistent (bestehende Person
    via Band+Name vorschlagen → „Das bin ich"; sonst neu als Zuhörer:in anlegen).
31. **Konzerte** (`Konzert` + `KonzertBand` + `Video.KonzertId`, Migration): `/konzerte`, `/konzerte/{id}`
    (Videos je Band gruppiert), `/admin/konzerte` (CRUD), Konzert-Auswahl im Video-Dialog,
    Konzerte-Abschnitt auf der Band-Detailseite.
32. **QR-Code** auf der Startseite (unten) für `https://harmoniq.q-no.ch` zur Smartphone-zu-Smartphone-Weitergabe
    (clientseitig generiert, kein Personenbezug).
33. **Aktivitäts-Feed** der Freund:innen/Bandkolleg:innen (Startseite eingeloggt + `/account/freunde`)
    über append-only `Aktivitaet`-Tabelle (Schreiben bei Ereignis + einmaliger Backfill); respektiert Sichtbarkeit.
    Inkl. **eigener Freitext-Beiträge** (`Typ=Beitrag`) an Freunde/Bandkollegen (schreiben/bearbeiten/löschen).
34. **Konzert-Komfort**: Autocomplete-mit-Anlegen im Video-Dialog + automatische `KonzertBand`-Zuordnung
    der Video-Band.
35. **Konzert-Programm & Mitwirkende**: Tabellen `KonzertStueck` (n:m Konzert↔Stück, je Programmpunkt
    optional Band) und `KonzertPerson` (n:m Konzert↔Person mit Rolle PersonRolleTyp). Anzeige auf der
    Konzert-Detailseite (Programm + Mitwirkende).
36. **Konzert-Erfassungs-Wizard** (`/admin/konzerte/erfassen`): ganzes Konzert in einem Schritt erfassen,
    fehlende Stücke/Komponist:innen/Bands per Find-or-create anlegen; Mitwirkende mit vorgeschlagener Rolle.

---

## 9. Projektstruktur

```
C:\Entw\Music\
├── HarmoniQ.slnx                 ← Solution (neues XML-Format, .NET 10)
├── src\
│   └── HarmoniQ.Web\             ← Blazor Web App (Haupt-Projekt; Assembly & Namespace HarmoniQ.Web)
│       ├── Components\
│       │   ├── Pages\            ← Home, Komponisten, Stuecke, Videos, Bands, Account, Admin
│       │   ├── Layout\           ← MainLayout (dunkles Theme), NavMenu
│       │   ├── Shared\           ← BewertungsBalken, BewertenDialog, VideoVorschlagDialog, Darstellung
│       │   └── Account\          ← Identity-Seiten (Login/Register/Manage)
│       ├── Services\             ← AdminInitializer, SmtpEmailSender, YouTubeId, YouTubeMetadataService
│       ├── Data\                 ← ApplicationDbContext, Models\, Migrations\, DbSeeder
│       └── wwwroot\              ← app.css, js\voter.js
└── Spezifikation.md
```

---

## 10. Externe Abhängigkeiten / API-Keys

| Service | Zweck | Kosten |
|---|---|---|
| Google OAuth | Login | Kostenlos |
| SMTP-Server | Versand von Bestätigungs-/Passwort-Reset-Mails | je nach Hoster (aktuell servertown.ch) |
| YouTube Data API v3 | Video-Suche im Import-Assistenten (optional; Key unter `YouTube:ApiKey`) | Kostenlos (10'000 Anfragen/Tag) |
| YouTube oEmbed | Titel + Kanal/Band-Erkennung (kein Key nötig) | Kostenlos |
| Railway.app / servertown.ch | Hosting | Free-Tier bzw. ~$5/Monat |

> **Secrets:** Google-Client-Secret und SMTP-Passwort werden **nie** in appsettings.json
> abgelegt, sondern lokal via `dotnet user-secrets` (bzw. Umgebungsvariablen in Produktion).
> Konfigurationsschlüssel: `Authentication:Google:ClientId/ClientSecret`,
> `Email:Host/Port/User/Password/From`, `Admin:Emails`.
