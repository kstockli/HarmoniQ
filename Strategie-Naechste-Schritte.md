# HarmoniQ – Standortbestimmung & nächste Schritte

*Stand: 29.08.2026. Grundlage: Review der öffentlichen Seite, der eingeloggten Schleife und der
Admin-Sicht auf Prod (harmoniq.q-no.ch), plus das eingearbeitete User-Feedback.*

---

## 1. Kern-Diagnose in einem Satz

**Das Angebot (Daten & Funktion) ist reif — es fehlt die Nachfrage (aktive Menschen).**
Der Engpass ist Aktivierung/Go-to-Market, **nicht** Features, Tempo oder Sprache.

Belege aus der Admin-/Live-Sicht:

| Angebot (da) | Nachfrage (fehlt) |
|---|---|
| 988 Bands, 1757 Konzerte, 3903 Stücke, 1167 Videos, 2522 Personen | **12 registrierte Benutzer** |
| Crawler hält die Daten weitgehend selbst aktuell | 13 öffentliche Video-Bewertungen |
| Vollständige Fan-Schleife gebaut & schön | Aktivität konzentriert auf **1 Power-User (Gründer)** |

Die Moderations-Last ist heute **gering** (11 offene Crawler-Funde, 2 Richtigstellungen) — aber nur,
weil kaum jemand da ist. Sie wächst mit der Adoption.

---

## 2. Was schon stark ist (nicht anfassen)

- **Tagebuch = Killer-Artefakt.** „Konzert-Jahr 2026: 10 Konzerte, 54 Stücke bewertet, ⌀ 4.1★",
  Höhepunkte, Zeitstrahl mit Notizen. Für echte Blasmusik-Fans wirklich bindend.
- **Personalisierter Wochenüberblick** (kommende Konzerte deiner Bands, neue Videos, „in der Nähe" ~12 km,
  Freundes-Aktivität) — funktioniert für einen aktiven Nutzer sichtbar gut.
- **Auto-befüllte Datenbasis** über den Crawler = der eigentliche strategische Trumpf.
- Performance, Login-Maske, Filter, Instrumente/Symbole: solide, kein MVP-Gap.

**Konsequenz:** Nicht mehr bauen, um „fertig" zu werden. Bauen nur noch dort, wo es **Aktivierung**
direkt bedient.

---

## 3. Was das negative Vereins-Feedback wirklich bedeutet

> „Nicht noch eine Plattform, die ich als Vereinsverantwortlicher pflegen muss."

Das ist **kein Feature-Wunsch, sondern ein Positionierungs-Fehlschlag.** Der Verantwortliche nahm an,
HarmoniQ sei ein CMS zum Füttern. Der Trumpf ist das Gegenteil: **seine Seite ist längst befüllt.**
Zwei Dinge untergraben diese Botschaft heute:

1. **Auto-Band-Seiten fühlen sich nach „Datenbank-Eintrag" an, nicht nach „unsere Seite".**
   Beispiel (AEW Concert Brass Fricktal): kein Logo, kein Foto, keine Geschichte, keine kommenden
   Konzerte, keine Tickets — nur Wettbewerbs-Einträge. Kein Stolz-Objekt.
2. **Der Nutzen FÜR den Verein (Reichweite) ist noch leer**, weil die Fan-Seite kaum aktiv ist.
   „Wir bringen dir Publikum" stimmt heute noch nicht → Henne-Ei.

---

## 4. Empfehlung – Reihenfolge

### 4.1 Vereine JETZT NICHT breit anschreiben
Ein erster Eindruck pro Verein. Einladen, bevor echter Fan-Traffic auf ihre Seite zeigbar ist,
**bestätigt genau ihre Befürchtung.** Halten, bis 4.2 steht.

### 4.2 Zuerst die Fan-Schleife an EINER Ecke beweisen (Keil-Pilot)
Eine Szene, die real erreichbar ist (Netzwerk um Luzern / JBL / Stadtmusik Luzern, wo die Daten reich
sind). Ziel: **20–50 echte Menschen** erleben die Schleife über die **Herbst-Konzertsaison** (läuft an):

> Konzert besucht → markiert + Stücke bewertet → Band gefolgt → automatisch erfahren, wann sie das
> nächste Mal spielt.

Erst wenn das für ~30 Leute nachweislich funktioniert und Spaß macht, existiert das Argument für Vereine.

