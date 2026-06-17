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
ihre Werke/Auftritte bis zur einzelnen Aufnahme. Siehe **Abschnitt 5b**.

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
| **User (Google-Login)** | Zusätzlich: Video-Vorschläge einreichen, eigene Bewertungen verwalten |
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

> **Hinweis Login:** Implementiert sind sowohl **lokale Konten** (E-Mail/Passwort, mit
> SMTP-Mailversand) als auch **Google-Login**. E-Mail-Bestätigung ist in der Testphase
> deaktiviert (`RequireConfirmedAccount = false`).

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

## 5. Datenmodell

> ⚠️ **Veraltet:** Die Entität **`Komponist` existiert nicht mehr** – sie wurde durch **`Person`**
> ersetzt (siehe Abschnitt 5b, jetzt umgesetzt). Stücke verweisen über **`StueckBeitrag`** auf
> Personen. Der untenstehende `Komponist`-Block ist nur noch historisch; maßgeblich ist 5b.

> **Quelle des Modells:** Die eigenen Entitäten (Stück, Band, Video, Bewertung, Person, …)
> sind als C#-Klassen in `src/HarmoniQ.Web/Data/Models/` definiert; der DbContext in
> `src/HarmoniQ.Web/Data/ApplicationDbContext.cs`. **Benutzer und Rollen** stammen
> **nicht** aus eigenem Code, sondern aus **ASP.NET Core Identity** (`IdentityDbContext`).
> Die SQLite-Tabellen `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserLogins`
> usw. enthalten daher **viele weitere, von Identity verwaltete Spalten** (z. B.
> `NormalizedEmail`, `PasswordHash`, `SecurityStamp`, `LockoutEnd`) — unten sind nur die
> für diese App relevanten Felder aufgeführt. Maßgeblich ist immer das Identity-Schema,
> nicht diese vereinfachte Darstellung.

```
Komponist
├── Id (Guid)
├── Name (string)
├── Biografie (string?)
├── Webseite (string?)            ← URL für Import-Assistent
├── BildUrl (string?)
└── Stücke [1:n]

Stück
├── Id (Guid)
├── KomponistId (FK)
├── Titel (string)
├── Jahr (int?)
├── Schwierigkeitsgrad (enum: Leicht / Mittel / Schwer / SehrSchwer / Unbekannt)
├── Besetzung (string?)           ← z. B. "Blasorchester"
├── Beschreibung (string?)
├── OriginalUrl (string?)         ← Quell-URL beim Import
└── Videos [1:n]

Band
├── Id (Guid)
├── Name (string)
├── Land (string?)
└── Webseite (string?)

Video
├── Id (Guid)
├── StückId (FK)
├── BandId (FK?)                  ← nullable, falls Band unbekannt
├── YouTubeVideoId (string)       ← nur die 11-stellige ID (aus URL extrahiert)
├── Titel (string)                ← optional bei Eingabe; sonst autom. via YouTube-oEmbed
├── AufnahmeDatum (DateOnly?)
├── Ort (string?)                 ← optional, Aufnahme-Ort (z. B. "KKL Luzern")
├── Anlass (string?)              ← optional (z. B. "Jahreskonzert 2024")
├── ErstelltAm (DateTime)         ← Erfassungszeitpunkt (für "zuletzt hinzugefügt")
├── Status (enum: Ausstehend / Genehmigt / Abgelehnt)
├── VorgeschlagenVon (FK → User?) ← null = Admin erfasst
└── Bewertungen [1:n]

Benutzer                          ← ASP.NET Core Identity (AspNetUsers)
├── Id (string)
├── Email (string)
├── (externe Logins z. B. Google → AspNetUserLogins)
└── Bewertungen [1:n]

Rollen                            ← ASP.NET Core Identity (AspNetRoles / AspNetUserRoles)
├── "Admin"  → Vollzugriff auf /admin
└── (Standard-Benutzer haben keine Rolle)
   Zuweisung: Beim App-Start werden die in appsettings unter "Admin:Emails"
   konfigurierten Benutzer automatisch der Rolle "Admin" zugeordnet, sobald sie
   registriert sind (siehe Services/AdminInitializer.cs).

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
```

