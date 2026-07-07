# HarmoniQ – Spezifikation Benutzer-Erlebnis (UX & Strategie)

> Dritte, eigenständige Spezifikation (neben `Spezifikation.md` = Datenmodell/Funktion und
> `Spezifikation-Crawler.md` = Import-Roboter). Hier geht es **nicht** um einzelne Features oder
> Entitäten, sondern um das **Gesamt-Erlebnis**: Wie fühlt sich HarmoniQ für eine echte Person an,
> wie findet sie hinein, was bringt sie zum Wiederkommen, und wie wächst die Plattform.
> Produktiv unter `https://harmoniq.q-no.ch`.
>
> **Charakter dieses Dokuments:** Lebendiges Diskussions-Protokoll. Offene Fragen stehen als
> *Offen*, Entschiedenes wird **fett** als „Entscheid" festgehalten (mit Begründung + Datum).
> Test-Zugänge (zwei temporäre Claude-Accounts, normal + Admin) existieren; Passwörter werden hier
> bewusst **nicht** protokolliert.

---

## 0. Vorgehen / Agenda (Diskussions-Reihenfolge)

Die Themen hängen voneinander ab — manche sind **Rahmenbedingungen**, die spätere Entscheide
einschränken. Vorgeschlagene Reihenfolge (von „setzt den Rahmen" zu „baut darauf auf"):

| # | Block | Warum an dieser Stelle | Status |
|---|---|---|---|
| 1 | **Zielbild, Zielgruppen & Erfolg** | Klärt, *für wen* und *wozu* — jede UX-Entscheidung misst sich daran | offen |
| 2 | **Datenschutz, Recht & Vertrauen** | Harte Rahmenbedingung (CH-DSG, Persönlichkeitsrechte); bestimmt Onboarding & Person-Erfassung | offen |
| 3 | **Onboarding & Profil/Person** | „Erster Eindruck" + Claim-Flow; baut auf 1 & 2 auf | offen |
| 4 | **Kern-Anwendungsfälle (was ist spannend?)** | Priorisierung der Features, die Wiederkommen erzeugen | offen |
| 5 | **Rollen & Berechtigungen** | Zwischenstufe „Band-Admin", Moderation; folgt aus 4 | offen |
| 6 | **Navigation & Selbsterklärbarkeit** | Finden sich User zurecht? | offen |
| 7 | **Design, Gefühl & Markenauftritt** | Vereinheitlichung, Startbild, Ton/Sound | offen |
| 8 | **Performance & technischer Stack** | „Lange bis reaktiv", Blazor Server vs. Client, Railway-Abo | offen |
| 9 | **Verbreitung & Sichtbarkeit** (mittelfristige Hauptfrage) | Hängt von SEO (8) + gutem Produkt (3–7) ab → bewusst spät | offen |
| 10 | **Langfristiger Ausbau** | Mehrsprachigkeit (FR/EN), öffentliche API | offen |

**Empfehlung zum Einstieg:** Bevor wir Block 3–7 diskutieren, mache ich einen **echten
Onboarding-Durchlauf** auf der Live-Seite (anonym → Registrierung → normaler User → Admin) und
protokolliere die Reibungspunkte. So diskutieren wir an konkreten Befunden statt an Vermutungen.

---

## 0.1 Entscheid-Übersicht & Roadmap (Stand 2026-06-30)

*Alle 10 Blöcke durchdiskutiert. Kern-Entscheide kompakt + empfohlene Umsetzungs-Reihenfolge.*

**Strategie-Leitsätze:**
- **Vertiefen + fokussieren, NICHT verbreitern** (1.1) — kein neuer Inhaltstyp; bestehende Inhalte
  kontext-/entdeckungs-zentriert rahmen.
- **Smartphone-first** (Block 6) — mobile Ansicht ist der Maßstab, Desktop sekundär (Band-Admins).
- **Verbreiten erst, wenn „richtig gut"** (Block 9) — sonst verbrennt jeder Verein seinen ersten Eindruck.
- Primär-Zielgruppe: **Publikum/Fans (persönlicher Mehrwert) + aktive Musiker:innen.**

**Phase 0 — Fundament & Quick Wins (jetzt, billig, hoher Hebel):** *(Umsetzungs-Stand 2026-06-30)*
- ✅ **TTFB-Fix:** `Home.razor`-Aggregate gecacht (`IMemoryCache`, 5 min) + restliche Queries via
  `Task.WhenAll` parallelisiert + Startseiten-Indizes. (Commit „Startup-Performance".) [Block 8]
- ✅ **SEO/Sharing:** `lang=de`, `meta description`, **Open-Graph-Tags** (`og:type/site_name/title/
  description/url/image/locale`) + Twitter-Card + `canonical` in `App.razor`. OG-Bild =
  `img/harmoniq-logo.png`; Basis-URL via `Seo:BasisUrl` konfigurierbar (Default Live-Domain).
  [Block 9.1 / A7]
- ✅ **Skeleton statt „0"** als Ladezustand: Statistik-Kacheln zeigen beim Circuit-Aufbau einen
  `MudSkeleton` statt der irreführenden „0" (`_geladen`-Flag in `Home.razor`). **Ohne Ton** (kein
  Audio eingebaut). *Dezente Logo-Animation: noch offen (optional, niedrige Prio).* [Block 7]
- ✅ **Analytics-Plumbing:** datenschutzkonformer Script-Slot in `App.razor`, **standardmäßig aus** —
  aktiviert sich erst, wenn `Analytics:ScriptUrl` (+ optional `Analytics:Domain`) gesetzt ist
  (passt für Plausible/Umami, cookielos). *Instanz/Domain noch vom User zu hinterlegen.* [Block 9.1]
- ✅ **i18n-Disziplin / Sichtbarkeit:** `lang=de` gesetzt; **Sichtbarkeits-Logik bereits zentral**
  in `Services/PersonenSicht.cs` (single source of truth für UI + spätere API). Leitlinie „keine
  neuen Hardcoded-Strings" bleibt bestehen; voller `.resx`-Retrofit terminiert auf Romandie-Push
  (Block 10). [Block 10]

**Phase 1 — Kern-Erlebnis „richtig gut" machen:**
- ✅ **Konzert-Tagebuch** (`KonzertBesuch` + `StueckEindruck`, privat) — umgesetzt: „Ich war im
  Publikum" + Sterne/Notiz je Programmpunkt (auch bei Wettbewerben), Seite „Mein Konzert-Tagebuch"
  mit „Mein Konzert-Jahr", 4-stufige Sichtbarkeit (NurIch/FreundeAnwesenheit/Freunde/Öffentlich),
  Feed-Eintrag „war beim Konzert", „Eindrücke der Besucher:innen" auf der Konzertseite,
  Admin-Moderation. Abgrenzung zu „Ich habe mitgewirkt" (`KonzertPerson`, Bühnenrollen). Details in
  `Spezifikation.md`. **Offen/optional:** Excel-Import der eigenen Historie (zurückgestellt).
- ✅ **Neuer Einstieg:** umgesetzt — kontext-zentrierte Startseite (anonym: Hero „Dein Konzert-Tagebuch",
  B+A-Positionierung; eingeloggt: „Mein Konzert-Jahr"-Teaser oben, „Demnächst", „Deine Bands"-Sektion,
  Aufnahmen; Statistik klein/weit unten), **Bottom-Navigation** (mobil, 5 Tabs), Komponist:innen bereits
  eigener Menüpunkt, vereinheitlichte **Stück-Zeile** (`StueckZeilen`: Karten mobil / Tabelle Desktop,
  in Person-Werke & Band-Stücke). [Block 6/7]
- ✅ **`Lokal`-Entität** (+ `LokalAlias` + Merge + CRUD, Koordinaten/Geocoding/Karte) — umgesetzt;
  ersetzt den Freitext-Ort, Basis für Distanz. [Block 4.3]
- ✅ **„Demnächst"-Distanz** — `/konzerte`-Distanzfilter (Standort/PLZ + Radius, Auto-Standort) und
  km-Anzeige; Startseite datums-sortiert mit dekorativem km, einheitliche `KonzertKarte`. [Block 4.3]
- ✅ **Claim Modell B** — umgesetzt: (1) **„Meine Person" legt beim ersten Speichern IMMER eine eigene
  Person an** (Default Zuhörer:in) – der frühere Onboarding-Zwischenschritt (Namens-Dialog) entfällt,
  `/account/onboarding` leitet nur noch auf `/account/person` weiter. **Keine Identitäts-Pickliste**
  (datenschutz-heikel, Block 2/3 ausdiskutiert) – es werden keine fremden Personen zur Auswahl
  vorgeschlagen. (2) **Evidenzbasierter
  Merge-Vorschlag** erst *später* (`ClaimVorschlagService`): auf „Meine Person" ein dezenter,
  wegklickbarer Hinweis „Gehörst du zu dieser bereits erfassten Person X?", nur bei **starker Konfidenz**
  (Name-Gleichheit normalisiert **+ gemeinsame Band**); Annahme → Merge der Selbst-Person in die erfasste
  Person (Verknüpfung wandert mit). (3) **Verifizierungs-Gate** für **sichtbare Rollen**
  (Dirigent:in/Komponist:in/öffentlich): **kein Auto-Merge**, Hinweis auf Admin/Verein-Bestätigung; für
  „DAS BIN ICH"-Restpfade steht `VerknuepfungService.BeanspruchenAsync` (offener `PersonAnspruch`
  statt Sofort-Verknüpfung). [Block 3]
  **Follow-up:** Merge-on-confirm im Admin für gegateten sichtbaren Claim (heute manuell via Person-Merge);
  Signal „besuchte Konzerte" (Tagebuch) als zusätzlicher Trigger (v1 nutzt Band / Instrument+Band).