### 4.3 Band-Seite zum Stolz-Objekt machen (Beweis fürs „Null-Aufwand"-Versprechen)
Automatisch auf die Band-Seite: **Logo/Foto, Kurzbeschrieb, Homepage-Link, nächste Konzerte + Ticket-Link**
(= Feedback-Punkt 12). Wenn der Präsident seine Seite sieht und sie ist gut — *ohne dass er etwas tat* —
kippt „noch eine Plattform" zu „ist ja schon da".

### 4.4 Vereins-Botschaft umdrehen + Self-Service-Übernahme
- Botschaft: *„Dein Verein ist schon auf HarmoniQ. Fans finden eure Konzerte & Aufnahmen. Nichts zu
  pflegen. Übernehmen/korrigieren? Ein Klick."* — Reichweite + null Aufwand, **nicht** „pflege deine Seite".
- **Reibung beseitigen:** Verwalter wird man heute nur, wenn ein Admin manuell ernennt
  (`/account/meine-bands`). Das skaliert nicht und widerspricht dem „ein Klick"-Versprechen.
  → **Self-Service-Claim** „Das ist unser Verein – ich verwalte" mit leichter Verifikation
  (z. B. Vereins-E-Mail-Domain / Bestätigungsmail), Admin nur als Fallback.

### 4.5 Aktivierungs-Reibung auf der Fan-Seite senken
Prüfen, ob die **erste** Fan-Handlung minimal ist: „Ich war an diesem Konzert" in **2 Taps**,
Account-Zwang erst später (Konto zum Speichern/Wiederkommen). Niedrigere Einstiegshürde ist
vermutlich hebelstärker als jede Vereins-Mail.

### 4.6 Französisch: noch NICHT
Großer Hebel, aber erst nach bewiesener Schleife im deutschsprachigen Keil. Jetzt würde es die dünne
Fan-Basis über Regionen verteilen und die Politur-Fläche verdoppeln.
**Aber vorbereiten:** Strings nicht hart verdrahten (i18n-fähig halten); Romandie/Wallis-Daten sind
bereits importiert → Tag X ist datenseitig bereit.

### 4.7 Vereinfachen / schneller
Größtenteils erledigt, **nicht** der Engpass. Weiter nebenbei, nicht als Schlagzeile.

---

## 5. Konkreter Keil-Pilot (Vorschlag)

1. **3–5 Schaufenster-Band-Seiten** aus dem eigenen Netzwerk richtig gut machen (Logo, Foto, nächstes
   Konzert + Ticket, Geschichte) — zur Not halbmanuell.