> **Hinweis:** Das obige Modell ist der **aktuell umgesetzte Stand**. Abschnitt 5b
> beschreibt die geplante Erweiterung zum Personen-/Rollen-Modell (noch nicht umgesetzt).

---

## 5b. Datenmodell – Personen & Rollen (IMDb-Stil) *(umgesetzt)*

### Konzept
Die bisherige Entität **Komponist** wird durch **Person** ersetzt. Eine Person kann
**mehrere Rollen** haben (Komponist:in, Dirigent:in, Musikant:in).

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
│                                    Default: Öffentlich (Komponist/Dirigent), Nur Initialen (Musikant)
├── Biografie (string?)
├── BildUrl (string?)
├── Geburtsjahr (int?)
├── BenutzerId (FK → User?, UNIQUE) ← optional: „das bin ich"-Verknüpfung zum eingeloggten Konto
├── Rollen [n] → PersonRolle        ← Komponist / Dirigent / Musikant
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
├── Url (string)
└── Typ (enum: Webseite / Instagram / X / Facebook / …)   ← erweiterbar

PersonRolle                         (welche Rollen kann die Person grundsätzlich?)
├── PersonId (FK)
├── Rolle (enum: Komponist / Dirigent / Musikant)
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

Stück                               (KomponistId entfällt → StückBeitrag)
├── Id, Titel, Jahr, Schwierigkeitsgrad, Besetzung, Beschreibung, OriginalUrl
└── Beiträge [1:n] → StückBeitrag

StückBeitrag                        (wer hat zum Stück beigetragen – mehrere möglich)
├── Id (Guid)
├── StückId (FK)
├── PersonId (FK)
└── Rolle (enum: Komponist / Arrangeur / Bearbeiter)

Video                              (zusätzlich zur bisherigen Band-Zuordnung)
├── … (wie bisher: StückId, BandId?, YouTubeVideoId, Titel, Status, …)
├── AufnahmeDatum (DateOnly?)      ← bereits vorhanden (optional)
├── Ort (string?)                  ← UMGESETZT, optional, z. B. "KKL Luzern"
├── Anlass (string?)               ← UMGESETZT, optional, z. B. "WMC Kerkrade 2022"
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

Richtigstellung                     (Freitext-Hinweis/Korrektur von eingeloggten Usern)
├── Id (Guid)
├── BetrifftTyp (enum: Video / Stück / Person / Band)
├── BetrifftId (Guid)              ← Verweis auf das gemeinte Objekt
├── Text (string)                  ← die eigentliche Richtigstellung (Freitext)
├── EingereichtVon (FK → User)
├── ErstelltAm (DateTime)
├── Status (enum: Offen / Erledigt / Abgelehnt)
├── Antwort (string?)              ← Antwort/Notiz des Admins
└── AntwortAm (DateTime?)          ← wann der Admin geantwortet hat
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
- Richtigstellung verweist lose (Typ + Id) auf Video/Stück/Person/Band

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
/bands/{id}                   → Band-Detail (Aufnahmen + gespielte Stücke)
/account/login                → Login (lokal + Google)
/account/register             → Registrierung (lokales Konto)
/account/profil               → Meine Bewertungen
/admin                        → Admin-Dashboard
/admin/komponisten            → CRUD Komponisten
/admin/stuecke                → CRUD Stücke
/admin/bands                  → CRUD Bands
/admin/videos                 → CRUD Videos
/admin/bewertungen            → Bewertungen verwalten (bearbeiten / löschen)
/admin/vorschlaege            → Review-Queue für User-Vorschläge
/admin/import                 → Import-Assistent (3-Schritt-Wizard)

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

### Phase 6 – Personen- & Rollen-Modell (IMDb-Stil) ⏳ *geplant* — siehe Abschnitt 5b
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

### Phase 7 – Deployment ⏳ *geplant*
24. PostgreSQL-Migration
25. Deployment (servertown.ch-Container oder Railway.app)
26. Google OAuth + SMTP Produktions-Konfiguration

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
