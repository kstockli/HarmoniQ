# Umsetzung: Performance-Feinschliff (TTFB) + SEO (JSON-LD, Logo-Tagline)

> Umsetzungs-Vorschlag aus dem Phase-1-Review (siehe `SpezifikationBenutzerErlebnis.md` §A10 Punkt 3
> und Block 8). Drei getrennte, unabhängig umsetzbare Teile.

## Teil A — TTFB der Startseite weiter senken (Prio: mittel-hoch)

**Ausgangslage (gemessen 2026-07-05):** Startseite-TTFB **910 ms** (vorher 1472 ms — schon besser).
Ziel: **anonym < 400–500 ms, eingeloggt < 600 ms.** Ursache bleibt: viele DB-Queries pro
Seitenaufbau in `src/HarmoniQ.Web/Components/Pages/Home.razor` (`OnInitializedAsync`), eingeloggt
zusätzlich die „Für dich"-Blöcke (kommende Konzerte deiner Bands, neue Videos deiner Bands,
Wochenüberblick).

*(Teilweise deckungsgleich mit der bereits gespawnten Aufgabe „Home.razor TTFB senken (Aggregate
cachen)" — diese Datei erweitert deren Scope um die neuen Für-dich-Queries.)*

**Vorschlag:**
1. **Globale Aggregate cachen** (`IMemoryCache`, TTL ~2–5 min): die 6 Zähler (Komponist:innen/
   Personen/Bands/Stücke/Videos/Bewertungen) + Featured-Komponist:innen + Featured-Bands. Nicht
   user-spezifisch → aus dem Hot-Path nehmen.
2. **„Für dich"-Queries (eingeloggt):** pro User halten, aber (a) auf Indizes achten
   (Band-Mitgliedschaft des Users, `Konzert.Datum`), (b) optional **kurzer per-User-Cache** (TTL
   ~1–2 min) für „kommende Konzerte deiner Bands" / „neue Videos deiner Bands".
3. **Unabhängige Queries parallelisieren** (mehrere Kontexte aus `IDbContextFactory` + `Task.WhenAll`)
   statt streng sequenziell.

**Akzeptanz:** TTFB anonym < ~450 ms, eingeloggt < ~600 ms (im Browser via Performance-API messen,
2. Aufruf = warm). Inhalt unverändert korrekt.

## Teil B — JSON-LD / strukturierte Daten (Prio: mittel, SEO)

**Ausgangslage:** `lang`, Meta-Description, Open-Graph, Twitter-Cards sind gesetzt (in
`src/HarmoniQ.Web/Components/App.razor`). **Es fehlt JSON-LD** (Schema.org) → verhindert Rich
Results bei Google. Die neue `Lokal`-Entität liefert jetzt **Koordinaten**, die sich ideal einbetten
lassen.

**Vorschlag — pro Detailseite `<HeadContent>` mit `<script type="application/ld+json">`:**
- **Konzert-Detail** (`KonzertDetail.razor`): `MusicEvent` mit
  `name`, `startDate` (Konzertdatum), `location` = `Place { name, address, geo { latitude, longitude } }`
  aus dem `Lokal`, `performer` = `MusicGroup[]` (beteiligte Bands). URL/`image` ergänzen.
- **Personen-Detail** (`/personen/{id}`): `Person` (Name, ggf. `sameAs` = Wikipedia-Link),
  Komponist:innen ggf. mit Werk-Bezug.
- Optional **Startseite:** `WebSite` (+ `SearchAction` für Sitelinks-Suchfeld).

**Wichtig:** JSON-LD nur mit **öffentlich sichtbaren** Feldern befüllen (Block-2-Sichtbarkeit —
keine privaten/nicht-öffentlichen Personen). Werte serverseitig HTML-safe serialisieren.

**Akzeptanz:** Google Rich Results Test (search.google.com/test/rich-results) besteht für eine
Konzert-URL (MusicEvent erkannt) und eine Personen-URL.

## Teil C — Logo-Tagline auf Deutsch (Prio: niedrig, Kosmetik)

**Ausgangslage:** Das Logo zeigt die englische Tagline **„THE MUSIC DATABASE"**, während die
Positionierung deutsch ist (og:title „HarmoniQ – die Blasmusik-Datenbank").
**Dateien:** `src/HarmoniQ.Web/wwwroot/img/harmoniq-logo.svg` (Tagline als `<text>` im SVG) und die
PNG-Variante `harmoniq-logo.png` (für og:image).

**Vorschlag:** Tagline im SVG auf **„Die Blasmusik-Datenbank"** ändern (oder Tagline ganz weglassen).
PNG bei Änderung neu exportieren (wird als `og:image` geteilt). Rein kosmetisch, keine Logik.

**Akzeptanz:** Logo zeigt deutsche (oder keine) Tagline; og:image konsistent.

---

## Nicht in diesem Dokument (bewusst tiefe Prio)
- **Region-Filter für anonyme „Demnächst"** (Review-Punkt 1): vom User herabgestuft — aktuell
  Sommerpause, WMC Kerkrade ist realistisch das nächste grosse Event. Später nachrüsten
  (Region/Kanton-Dropdown, Default CH), siehe `SpezifikationBenutzerErlebnis.md` §4.3.