2. **20–50 echte Fans** dieser Szene für die Herbst-Konzerte zum Tagebuch/Folgen bewegen (persönlich,
   QR-Code am Konzert, „scanne & merk dir dieses Konzert").
3. **Messen** (siehe 6): Kommen sie wieder? Zündet der „nächstes Konzert"-Anstoß?
4. **Dann** dieselben 3–5 Vereine **warm** ansprechen: „eure Fans sind schon da."

### 5.1 Pilot konkret: JBL-Lagerkonzert „Fantasy" (02.10.2026, Aula Cher Sarnen)

**Warum dieses Konzert:** erstes Konzert nach der Sommerpause, die **neue Besetzung debütiert**, viele
**Eltern/Familien** im Saal, Kunos Netzwerk (Tochter im JBL). Ein Saal voll potenzieller Fans an einem Abend.

**Der Hook (umgedeutet):** HarmoniQ am Konzert = **„digitales Programmheft"**. Der QR verspricht nicht
„bewerte", sondern **Hintergrundwissen** – was ist das Stück, wer der Komponist, wer dirigiert – **ohne Konto,
null Hürde**. Das wollen fast alle (Programmhefte werden gelesen). Bewerten/Folgen ist die Kür für
Enthusiast:innen. (Erkenntnis: „bewerten" ist aktiv und nur für einen Teil; „lesen" ist passiv und für alle.)

**Kanäle (nicht „Werbung", sondern Nutzen zum richtigen Moment):**
- **QR** auf Programm/Tisch: „Mehr zu den Stücken von heute → [QR]".
- **JBL-Förderverein** = warme institutionelle Tür (fertige JBL-Seite zeigen → QR-Platzierung + eine Erwähnung).
- **Cluster** aus 5–6 Freund:innen, die zusammen hingehen und gemeinsam markieren (Freundes-Feed leuchtet auf).
- **Nicht:** Peer-Marketing durch die Tochter; **keine** Foto-Upload-Funktion (Jugendorchester = Minderjährige
  → Bildrecht/Einwilligung + Moderation; zu riskant). Anonymes Bewerten bleibt **privat** (Sterne + private
  Notiz, keine öffentliche Anzeige → keine Moderation nötig).

**Schon vorbereitet (erledigt):** JBL-Konzertseite angereichert – „Fantasy"-Beschreibung, Dirigent Sandro Blank,
3 Stücke mit Jahr + Beschreibung; die 16 gefolgten Bands haben Beschreibungen; kaputte `|null`-Links bereinigt.

**Noch zu bauen für die Konversion (Reihenfolge):**
1. **Stück-Beschreibung auf der Konzertseite ausklappbar** (das „Programmheft" direkt am QR-Ziel).
2. **Wert-zuerst-Onboarding:** anonym **privat** bewerten/merken (Cookie, wie Video-Voting), Konto **danach**.
3. **5-Band-Nudge:** nach dem ersten Folgen „diese Bands kennst du vielleicht auch" (Feed wird erst ab ~5 wertvoll).

**Timeline (heute 30.08. → 02.10., ~4½ Wochen):** Konzertseite fertig machen → ~2 Wochen vorher Förderverein →
Konzertabend QR + Cluster → danach „nächstes Konzert"-Anstoss messen (siehe §6).

---

## 6. Woran wir Erfolg messen (statt „Features fertig")

- **Aktivierung:** Anteil neuer Nutzer, die ≥1 Konzert markieren **und** ≥1 Stück bewerten.
- **Retention:** Anteil, der nach dem nächsten Konzert **wiederkommt** (Tagebuch-Eintrag Nr. 2).
- **Schleifen-Beweis:** Klicks auf „nächstes Konzert deiner Bands" / Öffnungen der Wochenmail.
- **Nordstern für diese Phase:** von **12** auf **~100 real aktive** Fans in der Pilot-Szene.

---

## 7. Kleine Bausteine, die den Pilot stützen (Bau-Backlog, priorisiert)

1. **Ticket-/Ankündigungs-Link am Konzert** (Feedback-Punkt 12) — direkter Fan-Nutzen + Vereins-Attraktivität.
2. **Band-Seite anreichern:** Logo/Foto/Homepage + „nächste Konzerte" prominent (Stolz-Objekt).
3. **Self-Service Band-Übernahme** statt manueller Admin-Ernennung (4.4).
4. **Fan-Aktivierung in 2 Taps** prüfen/senken (4.5): Konzert merken ohne sofortigen Account.
5. **Onboarding-Nudge:** neuer Nutzer → sofort „folge 1 Band" + „trag dein letztes Konzert ein".

*(Erst 1–2 vor dem Pilot; 3–5 begleitend.)*

---

## 8. Bewusst NICHT jetzt

- Breite Vereins-Einladung (erst nach 4.2).
- Französisch (erst nach bewiesener Schleife; Architektur aber i18n-fähig halten).
- Weitere „Vollständigkeits"-Features. Das Produkt ist funktional reif genug für den Pilot.

---

## 9. Offene strategische Frage an Kuno

Welche **eine Szene** ist am realistischsten aktivierbar (Netzwerk, Zugang zu 20–50 Fans, reiche
Daten)? Danach richtet sich der ganze Pilot: Luzern-Umfeld (JBL/Stadtmusik), eine konkrete Region,
oder ein bestimmtes Herbst-Konzert als Startpunkt?

*(Teilweise beantwortet: Startpunkt = JBL-Lagerkonzert 02.10., siehe §5.1.)*

---

## 10. Kreative Verbreitungs-Ideen — *offen, folgt von Claude*

**Vorgemerkt (Kuno-Wunsch):** Über die soliden Kanäle (§5.1) hinaus eine Sammlung **kreativer,
niederschwelliger Ideen** zur Verbreitung erarbeiten – z. B. rund ums Konzert-Erlebnis, den Freundes-Feed,
das „digitale Programmheft", saisonale Aufhänger (Herbst-Konzertsaison), Vereins-Stolz, Tagebuch-Jahresrückblick
als teilbares Artefakt. Bewusst **später** (nicht vor dem 02.10.-Piloten), aber hier reserviert.