- 🟡 **Wiederkehr-Schleife (Benachrichtigungen)** [Block 4.2] — **Kern UMGESETZT (2026-07-04).**
  „Band folgen" (`BandInteresse`), Präferenzen (2 unabhängige Kanäle + Abmelde-Token), Digest-
  Zusammenstellung (`DigestService`, Bausteine A/B/C aus Mitgliedschaft ∪ Folgen), Versand über
  **E-Mail** (Wochenüberblick + One-Click-Abmeldung) **und PWA-Push** (VAPID/Service-Worker), gesteuert
  vom wöchentlichen `WochenBenachrichtigungHostedService`; Dedup via `BenachrichtigungGesendet`.
  Präferenzseite `/account/benachrichtigungen` mit Live-Vorschau + Geräte-Push-Anmeldung. Startseiten-
  **Feed** „Für dich" (A/B/C aus Mitglied- ∪ gefolgten Bands, ohne Dedup, `DigestService(nurUngesehene:false)`)
  umgesetzt. Trigger **F** („in deiner Nähe") umgesetzt: privater, ~1 km vergröberter Standort/Heimat-PLZ
  an `Person` (opt-in, auf `/account/benachrichtigungen`), Nähe-Konzerte fremder Bands im 30-km-Umkreis.
  **Damit ist der v1-Umfang von 4.2 vollständig.** Optional später: ereignisgesteuerte Sofort-Mails.

**Phase 2 — Verbreitung starten (wenn Readiness-Checkliste 9.4 erfüllt):**
- **Band-Admin**-Rolle (`BandAdministrator` + Audit-Log) + **Crawler-Einladung** an offizielle
  Adressen. [Block 5]
- **Warmer Pilot:** JBL (Jugendblasorchester Luzern — Tochter, Flyer) + 3–5 bekannte Vereine, dann
  regionsweise Crawler-Einladung. [Block 9.3]

**Phase 3 — Romandie & Skalierung:**
- **FR-Retrofit** + FR-Pilotverein; **Verband-Verstärkung**; später EN, **öffentliche API**,
  Geocoding/Karte für Lokale. [Block 9/10]

---

## 1. Zielbild, Zielgruppen & Erfolg

*Rahmenfrage: Bevor wir über Buttons und Bilder reden — wofür ist HarmoniQ da, und woran erkennen
wir, dass es funktioniert?*

**Zielgruppen (Hypothese, zu schärfen):**
- Aktive Blasmusiker:innen (Vereinsmitglieder) — wollen sich/ihren Verein wiederfinden, vernetzen.
- Vereins-Funktionär:innen (Präsidium, Dirigent:in, Musikkommission) — pflegen Daten, suchen Aushilfen.
- Interessierte / Publikum / Fans — schauen Videos, Konzert-Kalender.
- (Crawler-seitig) Personen, die **schon erfasst sind, aber noch keinen Account haben** → Claim.

**Offen:**
- Welche dieser Gruppen ist die **primäre**? (Bestimmt, wofür wir optimieren.)
- Was ist der **eine Satz**, der HarmoniQ erklärt? („IMDb für Blasmusik" ist intern — was sagen wir Nutzern?)
- **Wie messen wir Erfolg?** Aktive User? Wiederkehr-Rate? Beanspruchte Profile? Datenqualität?
  → Braucht es datenschutzkonforme Analytics (z. B. Plausible/self-hosted, ohne Cookies)?
- **Erfolg ≠ nur Zahl der User:** Auch „jeder Verein der Region ist sauber abgebildet" ist ein Ziel.

**Entscheid (2026-06-29):** **Primäre Zielgruppe = Publikum/Fans, die angemeldet einen persönlichen
Mehrwert bekommen** (siehe Block 4: „meine besuchten Konzerte" mit privaten Notizen/Bewertungen pro
Stück) **+ aktive Musiker:innen**. → UX wird für diese zwei Gruppen optimiert; Funktionär:innen und
reiner Anonym-Konsum sind sekundär (aber unterstützt).

### 1.1 Standortbestimmung: Datenbank vs. persönliches Erlebnis (Diskussion 2026-06-29)
*Frage des Users vor Block 9: „Ist das das, was man als User will? Fehlt ein Feature? Oder ist es
überladen?"*

**Befund aus dem Test:** 184 Videos, **nur 2 Bewertungen**. **Einordnung (User 2026-06-29):** Die
vielen Videos wurden **bewusst per Crawler** beschafft, um überhaupt **Stoff** zu haben — die 2
Bewertungen sind also **kein** Engagement-Versagen einer echten Nutzerbasis, sondern früher
Seed-Stand. Der grundsätzliche Punkt bleibt aber: Reines Browsen/Bewerten von Aufnahmen ist als
**Wiederkehr-Grund** schwach (Nachschlagewerk-Muster). Gute Videos und **künftige Konzerte** sind
sehr wohl Teil des Reizes (vom User bestätigt) — sie funktionieren am besten als **Entdeckungs-
Einstieg**, der in die persönlichen Sog-Features (Tagebuch, Wiederkehr-Schleife) hineinführt.

**Diagnose (Hypothese):** HarmoniQ ist heute überzeugend als **Datenbank/Lexikon** gebaut
(„the music database", 6 flache Dimensionen in der Nav: Konzerte/Komponist:innen/Personen/Stücke/
Videos/Bands). Die **primäre Zielgruppe** (Fans + aktive Musiker:innen) will aber ein **persönliches
+ soziales Erlebnis**. Es ist also **nicht „mit den falschen Features überladen"**, sondern
**breit und lexikon-förmig gerahmt**, wo „mein Kontext zuerst" stärker zöge.

**Drei Linsen auf die Frage:**
- **Was wollen User?** Einen **Grund wiederzukommen** (persönlicher Mehrwert, der wächst → das
  Konzert-Tagebuch, Block 4) **und** einen **echten Nutzen** (wiederkehrendes Vereins-Problem →
  Aushilfe-Suche, Block 4). Beides erzeugt Bindung, das bloße Browsen nicht.
- **Fehlt ein Feature?** Nicht *mehr Inhaltstypen*, sondern: (a) **Wiederkehr-Schleifen**
  (Benachrichtigungen/Feed: „neues Video deiner Band", „Aushilfe Trompete gesucht"), (b) die
  **Aushilfe-Suche** (noch nicht gebaut, größtes Alleinstellungsmerkmal), (c) ein **„das bin/ist
  meins"-Moment beim ersten Besuch** (Claim/eigene Band früh sichtbar, statt hinter Lexikon-Liste).
- **Überladen?** Mildes Risiko: 6 gleichrangige DB-Dimensionen sind für eine:n Fan viel; die
  Trennung **„Komponist:innen" vs. „Personen"** ist subtil (Komponist = Person) und kann verwirren.
  Einstieg sollte **kontext-zentriert** sein (meine Konzerte / meine Band / was läuft), nicht eine
  flache Liste „1438 Stücke".

**Empfehlung (Reihenfolge vor Block 9):** **NICHT verbreitern** (keine neuen Inhaltstypen),
sondern **vertiefen + fokussieren**:
1. die zwei Sog-Features ausbauen (Tagebuch + Aushilfe-Suche),
2. den Einstieg auf den eigenen Kontext umstellen,
3. eine Wiederkehr-Schleife (Benachrichtigung/Feed) ergänzen.
**Erst danach** Block 9 (Verbreitung) — sonst lädt man Vereine zu einem „einmal anschauen" ein.

**Wie entscheiden statt raten:** (a) **Ein-Satz-Positionierung** festlegen (Block 1) — Nutzer-Nutzen
statt „Datenbank"; (b) **Mini-Test mit 1–2 echten Vereinsmitgliedern**: 5 Minuten zuschauen, wo sie
hängen bleiben / was sie vermissen. Das beantwortet „wollen sie das?" belastbarer als jede Annahme.

**Entscheid:** *(offen — Richtung „vertiefen+fokussieren vor verbreiten" vorgeschlagen)*

---

## 2. Datenschutz, Recht & Vertrauen *(neu — war in der Fragestellung nicht explizit)*

*Warum so früh: HarmoniQ zeigt **echte Personen mit Namen, Instrument, Verein, teils Bild** — auch
solche, die sich nie angemeldet haben (vom Crawler erfasst). Das ist die größte rechtliche und
Vertrauens-Frage und schränkt Onboarding (Block 3) und Person-Erfassung direkt ein.*

**Offen:**
- **Schweizer DSG / Persönlichkeitsrechte:** Dürfen Personen ohne Einwilligung öffentlich gelistet
  werden (Name + Instrument + Verein)? Vermutlich ja für „öffentlich auftretende Funktionäre/Konzert-
  Mitwirkende" (berechtigtes Interesse, Daten stammen aus öffentlichen Quellen), aber: klare
  **Löschung-/Widerspruch-Funktion** nötig. Wie niederschwellig?
- **Recht am eigenen Bild:** Crawler-/Wikipedia-Bilder — Quelle & Lizenz dokumentieren.
- **Minderjährige:** Jugendblasmusik (JBL!) → besonders heikel. Andere Defaults für Sichtbarkeit?
- **Pflicht-Seiten:** Impressum, Datenschutzerklärung, evtl. Nutzungsbedingungen — vorhanden?
- **Sichtbarkeits-Default — geklärt (User, 2026-06-29):** Per Default sind **nur Komponist:innen,
  Dirigent:innen und Vorstände** öffentlich sichtbar. Musikant:innen und Zuhörer:innen sind **nicht**
  per Default sichtbar. → Das **entschärft das Claim-Risiko erheblich**: Die große Mehrheit der
  erfassten Personen ist gar nicht öffentlich; nur die wenigen sichtbaren Rollen
  (Dirigent:in/Vorstand/Komponist:in) sind „lohnende" Impersonations-Ziele → dort lohnt sich der
  Verifizierungs-Aufwand, beim Rest nicht. (Bestätigt im Test A5: Zuhörer:innen erscheinen nur als
  Initialen.)
- **Vertrauen/Moderation:** Wer darf Daten ändern? Wie verhindern wir Falsch-/Vandalismus-Einträge?
  Melde-Funktion für anstößige/falsche Inhalte?

**Entscheid:** *(offen)*

---

## 3. Onboarding & Profil / Person

*Der erste Eindruck. Ziel: in <60 Sekunden von „nie gehört" zu „das bin ja ich / mein Verein".*

> **Onboarding-Schritt „Folge Bands" (2026-07-05):** Nach dem Person-Claim folgt ein Schritt, in dem
> man (mind. 1, überspringbar) Bands zu folgen wählt — das füttert den „Für dich"-Feed. Details +
> Datenmodell (`Band.HeimatLokalId`, Bands in der Nähe) in **§4.4**.

**Offene Fragen (aus der Sitzungseröffnung + Ergänzungen):**
- **Login-Reihenfolge:** Sollten **Google-/Microsoft-Buttons zuerst** stehen (vor lokalem
  E-Mail/Passwort)? *(Hypothese: ja — Social-Login senkt die Hürde; lokaler Login als „oder".)*
- **Person-Erfassung proaktiv:** Soll nach der Registrierung die **eigene Person automatisch
  vorgeschlagen** werden (statt optionalem Extra-Schritt)? Und bei **Namensgleichheit** der
  **Merge/Claim** angeboten werden („Bist du diese bereits erfasste Person?")?
- **Claim-Flow *(Ergänzung)*:** Sehr viele Personen sind durch den Crawler schon angelegt. Der
  stärkste Aha-Moment ist „dein Profil existiert schon — beanspruche es". Wie verifizieren wir, dass
  jemand wirklich diese Person ist (E-Mail-Match? Bestätigung durch Band-Admin? Selbstdeklaration)?
- **Onboarding selbsterklärend vs. geführt:** Kurze geführte Tour (2–3 Schritte) oder reines
  „Learning by doing"? *(Ergänzung: optionale, überspringbare Tour — nicht aufzwingen.)*
- **E-Mail-Verifizierung & Benachrichtigungen *(Ergänzung)*:** Double-Opt-in? Welche
  Benachrichtigungen (neue Freundschaftsanfrage, Konzert deines Vereins, Kommentar)? E-Mail vs.
  Push (PWA)?

**Claim-Modell — zwei Ansätze (Diskussion 2026-06-29):**
- **Modell A (heute live):** Onboarding zeigt beim Tippen des Namens eine Trefferliste echter
  Personen mit „DAS BIN ICH" → sofortige Verknüpfung. Schnell, aber: präsentiert eine
  **Identitäts-Auswahlliste** und verknüpft **ohne Verifizierung** (Impersonations-Risiko, v. a.
  bei den sichtbaren Rollen Dirigent:in/Vorstand).
- **Modell B (User-Vorschlag, bevorzugt):** User **erfasst zuerst die eigenen Daten** (legt sich
  als eigene Person an, Default „Zuhörer:in"); **erst später**, wenn **„einiges passt"** (genug
  übereinstimmende Signale: Name + Band + Instrument + besuchte Konzerte), fragt das System
  proaktiv: „Bist du dieselbe Person wie X?" → Merge-Angebot.

**Bewertung (warum B aus Datenschutz-Sicht besser ist):**
1. **Keine Identitäts-Pickliste:** B präsentiert fremde Personendaten nicht als „zum Beanspruchen";
   der Treffer wird vom System aus Evidenz vorgeschlagen, nicht aus einem Verzeichnis ausgewählt.
2. **Evidenzbasiert statt Behauptung:** „einiges passt" ist eine **weiche Verifizierung** — ein
   Impostor müsste ein passendes Profil *aufbauen* statt nur einen Button zu klicken.
3. **Datensparsamkeit/Reihenfolge:** Person startet mit **eigenen** (nicht-sichtbaren) Daten und
   willigt erst kontextbezogen in die Verknüpfung mit dem reicheren öffentlichen Record ein.
4. **Passt zur Architektur:** Nutzt den bereits vorhandenen **Person-Merge** (Doppel-Person aus
   Selbst-Erfassung + Crawler-Record zusammenführen).

**Einschränkung / Empfehlung (Synthese):** Modell B als **Default**, ABER für die **öffentlich
sichtbaren, sensiblen Rollen** (Dirigent:in, Vorstand) zusätzlich eine **Bestätigung** verlangen
(z. B. Band-Admin-Freigabe oder E-Mail-Abgleich), bevor der Merge auf das öffentliche Profil wirkt.
Solange unbestätigt: Status **„beansprucht – unbestätigt"**. Da laut Block 2 nur diese Rollen
überhaupt öffentlich sind, konzentriert sich der Verifizierungs-Aufwand genau auf die wenigen
relevanten Fälle; für Zuhörer:innen/Musikant:innen (nicht sichtbar) genügt der weiche Merge.

**Entscheid (Richtung, 2026-06-29):** Tendenz **Modell B** (Erfassen → später evidenzbasiert
Merge vorschlagen) als Leitidee; „DAS BIN ICH"-Sofortpfad höchstens für nicht-sichtbare Rollen
behalten.

**Trigger „einiges passt" (provisorisch festgezurrt 2026-06-29 — speist sich aus Block 4):**
- **Kandidaten-Generierung:** Vergleiche die **Selbst-Person** des Users (Name, ggf. Band,
  Instrument, **besuchte Konzerte aus Block 4**) mit bestehenden Personen.
- **Konfidenz-Stufen:**
  - **Stark** = Name (fuzzy) **+ mindestens ein Korroborations-Signal** (gleiche Band-Mitgliedschaft
    ODER die bestehende Person war Musikant:in an einem Konzert, das der User als besucht markiert
    hat ODER Instrument + Band gleich) → **Hinweis anzeigen**.
  - **Schwach** = nur Name → **kein** proaktiver Vorschlag (kein „Picklisten"-Leak).
- **Darstellung = dezenter, wegklickbarer Hinweis, KEIN Pop-up/Zwang:** „Gehörst du zu dieser
  bereits erfassten Person? → prüfen". Bestätigt der User → Merge (Selbst-Person + Crawler-Person).
- **Verifizierungs-Gate:** Wirkt der Merge auf ein **öffentlich sichtbares** Profil (Dirigent:in/
  Vorstand/Komponist:in, siehe Block 2), bleibt Status **„beansprucht – unbestätigt"** bis
  Bestätigung (Band-Admin-Freigabe oder E-Mail-Abgleich). Nicht-sichtbare Rollen: weicher Merge ok.
- **Offen:** Genaue Fuzzy-Namens-Schwelle; ob „stark" sofort oder erst ab ≥2 Signalen anzeigt.

---

## 4. Kern-Anwendungsfälle — „Was ist spannend?"

*Die Features, die zum Wiederkommen bewegen. Sammlung aus der Eröffnung + Ergänzungen, noch zu
**priorisieren** (Aufwand × Sog).*

| Idee | Sog (Hypothese) | Bemerkung |
|---|---|---|
| Videos sehen & bewerten | mittel | Kernfunktion, schon da |
| **Aushilfe-Suche** („Wer spielt Trompete & ist erreichbar?") | **hoch** | Echtes Vereins-Problem, einzigartig, Netzwerkeffekt |
| Mit anderen Musiker:innen vernetzen (Freundschaften) | mittel-hoch | Verstärkt Aushilfe-Suche |
| „Welcher Verein hat welche Partitur?" | mittel | Nische, aber sehr nützlich für Dirigent:innen |
| Konzert-Kalender / „was läuft in meiner Region" | hoch | Niederschwellig, auch für Publikum |
| Ranglisten/Wettbewerbsresultate (SBBW/EMF) | mittel | Schon im Datenmodell |
| *(Ergänzung)* Eigene Auftritts-/Repertoire-Historie pro Person | mittel | „Mein musikalischer Lebenslauf" |
| **„Meine besuchten Konzerte"** (angemeldet) | **hoch** | **Persönliches Tagebuch:** Konzerte als „besucht" markieren, **private Notizen** und/oder **Bewertungen pro Stück** — siehe Entscheid |
| *(Ergänzung)* Geteilte Listen / Empfehlungen | offen | |

**Entscheid (2026-06-29):** Leit-Anwendungsfall für die primäre Zielgruppe (Block 1) =
**„Meine besuchten Konzerte"**. Angemeldete Nutzer:innen markieren besuchte Konzerte und hinterlegen
dazu **private Notizen** und/oder **Bewertungen — auf Stück-Ebene**. Offene Detailfragen für später:
- **Privat vs. öffentlich:** Notizen privat (nur ich); Bewertungen evtl. aggregiert öffentlich?
- **Verhältnis zur bestehenden Video-Bewertung:** Ist die Stück-Bewertung „beim Konzert" dasselbe
  wie/getrennt von der Video-Bewertung? (Datenmodell-Auswirkung → ggf. `Spezifikation.md` anpassen.)
- **Datenmodell:** braucht vermutlich eine Verknüpfung User ↔ Konzert (`KonzertBesuch`) + private
  Notiz/Bewertung pro `KonzertStueck`. *(Noch zu spezifizieren — berührt Datenmodell-Spec.)*

Weitere starke Kandidaten (offen, niedrigere Prio): **Aushilfe-Suche** + **Konzert-Kalender**.

### 4.1 Spezifikation „Konzert-Tagebuch" (Leit-Feature, Entwurf 2026-06-29)

**Idee:** Angemeldete Nutzer:innen führen ein persönliches Tagebuch der Konzerte, die sie besucht
haben — mit privaten Notizen und einer Bewertung **pro gespieltem Stück**. Das ist der persönliche
Mehrwert für die primäre Zielgruppe (Publikum/Fans) **und** die Datenquelle für den Claim-Trigger
(Block 3).

**Befund aus dem Test (A5):** Das Modell kennt bereits **Person ↔ Konzert als Zuhörer:in**
(„Mitwirkende & Gäste"). Es fehlt: Self-Service-Markierung, private Notizen, Stück-Bewertung.

**Vorgeschlagene Entitäten (→ berührt `Spezifikation.md`, bei Umsetzung dort nachführen):**
- **`KonzertBesuch`** (User/Person ↔ Konzert): „Ich war dabei". Felder: Konzert, User, Datum,
  **Sichtbarkeit** (Default **privat**; später optional „für Freunde"), optionale Gesamt-Notiz.
  *Verhältnis zu bestehender Zuhörer:in-Verknüpfung klären:* selbst-deklarierter Besuch vs.
  kuratierte „Mitwirkende"-Liste — entweder dieselbe `KonzertPerson(Zuhörer:in)` mit Flag
  „selbst markiert", oder eigene Tabelle. (Empfehlung: eigene `KonzertBesuch`-Tabelle, weil
  Besuch privat ist und Mitwirkende öffentlich kuratiert.)
- **`StueckEindruck`** (User ↔ `KonzertStueck`): **private Bewertung** (z. B. 1–5) + **private
  Notiz** zu einem Stück, *wie es an diesem Konzert gespielt wurde*. Granularität = `KonzertStueck`
  (Stück+Band+Konzert), nicht der abstrakte `Stueck`.

**Abgrenzung zur bestehenden Video-Bewertung (wichtige Entscheidung, offen):**
- Video-Bewertung = Bewertung einer **Aufnahme** (öffentlich/aggregiert, heute kaum genutzt: 2).
- Stück-Eindruck = **persönlicher Live-Eindruck** eines besuchten Konzerts (privat).
- → Sie sind konzeptionell verschieden (Aufnahme vs. Live). **Empfehlung: getrennt halten**,
  später optional zu einem „Stück-Gesamteindruck" aggregieren. *(Zu bestätigen.)*

**UX-Platzierung:**
- **Konzert-Detail:** Toggle „Ich war dabei" (privat). Sobald gesetzt, je Programm-Zeile inline
  ein Stern-Rating + Notiz-Feld (baut auf der bestehenden Programm-Tabelle aus A5 auf).
- **„Meine besuchten Konzerte" / „Konzert-Tagebuch":** eigene Seite unter „Konto" (neben „Meine
  Beiträge"), chronologische Liste mit eigenen Bewertungen/Notizen.
- **Onboarding-Brücke:** Auch ohne Person-Verknüpfung nutzbar — und je mehr Besuche, desto eher
  greift der Claim-Trigger (Block 3): „Du warst an Konzerten der Feldmusik X — bist du …?".

**Datenschutz (passt zu Block 2):** Besuch + Eindruck sind **privat by default**; nichts davon ist
öffentlich sichtbar. Aggregierte/öffentliche Auswertung nur opt-in.

**Entscheide (2026-06-29):**
- **(2) Stück-Eindruck wird GETRENNT von der Video-Bewertung geführt.** Live-Eindruck (privat) ≠
  Aufnahme-Bewertung (öffentlich). Spätere optionale Aggregation zu einem „Stück-Gesamteindruck"
  bleibt möglich, ist aber nicht Teil v1.
- **(4) Sichtbarkeit: privat by default, später teilbar.** Tagebuch/Notizen/Bewertungen sind
  zunächst rein privat; in einer späteren Stufe können **einzelne** Einträge optional „für Freunde"
  sichtbar gemacht werden → speist die Vernetzungs-Funktion (Freunde) und das Wachstum. Kein
  öffentlich-aggregiertes Rating in v1.

**Noch offen (kleiner, technisch):**
- **(1)** Eigene `KonzertBesuch`-Tabelle (Empfehlung, wegen Privatheit) vs. Wiederverwendung der
  bestehenden Zuhörer:in-Verknüpfung. *(Tendenz: eigene Tabelle.)*
- **(3)** Bewertungs-Skala — Vorschlag **1–5 Sterne** (konsistent mit der bestehenden Video-
  Bewertung, damit eine spätere Aggregation überhaupt möglich ist). *(Zu bestätigen.)*

**Was „Tagebuch" konkret heißt (Klärung 2026-06-29):** Analogie **Letterboxd (Filme) / Untappd
(Bier), aber für Blasmusik-Konzerte**. Eine persönliche Schicht über den (ohnehin vorhandenen)
Konzerten:
- Konzert als **„war dabei"** markieren (1 Klick, privat).
- Je gehörtem **Stück** optional **Sterne + Notiz** („Gänsehaut im 4. Satz", „Programm zu lang").
- Daraus wächst **„Mein Konzert-Jahr"**: persönliche Historie — „2026: 11 Konzerte, Lieblingsstück
  X, meistgehörte:r Komponist:in Y, beste Band Z".
- *User-Story:* Anna geht ans Jahreskonzert der Feldmusik. Auf dem Heimweg öffnet sie HarmoniQ,
  tippt „war dabei", gibt „Lord of the Rings" 5⭐ und notiert sich etwas. Monate später blättert
  sie durch ihr Konzert-Jahr — **und genau dieses Zurückblättern + das nächste Eintragen ist der
  Wiederkehr-Grund**, den das bloße Browsen nicht liefert.

**Validierung (User 2026-06-29):** Der User **führt diese Liste besuchter Konzerte heute schon in
Excel**. → Das Tagebuch ist **belegter Eigenbedarf**, kein spekulatives Feature („Founder-User-Fit").
Implikation: Ein **Import/Erfassung der bestehenden Excel-Historie** wäre ein starker erster
Mehrwert (eigene Vergangenheit sofort drin). *(Als möglicher Bestandteil vormerken.)*

**Zugang: Login vs. Cookie (Klärung/Entscheid 2026-06-30):** Das Tagebuch **erfordert Login** — ein
persönliches Archiv, das über Jahre wächst, lässt sich mit einem Cookie nicht versprechen (geht bei
Cookie-Löschung/Gerätewechsel verloren). Das deckt sich mit dem User-Ziel **„zum Login motivieren"**.
Aber um die Einstiegshürde zu senken: **Cookie-basierter „Schnupper-Eintrag"** — anonym darf man
**ein** Konzert markieren / einen Stern setzen, dann Prompt **„Damit dein Tagebuch erhalten bleibt:
anmelden"**; beim Login wird der Cookie-Stand **ins Konto übernommen**. Das nutzt das bereits
vorhandene Muster (anonymer Cookie-Vote → bei Login verknüpft, siehe `Spezifikation.md` §3) und
macht den Login zum natürlichen nächsten Schritt statt zur Eingangshürde.

### 4.2 Wiederkehr-Schleife / Benachrichtigungen (Entwurf 2026-06-29)
*Mechanismen, die User zurückbringen, OHNE dass sie es sich aktiv vornehmen. Kanal: E-Mail-Digest
und/oder **PWA-Push** (PWA-Basis steht, A7). Alles opt-in, frequenzbegrenzt.*
- **Vor dem Konzert (Entdeckung):** „Nächste Woche spielt <deine Band> / ein Verein in deiner Nähe
  — hingehen?" → speist sich aus den **Crawler-Konzert-Funden** (siehe 4.3) → führt zum Besuch.
- **Nach dem Konzert (Tagebuch):** Tags darauf „Warst du gestern bei <Konzert>? → eintragen &
  bewerten." → füllt das Tagebuch (und damit die Claim-Evidenz, Block 3).
- **Inhalt:** „Neues Video deiner Band" / „Zu einem Stück, das du 5⭐ gabst, gibt es eine neue
  Aufnahme."
- **Sozial:** „Dein:e Freund:in war am gleichen Konzert" / Aushilfe-Anfrage / Freundschaftsanfrage.
- **Flywheel:** *entdecken (Crawler) → besuchen → eintragen (Tagebuch) → Anstoß zum nächsten →
  wiederholen.* Das ist die eigentliche Bindung; einzelne Features sind nur Stationen darin.

**Beschluss v1 (2026-07-04, ausdiskutiert — Umsetzung offen):**
- **Format:** *ein* **Wochen-Digest** („Deine Woche in der Blasmusik", z. B. So-Abend), gebündelt statt
  ereignisgesteuert → einfaches Frequenz-Cap (max. 1/Woche), geringstes Abmelde-Risiko.
- **Kanäle:** **E-Mail + PWA-Push** (beide v1). *Eine* Digest-Zusammenstellung, zwei Kanal-Adapter.
  Push braucht zusätzlich: Service-Worker-Push-Handler, gespeicherte `PushSubscription`, **VAPID-Keys**,
  Web-Push-Lib (iOS: „zum Homescreen").
- **Trigger v1** (alle band-/follow-basiert, kein Zwang zu Geocoding):
  - **B** Tagebuch-Nudge: Konzert deiner/gefolgter Band letzte Woche **ohne** eigenen KonzertBesuch →
    „Warst du dabei? → eintragen." (Herz von Phase 1: füllt Tagebuch + Claim-Evidenz.)
  - **C** „Neues Video deiner/gefolgter Band" (nutzt Video-`createtime` + Band-Bezug).
  - **A** „Kommende Konzerte deiner/gefolgter Bands" — durch **Folgen** erst für Zuhörer:innen sinnvoll.
  - **F** „Konzerte in deiner Nähe" (Crawler-Funde): Nähe aus **privat gespeichertem, vergröbertem
    letztem Standort** der Person (opt-in) bzw. Fallback **Heimat-PLZ**; sonst aus den Orten der
    Band-/Follow-Konzerte. (Server-Job hat keinen localStorage-Standort → braucht dieses private Datum.)
- **Neue „Band folgen"-Beziehung** `BandInteresse` (Person ↔ Band, **privat**, kein öffentlicher
  Fan-Zähler, **kein** Roster-Eintrag): „Band folgen"-Button auf der Band-Seite + „Bands, denen ich
  folge" unter „Meine Person". Trigger-Quelle = **Mitgliedschaft ∪ Follow ∪ implizit** (≥2 besuchte
  Konzerte / hoch bewertetes Stück der Band). Mitgliedschaft impliziert Folgen (Union).
- **Consent (DSG):** Opt-in **beim Onboarding, Default an**, klar beschriftet, granulare
  Präferenzseite unter „Konto", **tokenbasierte One-Click-Abmeldung** (ohne Login).
- **Kanäle unabhängig wählbar (wichtig):** E-Mail und PWA-Push sind **getrennte Schalter** — der User
  kann **nur Push** (Infos aufs Smartphone), **nur E-Mail**, **beides** oder **keins** wählen. Die
  Digest-Zusammenstellung ist kanal-neutral; jeder aktive Kanal-Adapter versendet sie eigenständig.
  Präferenz-Struktur daher **pro Kanal** (nicht ein globales Ein/Aus): `EmailAktiv` / `PushAktiv`
  (später ausbaubar zu Toggles pro Trigger-Typ). Push wählbar erst, wenn auf dem Gerät eine
  Push-Berechtigung erteilt/`PushSubscription` vorhanden ist; die One-Click-Abmeldung im Mail-Footer
  betrifft nur den E-Mail-Kanal.
- **Architektur:** geplanter Hintergrund-Job (täglich, sendet pro User nur bei fälliger Kadenz +
  Opt-in + vorhandenem Inhalt) → Digest-Zusammenstellung → **`BenachrichtigungGesendet`-Log**
  (User, Typ, EntitätsId) gegen Wiederholung/Leer-Digests → Versand über bestehende Mail-Infra + Push.
- **Neue Daten (bei Umsetzung migrieren, dann `Spezifikation.md` synchronisieren):** `BandInteresse`;
  privater vergröberter Person-Standort + Heimat-PLZ (opt-in); Benachrichtigungs-Präferenzen;
  `BenachrichtigungGesendet`; `PushSubscription`.
- **Prod-Hinweis E-Mail:** Deliverability braucht korrektes **SPF/DKIM** auf der Domain (sonst Spam).

### 4.3 Crawler-Konzert-Empfehlungen „Was als Nächstes besuchen?" (User-Idee 2026-06-29)
Der Crawler findet ohnehin **kommende Konzerte** (KKL/EMF/Vereinsseiten). Daraus eine
**Empfehlungs-/Entdeckungs-Funktion**: „Spannende Konzerte demnächst" — gefiltert nach Region,
besuchten Bands, gehörten Komponist:innen/Stücken (aus dem Tagebuch). → Beantwortet die Frage
„wo soll ich hin?" und ist der **Einstieg in den Flywheel** (4.2). Verbindet Entdeckung (Videos +
künftige Konzerte, die der User als Reiz bestätigt hat) mit dem persönlichen Mehrwert.

**„Wissen wir, was in der Nähe ist?" (Klärung 2026-06-30):** Teilweise — und das bestimmt das
realistische v1:
- **Konzert-Ort:** Der Crawler erfasst den **Ort** (Lokal/Stadt) als Text, **nicht** als
  Geokoordinate. Eine **Region/Kanton**-Zuordnung ist machbar (Crawler-Spec C4 „Ort→Kanton" geplant);
  **exakte km** bräuchten **Geocoding** der Lokale.
- **User-Standort:** anonym unbekannt (GPS nur per **opt-in**-Browser-Geolocation, datenschutz-
  sensibel; IP grob). Eingeloggt: **Heim-Region im Profil** oder aus der eigenen Band abgeleitet.
- **Entscheid v1 (revidiert 2026-07-01):** **Datum ist der Leit-Sinn, Distanz ein Filter-Werkzeug.**
  Grund: die Konzert-Abdeckung ist regional lückenhaft → nach km zu *sortieren* würde falsche Nähe
  suggerieren.
  - **Startseite „Demnächst"** = **nur nach Datum** sortiert (nächste zuerst). Die km-Anzeige ist rein
    **dekorativ** („· 12 km") und erscheint **nur, wenn der Standort ohnehin bekannt ist** (frühere
    Freigabe / PLZ aus localStorage) — **kein** Standort-Prompt, **keine** Umsortierung.
  - **`/konzerte`** = echter **Distanz-Filter**: Bezugspunkt via **Browser-Geolocation (opt-in, ideal
    mobil)** ODER **PLZ-Eingabe** (Fallback Desktop / bei Ablehnung; via Nominatim `postalcode` + `ch`).
    Dann **Radius-Filter (10/25/50/100 km)** + km-Spalte, innerhalb des Radius nach Distanz sortiert.
    Bezugspunkt wird in **localStorage** gemerkt (nicht serverseitig gespeichert → datensparsam;
    Distanz client-seitig berechnet). Konzerte ohne Lokal-Koordinaten werden bei aktivem Filter
    ausgeblendet (mit Hinweis).
  - **Standort-Automatik (2026-07-04):** Ist die Geolocation-Berechtigung bereits erteilt, wird der
    Standort beim Seitenaufruf **still** aktualisiert (kein erneuter Prompt; `navigator.permissions`
    prüft den Status, gecachter Fix bis 10 min, ~1 km genügt) — man muss „Mein Standort" nicht neu
    klicken. Ohne Berechtigung: gespeicherter Bezugspunkt bzw. Button/PLZ.
  - **Voraussetzung:** Lokal-Koordinaten. Admin-Batch **„Koordinaten via Nominatim ergänzen"**
    (rate-limitiert ~1/s) füllt bestehende `Lokale`. Kein Profilfeld nötig (Standort/PLZ deckt
    eingeloggt wie anonym ab).

**Entscheid (User 2026-06-30): eigene `Lokal`-/Veranstaltungsort-Entität** (→ Datenmodell-Änderung,
bei Umsetzung `Spezifikation.md` nachführen). Ersetzt den heutigen Freitext-`Ort` am Konzert durch
eine Referenz auf `Lokal`.
- **Felder:** Name (z. B. „KKL Luzern"), optional Saal/Detail, Adresse, Stadt, **Kanton/Region**,
  **Koordinaten (lat/lng)**, optional Webseite.
- **Nutzen:** (1) Region-Filter & später Distanz für „Demnächst"; (2) **Karte unten auf der
  Konzert-Detailseite**; (3) Gruppierung „Konzerte an diesem Lokal" („was läuft im KKL"); (4)
  Dublettenfreiheit (find-or-create über Name/Stadt + Alias, analog Band/Stück).
- **Migration:** bestehende Freitext-Orte einmalig parsen → `Lokal` find-or-create; Crawler mappt
  künftige Funde aufs `Lokal` (KKL-Importer kennt Saal/Stadt bereits).
- **Geocoding:** Koordinaten via **Nominatim/OpenStreetMap** (gratis, kein Key) aus der Adresse;
  Karte via **Leaflet + OSM-Tiles** (kein API-Key). Öffentliche Orte → datenschutz-unkritisch.
- **`LokalAlias` (Entscheid User 2026-06-30):** Alternativ-Namen-Tabelle analog `BandAlias`/
  `StueckAlias` (z. B. „KKL" / „Kultur- und Kongresszentrum Luzern" / „KKL Luzern"). Find-or-create
  matcht Name **oder** Alias → weniger Dubletten beim Import.
- **Orte-Merge im Admin (Entscheid User 2026-06-30):** Merge-Funktion für `Lokal` analog
  `StueckMergeService`/Band-Merge: hängt alle Konzerte vom Quell- aufs Ziel-Lokal um, füllt
  Ziel-Lücken (Koordinaten/Adresse/Kanton), sichert Quell-Name + dessen Aliase als `LokalAlias`,
  löscht die Quelle. Button im Admin (Lokale-Verwaltung).
- **Reichweite:** Karte/Geocoding sind optional und nachrüstbar; die Entität + `LokalAlias` +
  Kanton-Zuordnung zuerst (deckt Region-Filter + Dedup ab), Koordinaten/Karte/Merge-Komfort danach.

### 4.4 Feed-Cold-Start: „Bands folgen" + Bands in der Nähe (Idee/Entscheid 2026-07-05)

> **Stand (umgesetzt 2026-07-05):** Folge-Seite `/account/bands-folgen` (eigene Bands auto-inkludiert,
> Vorschläge nah/beliebt, Suche, Standort-Button, Folgen-Toggle) + **Leerer-Feed-Aufruf** auf der
> Startseite (wenn Person aber keine Bands) + **`Band.HeimatLokalId → Lokal`** (Migration
> `BandHeimatLokal`). **Band-Admin: Feld „Heimatort/Probelokal"** (Lokal-Autocomplete, Find-or-create
> + Geocoding beim Speichern) umgesetzt. **Offen:** Band-Standorte im Bestand **befüllen** (Backfill,
> s. u.), damit „Bands in der Nähe" echte Distanzen zeigt; direkte Weiterleitung nach Person-Anlage
> auf die Folge-Seite (aktuell greift der Startseiten-Aufruf beim nächsten Home-Besuch).
>
> **Backfill Band-Standorte — UMGESETZT (2026-07-05):** Admin-Seite `/admin/band-standorte`
> (Link aus `/admin/bands`), **pro Band einzeln** entscheidbar: je Zeile (A) Button „häufigstes
> Konzert-Lokal (N×)", (B) Button „aus Bandnamen geratene Ortschaft", (C) freies Textfeld — dann
> „Übernehmen" (Find-or-create Lokal + Geocoding + Verknüpfung). Vorbelegung: wiederkehrendes
> Konzert-Lokal (≥2×) sonst Namens-Vorschlag (verhindert falsches Kerkrade bei Einmal-Wettbewerben).
> Verifiziert (Sarnen inkl. Koordinaten). Kanton füllt bei Bedarf der bestehende Lokal-Koordinaten-Batch.
>
> **Backfill-Plan Band-Standorte (2026-07-05):** Sobald eine Band ein Heimat-`Lokal` hat, füllt der
> bestehende `LokaleAdmin`-Batch „Koordinaten via Nominatim ergänzen" die Koordinaten. Es fehlt also
> nur das **Zuordnen eines Heimatorts** je Band. Signale (nach Verlässlichkeit): (1) **häufigstes
> Konzert-Lokal** der Band (stark; hat oft schon Koordinaten); (2) **Ortschaft aus dem Bandnamen**
> (Präfixe wie „Musikgesellschaft/Feldmusik/Blasorchester/Stadtmusik/Harmonie/Brass Band/Jugendmusik…"
> strippen → Rest = Ort → Find-or-create Lokal); (3) **Crawler/EMF** (vereine-API/Webseite hat den
> Ort → HeimatLokal beim Import setzen). Empfehlung: Admin-Batch „Heimatorte ergänzen" = (1) automatisch,
> (2) als Fallback mit Review; Einzelkorrektur über das neue Band-Admin-Feld. Danach Koordinaten-Batch.
*Der „Für dich"-Feed (4.2) ist erst wertvoll, wenn man Bands folgt. Darum den Feed aktiv „anfüttern".*

**Ist-Stand (verifiziert im Code):** „Folgen" existiert bereits (`BandInteresse`); der Feed nutzt
„Mitglied ∪ gefolgte Bands". `Person.StandortLat/Lng` (Heimat-Standort) existiert; `Lokal` hat
Lat/Lng/Kanton; „Konzerte in deiner Nähe" (`_feed.Nahe`) funktioniert. **Lücke:** `Band` hat
**keinen Ort** → „Bands in der Nähe" (noch) nicht möglich.

**Entscheide/Empfehlungen:**
- **Onboarding-Schritt „Folge Bands"** direkt nach dem Person-Claim (Block 3): eigene Mitglieds-
  Bands **vorausgewählt/auto-gefolgt**; dazu **~3 Vorschläge** (nah / beliebt / Suche). **Politik
  (Entscheid User 2026-07-05): 3 vorschlagen, weiches Minimum 1, überspringbar** (niederschwellig,
  kein harter Zwang).
- **Leerer-Feed-Zustand:** hat man keine gefolgten/Mitglieds-Bands, **statt leerer Sektion** ein
  Aufruf „Folge Bands, um Neuigkeiten zu sehen → Bands in deiner Nähe / entdecken".
- **Standort opt-in:** `geo.js` + `Person.StandortLat/Lng` sind da → „Bands in der Nähe" per
  Distanz, sobald Bands einen Ort haben. Opt-in, kein Zwang.
- **Datenmodell (einzige nötige Ergänzung): `Band.HeimatLokalId → Lokal`** (optional). Das Lokal
  darf ein echtes **Probelokal ODER nur die Ortschaft** sein (Ortszentrums-Koordinaten reichen für
  die Distanz — User-Punkt bestätigt). **Kein** neues Koordinaten-Feld auf `Band` — `Lokal` liefert
  Lat/Lng/Kanton/Alias/Geocoding; Distanz via bestehender Haversine (Home.razor). Kanton hilft
  zusätzlich dem „Demnächst"-Region-Filter (4.3). Herkunft: Band-Admin, Crawler (EMF/Webseite hat
  Ort) oder Ableitung aus dem häufigsten Konzert-Lokal der Band. (→ `Spezifikation.md` bei Umsetzung.)

---

## 5. Rollen & Berechtigungen

*Heute: Anonym / User / Admin. Frage aus der Eröffnung: braucht es eine **Zwischenstufe**?*

**Grundsatz-Entscheid (2026-06-29):** Ja, es braucht die Zwischenstufe **Band-Admin** (UI:
„Verein verwalten"). Der globale Admin ist sonst der Flaschenhals für Datenpflege **und** für die
Claim-Verifizierung sichtbarer Rollen (Block 3). Die Stufe wird **jetzt spezifiziert**, **umgesetzt**
wird sie, sobald die ersten echten Vereine an Bord kommen.

### 5.1 Rollen-Modell (Ziel)
| Stufe | Geltung | Darf |
|---|---|---|
| **Anonym** | global | lesen (nur öffentlich), Cookie-Vote |
| **User** | global | + eigene Person/Claim, Konzert-Tagebuch (privat, Block 4), Freunde |
| **Band-Admin** | **pro Band** | + die **eigene Band** pflegen (s. 5.2), Claims der Band bestätigen |
| **Admin** | global | alles, inkl. Importe/Crawler, Band-Admins ernennen |

### 5.2 Was darf ein Band-Admin (band-skopiert)
- **Band-Stammdaten** pflegen: Name/Aliase, Logo/Bild, Beschreibung, Links, Kategorie/Stärkeklasse.
- **Mitglieder & Vorstand** verwalten: Personen hinzufügen/entfernen, Funktion (Dirigent:in,
  Präsident:in …), Instrument/Stimme, Zeiträume (= `BandMitgliedschaft`).
- **Konzerte der Band** anlegen/bearbeiten inkl. Programm (Stücke, Mitwirkende).
- **Claims bestätigen:** Beansprucht jemand eine **sichtbare Rolle** seiner Band (Dirigent:in/
  Vorstand, Block 3), kann der Band-Admin „bestätigen" → schließt das Verifizierungs-Gate.
- **„FEHLER MELDEN"-Meldungen** zur eigenen Band zuerst bearbeiten (vor Eskalation an globalen Admin).
- **Konzerte löschen / mergen — nur „Solo-Band-Konzerte" (Entscheid 2026-06-29):** Ein Band-Admin
  darf ein Konzert **löschen (nach Rückfrage)** oder **innerhalb der Band mergen**, **sofern nur
  seine eigene Band beteiligt ist** (alle `KonzertBand`/Programm-Bands gehören dieser einen Band).
  Sobald **eine weitere Band** beteiligt ist (z. B. Gemeinschaftskonzert, Wettbewerb mit mehreren
  Bands), ist Löschen/Merge gesperrt → nur globaler Admin (betrifft fremde Daten). Begründung: Eine
  Band ist über `KonzertBand` **und** über die Programm-Stücke (`KonzertStueck.Band`) beteiligt;
  „Solo" heißt, keine dieser Referenzen zeigt auf eine fremde Band.
- **NICHT:** andere Bands, globale Daten, Importe/Crawler — und **keine privaten Nutzerdaten**
  (Konzert-Tagebuch/Notizen anderer User bleiben unsichtbar; Band-Admin pflegt nur öffentlich
  kuratierte Banddaten).

### 5.3 Wie wird man Band-Admin? (Bootstrapping)
- **Erste:r Band-Admin je Band:** durch **globalen Admin** ernannt/freigegeben — auf Antrag eines
  verifizierten Funktionärs (Dirigent:in/Vorstand-Claim). Einfach, kontrolliert, kein Henne-Ei-Problem.
- **Weitere Band-Admins:** bestehende Band-Admins können Vereinskolleg:innen **selbst** zu
  Band-Admins der gleichen Band machen (skaliert ohne globalen Admin).
- *(Optional später)* **Self-Service-Antrag** „Ich betreue diesen Verein" → Verifizierung per
  E-Mail-Domain der Vereins-Website oder Bestätigung eines bestehenden Band-Admins.

#### 5.3.1 Crawler-gestützte Band-Admin-Einladung (Idee 2026-06-29) — verbindet 3 Blöcke
Der **Crawler erfasst bereits** Vorstands-/Muko-Funktionäre **mit Funktion und E-Mail** (Leitung-
Funde mit EMail-PersonLink, siehe `Spezifikation-Crawler.md` / `VorstandCrawlen`/`MukoCrawlen`).
Damit lässt sich der erste Band-Admin **aktiv finden statt abwarten**:
- **Kandidat = offizielle Vereins-/Funktions-Adresse**, bevorzugt die **offizielle Vereins-E-Mail
  oder die Präsidiums-/Kontakt-Adresse** (nicht eine private Privatadresse, s. Datenschutz unten).
- **Einladungs-Flow:** HarmoniQ sendet eine **Einladung** „Du bist als Präsident:in von <Band>
  gelistet — verwalte deinen Verein auf HarmoniQ" mit Einmal-Link. Klick + Login = **gleichzeitig
  Verifizierung** (Zugriff auf die offizielle Adresse beweist Berechtigung) **und** Band-Admin-
  Bootstrapping **und** der Person-Claim (Block 3) ist bestätigt.
- **Drei-in-eins:** Das schließt zugleich (1) **Block 3** Verifizierungs-Gate, (2) **Block 5**
  Bootstrapping und (3) **Block 9** Verbreitung (personalisierte Ansprache genau der richtigen
  Person) → siehe Querverweis in Block 9.
- **Datenschutz/Recht (WICHTIG, → Block 2):** Unaufgeforderte E-Mail an **private** Personen-
  Adressen ist heikel (CH-DSG + UWG Art. 3 lit. o, Spam). Daher: **nur offizielle Vereins-/Amts-
  Adressen** anschreiben, klarer Absender/Zweck, einfacher Opt-out/Lösch-Link, keine Massen-
  Aussendung an geharvestete Privatadressen. Vom Crawler ist die Adress-Herkunft bekannt → vor
  Versand nach „offiziell vs. privat" filtern.

### 5.4 Moderation, Audit & Konflikt
- **Änderungs-Historie/Audit-Log** je Entität (wer hat wann was geändert) — Voraussetzung für
  Vertrauen, sobald mehrere Hände editieren; ermöglicht **Rückgängig** durch Admin/Band-Admin.
- **Melde-Pfad:** „FEHLER MELDEN" (bereits vorhanden, A5/A6) → Band-Admin der betroffenen Band →
  globaler Admin als Eskalation.
- **Blast-Radius begrenzt:** Band-Admin-Rechte sind band-skopiert → Vandalismus/Fehler wirken nur
  lokal; granulare Identität (benannte Rolle) senkt die Missbrauchswahrscheinlichkeit.
- **Konkurrierende Bearbeitung:** „last write wins" + Historie genügt anfangs (geringe Parallelität);
  feineres Locking erst bei Bedarf.

**Datenmodell-Auswirkung (→ `Spezifikation.md` bei Umsetzung):** neue Verknüpfung
**`BandAdministrator`** (User ↔ Band, ernannt-von, ernannt-am) — **bewusst getrennt** von
`BandMitgliedschaft.Funktion` (Präsident:in/Dirigent:in zu sein heißt nicht automatisch
App-Verwaltungsrecht). Optional **Audit-Log**-Tabelle.

**Entscheide/Stand:**
- **(c) Löschen/Merge geklärt:** erlaubt für **Solo-Band-Konzerte** (nach Rückfrage), gesperrt
  sobald eine fremde Band beteiligt ist (siehe 5.2).
- **(a) Bootstrapping:** Tendenz **„globaler Admin ernennt erste:n"** als sicherer Start, **plus**
  die **Crawler-gestützte Einladung** (5.3.1) als aktiver, verifizierender Wachstums-Pfad. Noch zu
  entscheiden: ob der Self-Service-Antrag (E-Mail-Domain) zusätzlich von Anfang an dabei ist.
- **(b) Audit-Log-Umfang offen:** Empfehlung klein starten (Band/Konzert/Person/Mitgliedschaft).

---

## 6. Navigation & Selbsterklärbarkeit

**Offen:**
- Finden sich User zurecht? Braucht es **mehr Erklärung** (Tooltips, Hilfe-Texte) oder soll es
  **selbsterklärend** sein? *(Leitlinie: selbsterklärend zuerst; Erklärung nur dort, wo Begriffe
  fachspezifisch sind — z. B. „Stärkeklasse", „Selbstwahlstück".)*
- *(Ergänzung)* Konsistente Begriffe & Wege: heißt dasselbe überall gleich? Wie viele Klicks von
  Startseite zu „mein Verein"?
- *(Ergänzung)* **Mobile zuerst:** Vereinsmitglieder nutzen primär das Handy. Ist alles auf dem
  Smartphone gut bedienbar (PWA „zum Startbildschirm")?

**Entscheid (User 2026-06-29): SMARTPHONE-FIRST.** Die mobile Ansicht ist der **Maßstab**, für den
optimiert wird — nicht der PC-Browser. Der Desktop ist sekundär und v. a. für **Band-Admins**
(Datenpflege) praktisch. → Wireframes/Designs primär mobil entwerfen; Layout-Entscheide am
Smartphone-Verhalten messen: **Bottom-Navigation** statt Seitenleiste, Daumen-Bedienung, Tabellen
→ Karten/Listen auf schmalem Screen.

### 6.1 Neuer Einstieg: Startseite + Navigation (Entwurf 2026-06-29)
*Ziel: Einstieg von „flache Lexikon-Listen" zu „mein Kontext + Entdeckung" verschieben — die
sichtbare Vorderseite des Flywheels (4.2). Entdeckung (künftige Konzerte + gute Videos) bleibt
prominent, führt aber in die persönlichen Sog-Features.*

**Startseite — eingeloggt (personalisiert, von oben nach unten):**
1. **Begrüßung + EIN klarer nächster Schritt** (zustandsabhängig): kein Person-Link → Claim-Banner
   (existiert); kürzlich wahrscheinlich besuchtes Konzert nicht eingetragen → „Warst du bei <X>?
   → eintragen" (Wiederkehr, 4.2).
2. **Mein Konzert-Jahr** (Tagebuch-Teaser, 4.1): „2026: 11 Konzerte · Lieblingsstück X" + letzte
   Einträge + „Konzert eintragen". Der persönliche Anker.
3. **Demnächst** (Entdeckung, Crawler 4.3): kommende Konzerte. **Entscheid (User 2026-06-29):**
   ist **für alle gut** (auch anonym) → **weit oben** platzieren; eingeloggt nach Region/deinen
   Bands personalisiert, anonym nach Region/allgemein. „Wo soll ich hin?"
4. **Deine Bands** (Neuigkeiten): neue Videos/Konzerte der Bands, denen du folgst / in denen du bist.
5. **Sehenswerte Aufnahmen** (Entdeckung): kuratierte/neue Videos — der vom User bestätigte Reiz.
6. *(später)* **Freunde-Feed:** was Freund:innen besucht/bewertet haben.
7. **QR-Code „weitergeben"** unten (beibehalten).

**Startseite — anonym (erster Eindruck):**
- **Hero mit Nutzer-Nutzen-Satz** statt „the music database" — z. B. *„Entdecke Blasmusik-Konzerte,
  merke dir, was du besucht hast, und finde die Menschen dahinter."* (→ Block 1 Ein-Satz-Positionierung).
- Demnächst-Konzerte (Entdeckung) + sehenswerte Videos + Featured Bands/Komponist:innen.
- **CTA führt mit dem persönlichen Nutzen:** „Führe dein eigenes Konzert-Tagebuch — kostenlos."
  (statt „bewerten/Daten beitragen" — das ist sekundär).
- Statistik-Kacheln bleiben, aber **kleiner/weiter unten** (nett als „Beleg", nicht als Aufmacher).

**Navigation — Neugruppierung (entwirrt Komponist:innen vs. Personen):**
- **Primär (Entdecken + mein Kontext):** Startseite · Konzerte (inkl. Kalender/Demnächst) ·
  **Mein Tagebuch** *(neu, prominent eingeloggt)* · Videos.
- **Nachschlagen (Lexikon, sekundär):** **Komponist:innen** · Bands · Personen · Stücke.
  → **Entscheid (User 2026-06-29): „Komponist:innen" bleibt eigener Menüpunkt** (nicht in „Personen"
  auflösen), weil die Liste **anders sortiert** wird (viel gespielte zuerst) und der häufigste
  Browsing-Einstieg ist. Technisch sind Komponist:innen weiterhin Personen mit Rolle — die begriffliche
  Nähe (A6) klärt man über Beschriftung/Filter, nicht über Zusammenlegen.
- **Konto** (existiert): Mein Tagebuch · Meine Person · Freunde · Profil.

**Mobile-Layout (Maßstab, smartphone-first):**
- **Bottom-Navigation** mit 5 Tabs: **Start · Konzerte · Tagebuch · Videos · Mehr**. „Mehr" enthält
  die Gruppe „Nachschlagen" (Komponist:innen · Bands · Personen · Stücke) + Konto (Meine Person,
  Freunde, Profil, Logout).
- **Eine Spalte**, Reihenfolge = Priorität: Anstoß → **Demnächst (oben, für alle)** → Mein
  Konzert-Jahr (kompakt) → Deine Bands → Sehenswerte Aufnahmen → Footer (Statistik/QR).
- Tabellen → **Karten/Listen** auf schmalem Screen; große Touch-Flächen.
- Desktop = dasselbe Inhaltsmodell mit Seitenleiste; sekundär, v. a. für Band-Admins (Pflege).

**Leitprinzip:** **vertiefen + fokussieren, nicht verbreitern** (Block 1.1). Keine neuen
Inhaltstypen; bestehende Inhalte (Konzerte/Videos) in einen kontext- & entdeckungs-zentrierten
Rahmen stellen.

**Offen:** genaue Reihenfolge/Anzahl der Startseiten-Blöcke; ob „Nachschlagen" eingeklappt oder
flach bleibt; Wording der Filter-Chips; Mobile-Stapelung (Block 6 Mobile-Punkt).

**Entscheid:** *(offen — Wireframe-Skizze unten / im Chat zur Diskussion)*

### 6.2 Anonyme Startseite — Positionierungs-Varianten (mobil, Entwurf 2026-06-30)
*Der erste Eindruck = die Positionierungs-Wette (Block 1, „der eine Satz"). Drei Aufhänger getestet:*
- **A · Entdeckung („Blasmusik-Konzerte in deiner Nähe"):** niedrigschwellig, für alle sofort
  nützlich, CTA = stöbern ohne Konto. Risiko: wirkt wie reiner Veranstaltungskalender, zeigt den
  einzigartigen Wert nicht.
- **B · Tagebuch („Dein Konzert-Tagebuch"):** führt mit dem **validierten** persönlichen Bedarf
  (Letterboxd-Logik), CTA = kostenlos starten. Stärkste Differenzierung + Wiederkehr; verlangt früh
  Commitment.
- **C · Vernetzung („Die Menschen hinter der Blasmusik" + Such-mich):** stark für aktive
  Musiker:innen („du bist hier schon"); nischiger + datenschutz-sensibler → eher sekundär.

**Empfehlung:** **B als Überschrift + A direkt darunter** (Differenzierung *und* sofortiger Nutzen);
**C** als zweiter Button („finde dich"), nicht als Aufmacher.

**Ein-Satz-Positionierung — Kandidaten (Block 1, zu entscheiden):**
- (B-led) „Merk dir die Blasmusik-Konzerte, die du besuchst — und entdecke, wo du als Nächstes hingehst."
- (A-led) „Entdecke Blasmusik-Konzerte in deiner Nähe und die Menschen dahinter."

**Entscheid Positionierung (User 2026-06-30): B+A** — Überschrift „Dein Konzert-Tagebuch", darunter
„Demnächst", „finde dich" als zweiter Button. Login wird **bewusst motiviert** (mittelfristig
wichtig). Offene Detailpunkte dazu: „Nähe"-Frage (→ 4.3) und Tagebuch-Zugang/Cookie (→ 4.1).

---

## 7. Design, Gefühl & Markenauftritt

**Offen (aus der Eröffnung):**
- **Vereinheitlichung der Darstellungen:** z. B. Stück-Liste bei Komponist:innen ebenfalls als
  **Tabelle** (wie andere Listen)? Welche Ansichten weichen heute ab?
- **Gesamteindruck des Designs:** Was müsste sich ändern? *(Ich liefere nach dem Live-Durchlauf
  konkrete Befunde.)*
- **Animiertes Startbild:** Beim Start ein animiertes Bild — frisch oder eher altbacken?
- **Bild statt Warte-Bildschirm:** Könnte dieses Bild den initialen „Reaktiv-werden"-Moment
  (siehe Block 8) überbrücken statt eines leeren Lade-Screens? *(Hypothese: ja — ein bewusster,
  hübscher Splash überdeckt die Blazor-Server-Verbindungszeit elegant.)*
- **Ton beim Start:** Kurzer Ton — oder bewusst still (kein Aufsehen in Probe/Büro)? *(Hypothese:
  standardmäßig still; Ton höchstens als optionales, nie automatisches Element.)*

**Einschätzung & Entscheid-Vorschläge (2026-06-30, gestützt auf Live-Eindruck A1–A6):**
- **Gesamteindruck = bereits gut.** Dunkles Violett/Gold-Theme, klare Typografie, stimmiges Logo,
  aufgeräumt. **Kein Redesign nötig** — die echten Probleme sind *strukturell* (Einstieg/Nav →
  Block 6) und *Konsistenz* (A6), nicht ästhetisch. Leitlinie: **verfeinern, nicht neu erfinden.**
- **Vereinheitlichung (A6):** Eine **gemeinsame „Stück-Zeile"-Komponente** für alle Stück-Listen
  (Komponist-Werke, Konzert-Programm, Stücke-Liste): Titel · Komponist:in · Band · ggf. Bewertung,
  klickbar. **Smartphone-first (Block 6): Standard = Karte/Liste**, Desktop darf als Tabelle rendern.
  Beseitigt „Karten vs. Tabelle vs. einfache Liste"-Wildwuchs.
- **Animiertes Startbild:** **Dezent ja, aufdringlich nein.** Eine **kurze, subtile** Marken-
  Animation (Logo/Equalizer-Balken, <1 s, einmalig) wirkt frisch; ein **schweres/loopendes
  Hero-Video** wirkt altbacken und bremst die gefühlte Ladezeit → vermeiden.
- **Bild statt Warte-Bildschirm — JA, löst zugleich den Reaktivitäts-Befund (A4/Block 8):** Statt
  Platzhalter-**„0"** beim Verbindungsaufbau → **Skeleton-Screens** (Inhalts-Platzhalter) bzw. ein
  kurzer Marken-Ladezustand. Skeletons werden „schneller" empfunden als ein blockierender Splash und
  passen dazu, dass der **prerenderte Inhalt schon da ist** (A1). Empfehlung: Skeleton > Voll-Splash.
- **Ton beim Start:** **Standardmäßig still** (Proben/Büro/ÖV — unerwarteter Ton nervt, wirkt
  dated). Höchstens **optional/opt-in**, nie automatisch. Deckt sich mit der User-Hypothese.

**Entscheid (User bestätigt 2026-06-30):** Design verfeinern statt neu bauen; gemeinsame Stück-Zeile
(**Desktop Tabelle / Smartphone Karten**); dezente kurze Logo-Animation ok; **Skeleton/dezentes
Warte-Bild statt „0"** als Ladezustand; Start **ohne Ton**. *(Visuelle Details bei Umsetzung.)*

---

## 8. Performance & technischer Stack

*Befund des Users: „Am Anfang dauert es relativ lang, bis das Ganze reaktiv wird; danach gut."*

**Offen / zu analysieren:**
- **Ursache der Anfangsverzögerung:** Vermutlich (a) Railway **Cold Start** (Container schläft im
  günstigen Abo) und/oder (b) Aufbau des Blazor-Server-**SignalR-Circuits** + Prerender→Interaktiv-
  Übergang. → Erst messen, dann entscheiden.
- **Blazor Server vs. Client (WASM):** Client-seitig vermeidet die Circuit-Latenz pro Interaktion,
  hat aber einen **größeren initialen Download** (eher *langsamerer* erster Start). *(Hypothese:
  Wechsel auf reines WASM löst das Problem nicht zwingend; oft besser: Prerendering + „Auto"-Modus
  + Cold-Start beheben.)*
- **Railway-Abo:** Reicht das 5-$-Abo, oder braucht es das höhere für „immer warm" (kein Schlafen)?
  → Hängt direkt mit dem Cold-Start-Befund zusammen.
- *(Ergänzung)* **SEO-Kopplung:** Prerendering ist auch für Block 9 (Sichtbarkeit) entscheidend —
  Google muss die öffentlichen Inhalte sehen.

### 8.1 Analyse & Empfehlung (2026-06-30)
**Zwei getrennte Ursachen — nicht verwechseln:**
- **(A) Railway Cold Start:** Container war idle → muss erst hochfahren. Trifft nur die **erste**
  Anfrage nach einer Ruhephase; Symptom = lange Wartezeit/leere Seite beim allerersten Aufruf.
- **(B) Blazor-Server-Circuit-Aufbau:** Bei **jedem** frischen Seitenaufruf: `blazor.server.js`
  laden → WebSocket öffnen → Circuit herstellen → interaktiv re-rendern. **Beobachtet (A4): ~3 s
  auch bei warmem Server**, dabei zeigt die Seite kurz Platzhalter („0"), bis der Circuit steht.

**So unterscheiden (einfacher Selbsttest):**
- **Cold:** App ~20–30 min nicht anfassen, dann laden → deutlich langsamer/leer = Cold Start.
- **Warm:** sofort neu laden → wenn weiterhin ~2–3 s bis interaktiv = Circuit (B).

**MESSUNG (Browser, 2026-06-30) — überraschend eindeutig, weder A noch B ist Hauptursache:**
- 1. Aufruf: **TTFB 1474 ms**, DOMContentLoaded 1908 ms, load 2066 ms; `blazor.web.js` ab 1720 ms;
  SignalR-`negotiate` erst bei ~2056 ms, dauert nur **35 ms**.
- 2. Aufruf (sofort): **TTFB 1472 ms** — praktisch identisch.
- **Schluss:** **Kein Cold Start** (2. Aufruf gleich langsam) und **nicht der Circuit** (negotiate
  ist schnell + spät). Der Flaschenhals ist die **konstante Server-Antwortzeit von ~1,5 s bis zum
  ersten Byte** = **(C) Server-Render-/Query-Zeit**. Der Circuit kommt erst danach obendrauf
  (~0,4 s), fällt aber kaum ins Gewicht.

**(C) ist die eigentliche Ursache — im Code BESTÄTIGT (2026-06-30):** `Home.razor`
`OnInitializedAsync` (Zeilen ~300–367) führt **~13 einzelne DB-Queries streng sequenziell** aus
(jede ein eigener `await` → eigener Postgres-Round-Trip): 6× `CountAsync` (Zähler), die
Featured-Komponist:innen-Query (verschachteltes `Any` + `OrderByDescending(StueckBeitraege.Count)`
über alle 695 Personen — **die teuerste**), Featured-Bands, neue Videos, Kalender + 2–3
user-spezifische Lookups. ~13 Round-Trips × Latenz + schwere Aggregate = die gemessenen ~1,5 s.
**Konkreter Fix-Ansatz:**
- **Globale Aggregate cachen** (`IMemoryCache`, paar Minuten): die 6 Zähler + Featured-
  Komponist:innen + Featured-Bands sind **nicht user-spezifisch** und ändern sich selten → aus dem
  Hot-Path nehmen. Größter Hebel (entfernt ~8 Queries pro Aufruf).
- **Verbleibende unabhängige Queries parallelisieren** (mehrere Kontexte aus `IDbContextFactory`
  via `Task.WhenAll`) statt sequenziell.
- **Indizes prüfen** (z. B. `PersonRolle.Rolle`, `KonzertStueck.StueckId`); die teure
  Featured-Query ggf. materialisieren/cachen.
- Die per-User-Teile (Onboarding-Status, offene Anfragen) bleiben pro Request, sind aber günstig.

**Blazor Server vs. Client (WASM) — Empfehlung: bei Server bleiben.** WASM hat einen **größeren
Initial-Download** (mehrere MB .NET-Runtime/DLLs) → erster Start meist **langsamer**, nicht
schneller. WASM spart nur die Server-Roundtrips pro Klick (Vorteil bei hoher Latenz), aber HarmoniQ
ist **daten-/DB-lastig** und braucht den Server ohnehin. → Ein WASM-Wechsel **behebt „bis reaktiv"
nicht** und verschlechtert eher den ersten Eindruck.

**Wirksame Fixes (nach Hebel — nach der Messung priorisiert):**
1. **TTFB senken = größter Hebel (Ursache C):** (a) **Homepage-Statistik cachen** (ändert sich
   selten → In-Memory-Cache für ein paar Minuten); (b) **Query-Anzahl reduzieren** (Zähler in
   wenigen/aggregierten Statements statt vieler einzelner COUNTs; Indizes prüfen); (c) **App und
   Postgres in derselben Region** (Round-Trip-Latenz × Query-Zahl). Ein gecachter Homepage-Render
   sollte TTFB von ~1,5 s auf << 0,5 s drücken.
2. **Skeleton statt „0" (Block 7):** maskiert die Rest-Zeit (Circuit + verbleibende Render-Zeit) —
   billiger *gefühlter* Gewinn, aber **nicht** der eigentliche Fix (der ist Punkt 1).
3. **Streaming-Rendering:** schon gerenderten Teil sofort senden, langsame Teile nachstreamen →
   verteilt die 1,5 s, statt am Stück zu warten.
4. **Cold Start** (laut Messung aktuell **kein** Problem) — falls später doch: Warm-Halten via
   Uptime-Ping, billiger als Abo-Upgrade.
- **Streichen:** „Homepage-Daten im Prerender laden" als Circuit-Fix war die falsche Fährte — die
  Daten sind im Prerender, der Prerender selbst ist nur langsam (Ursache C).

**Railway-Abo:** **Erst messen, dann zahlen.** Fürs reine **Ausliefern** reicht wenig RAM
(~512 MB–1 GB) → 5-$-Tier wahrscheinlich genug, **sofern der Dienst warm bleibt** (nicht auf null
skaliert). Der **Crawler mit Playwright** braucht viel mehr RAM (512 MB → OOM, siehe
[[railway-playwright-rendering]]), läuft aber **on-demand** und nutzt für EMF inzwischen die
JSON-API statt Rendering. → Empfehlung: **nicht** vorschnell upgraden; zuerst (1)+(2)+(3) umsetzen;
ein größeres Abo nur, falls die Messung einen Cold Start zeigt, den der Ping nicht löst, oder falls
RAM für Crawler-Läufe knapp wird.

**Entscheid (2026-06-30, nach Messung):** Bei **Blazor Server** bleiben (WASM hilft nicht, die
1,5 s liegen server-seitig). **Hauptmaßnahme = TTFB senken** (Homepage-Statistik cachen + Queries
reduzieren + App/DB-Region). Skeleton + Streaming als Ergänzung. **Abo NICHT upgraden** — die
Ursache ist Code/Query-seitig, nicht die Plan-Größe (Messung zeigt: kein Cold Start). Nächster
konkreter Umsetzungsschritt: die Homepage-Aggregat-Queries im Code anschauen + cachen.

---

## 9. Verbreitung & Sichtbarkeit *(mittelfristige Hauptfrage)*

**Leitprinzip (User):** Größerer Verbreitungs-Effort erst, **wenn es „richtig gut" ist** — deckt sich
mit Block 1.1 („vertiefen + fokussieren vor verbreiten"). Verbreitung **verstärkt** das Produkt; ist
es noch schwach, verbrennt jeder eingeladene Verein seinen einmaligen ersten Eindruck.

### 9.1 Fundament — VOR jeder Outreach (billig, hoher Hebel)
- **SEO/Sharing-Lücken schließen (Befund A7):** `lang=de`, `meta description`, **Open-Graph-Tags**
  (`og:title/description/image/url`), Twitter-Cards, `canonical`, optional JSON-LD
  (`MusicEvent`/`Person`). **Voraussetzung für ALLE Kanäle** — jeder Kanal erzeugt geteilte Links;
  ohne OG-Tags sehen die in WhatsApp/Social „kaputt" aus.
- **Analytics (datenschutzkonform), koppelt an Block 1:** Ohne Messung blind. **Plausible/Umami**
  (cookielos, self-host möglich, DSG-freundlich). Kennzahlen: Besuche, Anmeldungen, **beanspruchte
  Profile**, Wiederkehrer, **Tagebuch-Einträge**. **Vor** der Outreach aufsetzen, um zu sehen, was wirkt.

### 9.2 Organisch / eingebaut (immer an, niedriger Aufwand)
- **Netzwerkeffekt „dein Verein ist schon da":** dank Crawler sind Vereine bereits abgebildet →
  **personalisierte** Links pro Band statt generischer Mail.
- **QR-Code** (vorhanden) + **Teilen-Buttons** mit schöner Vorschau (nach 9.1): Konzerte/Videos/
  „ich war dabei" sind teilbare Artefakte.
- **Flywheel** (4.2): Benachrichtigungen holen Leute zurück. **SEO organisch:** wer „Feldmusik X
  Konzert" googelt, findet HarmoniQ (sobald indexiert).

### 9.3 Aktive Outreach — Kanäle & Reihenfolge (die Hauptfrage)
Bewertung der Kanäle (warm → kalt):
- **(1) Warmes Netzwerk zuerst (inkl. JBL):** **JBL = Jugendblasorchester Luzern** — ein konkreter
  Verein, in dem die **Tochter des Users mitspielt** (kein Verband!). Idealer Pilot: persönlicher
  Draht, und **die Tochter könnte vor Ort Flyer verteilen** (Proben/Konzerte). **3–5 persönlich
  bekannte Vereine** als Pilot → werden die ersten **Band-Admins**, geben **ehrliches Feedback**
  (= zugleich der „5-Min-Test" aus 1.1) und seeden die Region. Warme Kontakte konvertieren weit
  besser als Kaltmail.
- **(1b) Flyer / Graswurzel offline:** Flyer mit **QR-Code** (Verbreitung ist schon angelegt) an
  Proben/Konzerten verteilen — über die Tochter bei JBL, später bei weiteren Pilotvereinen. Sehr
  niederschwellig, direkt bei der Zielgruppe, messbar via QR-Ziel-URL (eigener Kampagnen-Parameter).
- **(2) Crawler-gestützte Einladung (5.3.1):** halb-personalisiert, skalierbar, **selbst-
  verifizierend** (Mail an offizielle Adresse). Brücke zwischen warm und Masse. **Nur offizielle
  Vereins-/Amtsadressen**, nicht private (DSG/UWG, Block 2). Region für Region, **dort beginnen, wo
  die Daten am besten sind** (Innerschweiz/Luzern — JBL/EMF-Daten reich).
- **(3) Verband-Verstärkung (kantonale Blasmusikverbände / Schweizer Blasmusikverband SBV-ASM):**
  ein **Verband** (nicht JBL — das ist ein Verein!), der HarmoniQ erwähnt, erreicht viele Vereine
  auf einmal — **aber** Verbände wollen ein ausgereiftes, vertrauenswürdiges Produkt. Das ist der
  **„größere Effort", den der User sich für später aufgehoben** hat.
- **(4) Kalte Massenmail an Privatadressen — NICHT.** Höchstes Rechtsrisiko (UWG Art. 3 lit. o) und
  verbrennt den ersten Eindruck. Wenn überhaupt, nur offizielle Adressen mit klarem Zweck + Opt-out.

**Empfohlene Reihenfolge:** Fundament (9.1) → **warmer Pilot (3–5 Vereine inkl. JBL)** → Crawler-
Einladung an offizielle Adressen, regionsweise → **Romandie erst nach FR (Block 10)** mit FR-Pilot →
**Verband-Verstärkung, wenn „richtig gut".**

### 9.4 „Richtig gut" — Readiness-Checkliste (vor großem Effort erfüllt)
- Neuer Einstieg + **Tagebuch** live (Grund zu bleiben).  · **TTFB-Fix** (schneller erster Eindruck).
- **SEO/OG** erledigt (Links funktionieren).  · **Mobile** solide (smartphone-first).
- **Analytics** läuft (messbar).  · **Datenqualität** ok (keine Falsch-Bios auf Featured-Personen, A6).
- Für Romandie: **FR fertig** (Block 10).

### 9.5 Recht/Datenschutz (Block 2)
Mailings: **nur offizielle Adressen**, klarer Absender + Zweck + einfacher Opt-out/Lösch-Link, keine
Massen-Aussendung an geharvestete Privatadressen. Für FR/EU-Kontakte DSGVO-konform.

**Stärkster Einzelhebel:** „Dein Verein ist schon da → verwalte ihn" (Crawler-Einladung 5.3.1) —
vereint Verifizierung + Eigentum/Band-Admin + Outreach in einem Schritt.

**Entscheid:** *(offen — empfohlene Reihenfolge oben; konkret zuerst Fundament 9.1 + warmer Pilot)*

---

## 10. Langfristiger Ausbau

*Vom User vorgezogen (2026-06-30): **Mehrsprachigkeit ist Voraussetzung für die Verbreitung in die
Romandie** (Block 9). Einige CH-Bands — und mittelfristig deren Publikum — sind französischsprachig.
Ohne FR-UI keine FR-Outreach.*

### 10.1 Mehrsprachigkeit (i18n) — DE / FR / EN

**Was wird übersetzt — und was nicht:**
- **UI-Texte (Chrome):** Labels, Buttons, Navigation, Meldungen, statische Texte → lokalisiert.
- **Inhalte/Daten bleiben:** Eigennamen (Personen, Bands, Stücktitel, Lokale) werden **nicht**
  übersetzt. Konzert-Beschreibungen bleiben wie erfasst. **Wikipedia-Biografien** könnten **später**
  sprachspezifisch geholt werden (de/fr/en-Wikipedia) — optional.

**Technik (Blazor, Standardweg):**
- `Microsoft.Extensions.Localization` + **`.resx`-Ressourcen** je Kultur (`*.de.resx`/`*.fr.resx`/
  `*.en.resx`), Zugriff über `IStringLocalizer`.
- `RequestLocalizationMiddleware` mit unterstützten Kulturen; **Sprachwahl** über Kultur-Cookie
  (`.AspNetCore.Culture`), Default aus `Accept-Language`, Fallback **DE**. Sprachumschalter im UI.
- Blazor-Server-Eigenheit: Kultur pro Circuit setzen → übliches Muster ist ein
  `Culture/Set`-Endpunkt, der das Cookie setzt und zurückleitet (Cookie lässt sich aus dem
  SignalR-Circuit nicht direkt setzen). Dokumentierter Standard.

**Aufwand & Reihenfolge (Entscheid-Vorschläge):**
- Großer Posten = **alle hartkodierten deutschen Strings** in Ressourcen herausziehen (heute inline,
  z. B. `Home.razor`). Retrofit ist mechanisch, aber breit. **Leitlinie: ab jetzt keine neuen
  hartkodierten Strings** mehr (jede neue Komponente nutzt `IStringLocalizer`) — dann wird der
  Wechsel mit der Zeit billiger statt teurer.
- **FR vor EN priorisieren:** FR ist die **strategische** Sprache (Romandie-Publikum, Block 9); EN
  ist „nice to have"/international. Reihenfolge: **DE (da) → FR → EN später**.
- **Übersetzungsqualität:** Maschinelle Erstfassung ok, aber die **öffentliche Oberfläche braucht
  muttersprachliche Review** — die Romandie beurteilt Qualität streng. **Fach-Glossar** nötig:
  Harmonie / Brass Band / Fanfare / *société de musique* / *chef* / *registre*-*pupitre* (Register/
  Stimme). Idealerweise von einer/einem Romand-Musiker:in gegenlesen lassen.
- **Sofort-Fix unabhängig von allem:** `<html lang>` ist heute fälschlich `en` (Befund A7) → auf
  `de` setzen, später dynamisch je aktiver Kultur.
- **Mehrsprachiges SEO (koppelt an Block 9):** `hreflang`-Tags + sprachpräfix-URLs (`/fr/…`), damit
  Google beide Sprachen indexiert. Ein-Satz-Positionierung (Block 6.2) muss auch auf FR sitzen.

**Entscheid (User 2026-06-30): Disziplin jetzt, Retrofit zum Romandie-Push.** Sofort: `lang=de`-Fix
(A7) + **ab jetzt keine neuen hartkodierten Strings** (jede neue Komponente via `IStringLocalizer`).
**FR vor EN.** Der volle FR-Retrofit (bestehende Strings externalisieren + übersetzen + muttersprach
liche Review + Fach-Glossar) wird auf den **Start der Romandie-Verbreitung (Block 9)** terminiert.
So bleibt der Sofortaufwand klein, der spätere Retrofit wächst nicht weiter an.

### 10.2 Öffentliche API (read-only, nur öffentliche Sicht)

**Idee (aus der Eröffnung):** Eine API, die **genau das** ausliefert, was auch ein **nicht
eingeloggter** Besucher sieht — die öffentliche Read-only-Teilmenge (Block-2-Default:
Komponist:innen/Dirigent:innen/Vorstände öffentlich; alles Private nie).

**Design:**
- **Read-only REST/JSON**, versioniert (`/api/v1/…`): z. B. `konzerte`, `bands`, `komponisten`,
  `personen/{id}`, `stuecke`. Nur `GET`.
- **Sichtbarkeits-Logik wiederverwenden = Kernpunkt:** Die API **muss dieselbe** serverseitige
  Sichtbarkeitsfilterung anwenden wie die UI (ein gemeinsamer Service als „single source of truth"),
  sonst leakt Privates. → Schon **jetzt** die Filterung **zentralisieren** (für die UI), dann ist die
  API später eine **dünne Schicht**.
- **Auth:** für öffentliche Daten keine nötig; optional API-Key nur für Rate-Limit/Statistik, nicht
  für Zugriff. **Rate-Limiting** gegen Scraping-Last (ASP.NET Core Rate Limiter).
- **Caching:** öffentliche Daten → HTTP-Cache/ETag/`Cache-Control` (hilft auch der TTFB-Story).
- **Doku:** OpenAPI/Swagger, wenn gebaut.
- **Nutzen:** Vereins-Websites betten „unsere Konzerte auf HarmoniQ" ein; künftige Mobile-App;
  Community-Datennutzung; Partnerschaften.

**Priorität:** **Niedriger als i18n + Kern-Features** — Ökosystem-/Enabler-Spiel, kein
Retention-Treiber. **Nicht jetzt bauen**; aber die **Sichtbarkeits-Filterung jetzt zentral** halten,
damit die API später billig ist. Bauen, sobald echte Nachfrage da ist (eine Band fragt „kann ich
meine Daten ziehen?").

**Entscheid (2026-06-30, Empfehlung — bei Bedarf revidierbar):** API **zurückstellen** (kein
Retention-Treiber). Aber den **Sichtbarkeits-Service jetzt zentralisieren** (für die UI), damit die
API später eine dünne Schicht ist. Bauen, sobald eine Band konkret danach fragt.

---

## Anhang: Offene Befund-Liste (wird während der Live-Durchläufe gefüllt)

### A7 — SEO / Social-Sharing / PWA (Startseite-`<head>`, Browser, 2026-06-29)
**Gravierende, aber leicht behebbare Lücken (direkt relevant für Block 9 Verbreitung):**
- **`<html lang="en">`** obwohl die Seite **deutsch** ist → falsch. Schadet Screenreadern und SEO;
  sollte `de` sein (bzw. später dynamisch pro Sprache, Block 10).
- **Keine `meta description`** → Google bildet das Snippet selbst/schlecht.
- **Keine Open-Graph-Tags** (`og:title/description/image/url` fehlen komplett) und **keine Twitter-
  Cards** → beim Teilen in WhatsApp/Telegram/Social erscheint **keine Vorschau** (kein Bild, kein
  Titel). Für eine „bring a friend"-Verbreitung (QR-Code-Strategie) ein echter Dämpfer.
- **Kein `canonical`-Link**, **kein JSON-LD/strukturierte Daten** (Schema.org `MusicEvent`,
  `Person`, `MusicComposition` könnten Rich-Results für Konzerte/Personen bringen).

**Positiv (PWA-Basis steht — gut für Mobile/QR-Verbreitung):**
- `manifest.webmanifest` vorhanden, `theme-color #1a0a2e`, `mobile-web-app-capable`,
  `apple-mobile-web-app-*` (Titel „HarmoniQ") → App ist **installierbar** („zum Startbildschirm").
- `viewport`-Meta korrekt (`width=device-width, initial-scale=1`).

*Empfehlung:* OG-Tags + `meta description` + `lang=de` + canonical sind **kleiner Aufwand, hohe
Wirkung** für Sichtbarkeit/Sharing — gute frühe Maßnahme, bevor größerer Outreach startet (Block 9).

### A10 — Phase-1-Review (Browser, mobil ~520 px, 2026-07-05)
**Gesamturteil: Phase 1 ist beeindruckend vollständig & wirklich smartphone-first umgesetzt.**
Umgesetzt & gut:
- **Neuer Einstieg B+A** (anonym): Hero „Dein Konzert-Tagebuch für Blasmusik" + KOSTENLOS STARTEN +
  „Stöbern geht auch ohne Konto". **Bottom-Navigation** (Start·Konzerte·Tagebuch·Videos·Mehr).
- **Phase-0 gleich mit:** `lang=de`, Meta-Description, 7 OG-Tags, Twitter-Cards, **Umami-Analytics**;
  **TTFB 1472 → 910 ms**.
- **Eingeloggter Einstieg:** „Mein Konzert-Jahr 2026" (Tagebuch-Teaser mit echten Zahlen) · „Für
  dich → Kommende Konzerte deiner Bands" (personalisiert, CH-relevant) · **„Wochenüberblick" (Glocke
  = Wiederkehr-Schleife)** · neue Videos deiner Bands.
- **Tagebuch-Seite** (`/account/tagebuch`): Jahres-Statistik (3 Konzerte · 15 Stücke bewertet ·
  ⌀ 4.3★), „Deine Höhepunkte" (Top-Stücke), chronologische Timeline. Sehr nah an der Letterboxd-Idee.
- **Konzert-Detail:** `Lokal` mit Saal + **Leaflet/OSM-Karte mit Pin** (Veranstaltungsort);
  Programm mit **Bewertung + Notiz pro Stück** (5-Sterne interaktiv) + Privatsphäre-Zeile „Freunde
  sehen nur, dass ich dabei war" (= Entscheid privat/teilbar).

**Kritik / To-do (nach User-Feedback repriorisiert 2026-07-05):**
1. **Anonyme „Demnächst" ohne Region-Bezug** (nur WMC/NL). **Vom User herabgestuft (tiefe Prio):**
   aktuell Sommerpause, WMC Kerkrade ist realistisch das nächste grosse Event → nicht überbewerten.
   Später Region-Filter (Block 4.3). Eingeloggt „Für dich" ist ohnehin relevant.
2. **Programm+Bewertung-Tabelle zu breit fürs Handy** (5 Spalten, H-Scroll, `KonzertTagebuchPanel.
   razor`) → **Block-7 „Mobile → Karten" umsetzen. Wichtig (User).** Umsetzung:
   `Umsetzung-Mobile-Programm-Karten.md`.
3. **TTFB 910 ms** (Ziel <500) + **JSON-LD fehlt** + Logo-Tagline englisch → **Wichtig (User).**
   Umsetzung: `Umsetzung-Performance-und-SEO-Feinschliff.md`.

### A8 — Mobile-Ansicht (Browser, 2026-06-29) — NICHT abschließend prüfbar
- **Tool-Limitierung:** `resize_window` (auf 390–400 px) wirkte sich **nicht** auf den gerenderten
  Viewport aus (`window.innerWidth` blieb 1536; Screenshot blieb Desktop-Layout). Mobile-Layout
  ließ sich darum **nicht visuell verifizieren**.
- **Strukturelle Signale:** Ein **Hamburger-Icon (☰)** ist oben links vorhanden (Nav kann
  einklappen), PWA/Viewport-Meta korrekt → Grundlage für Responsive ist da.
- **Offen / am echten Handy zu prüfen:** Stapel-Reihenfolge auf der Login-/Register-Seite
  (Social oben? siehe A3), Verhalten der **Tabellen** (Konzerte/Programm) auf schmalem Screen
  (horizontales Scrollen vs. Karten-Umbruch), Touch-Trefferflächen. *(Per QR-Code schnell selbst
  am Smartphone testbar.)*


*(Konkrete Reibungspunkte aus dem Testen — Schritt → Beobachtung → Vorschlag.)*

### A1 — Anonyme Startseite (prerendertes HTML, ohne Browser, 2026-06-29)
**Positiv (wichtig für Block 8 & 9):** Die Startseite liefert **echten, vollständig gerenderten
Inhalt** im initialen HTML (kein leerer App-Shell). Sichtbar: Statistik (695 Komponist:innen, 1 260
Personen, 311 Bands, 1 438 Stücke, 184 Videos, 2 Bewertungen), letzte Konzerte (Landscapes @ KKL,
Sommerständchen), 4 neue Videos, 6 Featured-Komponist:innen mit Werkzahl (John Mackey 83, Alfred
Reed 67), 6 Bands. → **Prerendering ist aktiv** ⇒ grundsätzlich **SEO-fähig** und der „erste
Bildschirm" ist sofort befüllt (die Verzögerung betrifft nur das *Interaktiv-werden*, Block 8).
- Navigation: Startseite, Konzerte, Komponist:innen, Personen, Stücke, Videos, Bands.
- CTA: Login (oben rechts) + Registrierungs-Hinweis (bewerten, Videos vorschlagen, Mitwirkende
  ergänzen, Personen/Bands anlegen) + „ohne Account browsen".

**Befund/Beobachtung:** Nur **2 Bewertungen** bei 184 Videos — die Kern-Interaktion „bewerten" wird
faktisch kaum genutzt. Stützt den Strategie-Entscheid, den persönlichen Mehrwert („meine besuchten
Konzerte", Block 4) in den Vordergrund zu rücken.

**Noch im Browser zu prüfen (WebFetch kann `<head>` nicht zuverlässig lesen):**
- Sind **Meta-Description + Open-Graph-Tags** gesetzt? (Entscheidend für Google-Snippet & Social-
  Sharing-Vorschau, Block 9.) — *offen*
- Wie lange dauert „bis reaktiv" real, und woran liegt es (Cold-Start vs. Circuit)? (Block 8) — *offen*

### A2 — Anonyme Startseite (Browser, 2026-06-29)
- **Optik:** Dunkles Theme, Violett/Gold, Logo „HarmoniQ – the music database", aufgeräumt und
  modern. Linke Navigation (Startseite/Konzerte/Komponist:innen/Personen/Stücke/Videos/Bands),
  oben rechts „ANMELDEN".
- **Anmelde-Banner** oben: „Mehr aus HarmoniQ herausholen" + Buttons „ANMELDEN"/„KONTO ERSTELLEN".
  Text nennt bereits den Claim: „dich später mit deiner eigenen Person zu verknüpfen" → gut, der
  Mehrwert wird benannt. *Vorschlag:* den persönlichen Nutzen (Block 1/4: „besuchte Konzerte
  merken, Stücke bewerten") prominenter als die Beitrags-Features formulieren — passt besser zur
  primären Zielgruppe.

### A3 — Login- & Registrierungs-Seite (Browser, 2026-06-29)
- **Layout (Login & Register identisch):** Links die **lokale E-Mail/Passwort-Form** (visuell
  primär, Lese-Reihenfolge zuerst), rechts „Mit Dienst anmelden" (**Google**, **Microsoft**).
  Login zusätzlich: **Passkey**, „Angemeldet bleiben", „Passwort vergessen".
- **Befund zur Eröffnungsfrage „Google/Microsoft zuerst?":** Aktuell stehen die Sozial-Logins
  rechts/sekundär. Für die niederschwellige Zielgruppe ist **Social-Login die geringste Hürde**.
  *Vorschlag:* Google/Microsoft **optisch primär** (oben bzw. links, größer), lokales Passwort als
  „oder mit E-Mail". **Besonders wichtig auf Mobile:** Was zuerst *gestapelt* wird, zählt — Social
  sollte dort oben stehen. (Konkrete Mobile-Reihenfolge noch zu prüfen.)
- **Registrierung sammelt nur E-Mail + Passwort** (Mindestlänge **6 Zeichen** — eher schwach; ein
  Stärke-Hinweis/höheres Minimum erwägen). **Kein Name, keine Person** in diesem Schritt → der
  Person-/Claim-Vorschlag (Eröffnungsfrage) muss ein **Schritt NACH der Registrierung** sein.
  → Genau dieser Post-Registrierungs-Flow ist noch zu verifizieren (siehe A4).
- Passkey wird beim **Login** angeboten, bei der **Registrierung** nicht — Inkonsistenz, ggf. dort
  ebenfalls anbieten.

### A4 — Eingeloggter Durchlauf (User `claude@q-no.ch`, Browser, 2026-06-29)
**Arbeitsteilung:** Claude tippt aus Sicherheitsgründen **keine Passwörter** und legt keine Konten
an; der User hat sich selbst eingeloggt, danach treibt Claude den Browser. Claude **finalisiert
auch keinen Claim** („DAS BIN ICH") — das verknüpft das Test-Konto mit einer echten Person.

**Startseite eingeloggt:**
- Neue Sidebar-Sektion **„Konto"**: *Meine Beiträge, Meine Person, Freunde, Profil*.
- **QR-Code „HarmoniQ weitergeben"** ist eingeloggt auf der Startseite (niederschwellige Verbreitung).
- **Reaktivitäts-Befund (Block 8, sichtbar!):** Direkt nach dem Laden zeigte die Startseite
  **0** in allen Statistik-Kacheln und **kein** Person-Banner; erst nach ~3 s (SignalR-Circuit
  verbunden, Hydration) erschienen die echten Zahlen (695/1260/311/1438/184/2) **und** das Banner.
  → Der „bis reaktiv"-Effekt ist real und für Nutzer sichtbar (Platzhalter-Nullen wirken wie ein
  Fehler). *Vorschlag:* statt „0" einen neutralen Lade-/Skeleton-Zustand zeigen (verdeckt die
  Circuit-Zeit); Ursache-Analyse (Cold-Start vs. Circuit) in Block 8.

**Onboarding / Claim — Eröffnungsfragen sind BEREITS UMGESETZT:**
- Prominentes Banner **„Verknüpfe dich mit ‚deiner' Person"** (teal) auf der Startseite, Button
  „LOS GEHT'S" → `/account/onboarding`.
- Onboarding-Seite **„Wer bist du?"**: Band-Auswahl (optional) + Namensfeld. Beim Tippen erscheint
  **live** „**Bist du eine dieser Personen?**" mit Treffern (Name + Band) und je Button
  **„DAS BIN ICH"** (= Claim). Fallback-Karte **„Noch nicht dabei? — MICH NEU ANLEGEN"** legt eine
  neue Person mit Default-Rolle **„Zuhörer:in"** an. → Deckt **beide** Eröffnungsfragen ab:
  Person-Erfassung wird proaktiv vorgeschlagen, und bei Namensgleichheit wird Claim angeboten.
- **Default-Rolle „Zuhörer:in" passt exakt zur primären Zielgruppe** (Publikum/Fans, Block 1).
- **Offener Punkt (→ Block 2 Vertrauen):** „DAS BIN ICH" verknüpft offenbar **ohne Verifizierung**
  — jede:r könnte sich als beliebige real existierende Person ausgeben (Identitätsanmaßung,
  besonders heikel bei Funktionär:innen/Minderjährigen). Verifizierungs-/Bestätigungs-Mechanismus
  noch zu klären (z. B. Band-Admin-Freigabe, E-Mail-Abgleich, „beansprucht – unbestätigt"-Status).
- **Kleinere UX-Punkte:** Treffer-Liste ist lang (alle „Josef…") — ggf. Band-Filter stärker
  betonen; Diakritika/Teiltreffer-Verhalten ok (Live-Suche reagiert ab erstem Wort).

### A6 — Komponist:innen / Personen & Darstellungs-Konsistenz (Browser, 2026-06-29)
- **Komponisten-Übersicht `/komponisten`:** **Karten-Raster** (Avatar, Name, „N Werke", Bio-
  Auszug, „WERKE ANSEHEN"). **Konzert-Übersicht ist dagegen eine Tabelle** → **Darstellungs-
  Inkonsistenz bestätigt** (Eröffnungsfrage). Beide Muster sind für sich ok; die Frage ist
  Vereinheitlichung vs. bewusst unterschiedliche Muster je Inhaltstyp.
- **Komponist = Person (rollenbasiert):** „Werke ansehen" führt auf `/personen/{id}` mit Rollen-
  Chip „Komponist:in" + „* 1949" — das vereinheitlichte Person/Rollen-Modell der Hauptspec ist live.
- **Werke-Liste auf der Personenseite = einfache Liste** (Notensymbol + Titel + „· Komponist:in"),
  **keine Tabelle** → zweite Inkonsistenz zur Stück-/Konzert-Tabelle. *Vorschlag (Block 7):* eine
  einheitliche „Stück-Zeile"-Komponente (Titel/Komponist:in/ggf. Bewertung) für alle Stück-Listen.
- **Claim & Melden überall:** „DAS BIN ICH" und „FEHLER MELDEN / RICHTIGSTELLUNG" auch auf
  Personenseiten — konsistent.
- **Datenqualität (sichtbar, → Crawler-Spec):** Bio von „Adrian Pereira" = „norwegischer
  Fußballspieler" (falscher Wikipedia-Treffer). Solche Fehlanreicherungen sind für Nutzer sichtbar
  und untergraben Vertrauen → Review-/Melde-Pfad (vorhanden) + bessere Wikipedia-Disambiguierung.

### A5 — Konzert-Liste & -Detail (Browser, 2026-06-29)
- **Liste `/konzerte`:** saubere **Tabelle** (Datum/Konzert/Ort/Bands/Videos), Suche (Name/Ort) +
  Zeitraum-Filter („Zeitnah ±1 Monat", „24 von 363"). Konsistent mit anderen Listen.
- **Detailseite:** Breadcrumb, Titel, Datum/Ort, großes Foto, Beschreibung; **Programm als
  Tabelle** (Stück/Komponist:in/Band, klickbar); **„Mitwirkende & Gäste"** getrennt nach
  **Musikant:innen** und **Zuhörer:innen** (als Initialen „K. S." → datenschaftsbewusst, gut für
  Block 2); Video-Block je Band.
- **Melde-Funktion vorhanden:** Button **„FEHLER MELDEN / RICHTIGSTELLUNG"** → adressiert einen
  Teil der Moderations-/Vertrauens-Frage (Block 2/5). (Nicht abgesendet getestet.)
- **Leit-Feature „meine besuchten Konzerte" (Block 4) — STATUS:** Das Datenmodell kennt bereits
  **Person als Zuhörer:in an einem Konzert** (in „Mitwirkende & Gäste"). **Es fehlt** aber: ein
  Self-Service **„Ich war hier"/„besucht"-Button** für den eingeloggten User, **private Notizen**
  und **Bewertung pro Stück** am Konzert. → **Genau hier liegt die Ausbau-Arbeit** für die primäre
  Zielgruppe. Bestehende Video-Bewertung (nur 2 insgesamt) ist davon getrennt; Verhältnis klären
  (siehe Block-4-Entscheid). **Berührt Datenmodell → ggf. `Spezifikation.md` ergänzen.**
