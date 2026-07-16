# HarmoniQ – Spezifikation Erklär-Videos

Living-Dokument für die **Video-Kommunikation** von HarmoniQ (`https://harmoniq.q-no.ch`). Ziel ist nicht
*ein* Video, sondern ein **Programm aus mehreren kurzen Videos**, zielgruppengerecht und über die Zeit
(u. a. per Wochenmail) ausgeliefert. Entstanden als Punkt 6 der User-Test-Umsetzung
(`Umsetzung-Feedback-Usertests.md`) – bewusst am Ende, wenn die Oberfläche vereinfacht ist.

> Verwandte Specs: `Spezifikation.md` (Kern), `Spezifikation-Crawler.md` (Import), `SpezifikationBenutzerErlebnis.md` (UX/Strategie).

Status-Legende: ✅ produziert · 🎬 in Produktion · 📝 spezifiziert/geplant · 💬 offen (zu entscheiden)

---

## 1. Zweck & Leitidee

**Problem (aus User-Tests):** „Man kann so viel – zu wenig klar, *was* man tun soll / *wozu* das Ganze."
Die Oberfläche soll selbsterklärend sein; Videos sind **Akquise + Orientierung**, kein Ersatz dafür.

**Leitidee:** Kurze, nutzen-getriebene Häppchen statt eines langen Rundgangs. Ein **Hero-Video** zum
Einstieg, danach Woche für Woche ein **Feature-Clip** (Drip via Wochenmail). Jedes Video zeigt einen
konkreten, „coolen" Anwendungsfall – nicht eine Funktionsliste.

---

## 2. Adressaten (Tracks)

Drei Tracks, **phasiert** produziert (nach Priorität). Jeder Track ist eine eigene Episodenliste; die App
ändert sich noch, daher Videos bewusst **kurz und einzeln ersetzbar** halten.

### Track A — „Neu hier?" · Besucher:in (nicht eingeloggt) · **Priorität 1**
- **Persona:** War (vielleicht) an einem Blasmusik-Konzert, landet über Suche/Link/Wochenmail-Weiterleitung
  auf der Startseite, noch kein Konto.
- **Ziel:** In < 90 s verstehen „*Was ist HarmoniQ, was hab ich davon?*" → **Konto erstellen / Jetzt mitmachen**.
- **Umfang:** 1 Hero-Video.

### Track B — „Für Konzertgänger:innen" · eingeloggte Fans · **Priorität 2**
- **Persona:** Registriert, will die App im Alltag nutzen (folgen, Tagebuch, entdecken).
- **Ziel:** Je Clip *eine* Funktion begreifen und sofort ausprobieren.
- **Umfang:** Serie kurzer Häppchen, **Drip via Wochenmail**.

### Track C — „Für Vereins-Verwalter:innen" · Band-Admins · **Priorität 2–3**
- **Persona:** Wurde Band-Admin (Einladung/Ernennung), pflegt Verein, Konzerte, Videos.
- **Ziel:** Verwaltungs-Aufgaben eigenständig erledigen.
- **Umfang:** How-to-Serie, etwas länger/kapitelt; im Band-Admin-Bereich verlinkt + Verwalter-Onboarding.

> Weitere Tracks später denkbar (z. B. globaler Admin/Crawler) – nicht Teil der ersten Phasen.

---

## 3. Format & Produktion

**Grundformat (entschieden):** **Bildschirmaufnahme der echten App + dezente Motion-Graphics +
KI-Voiceover (Deutsch) + Untertitel.** Zeigt echte Abläufe, ist schnell produziert und **leicht
aktualisierbar** (wichtig, weil sich die UI noch ändert).

- **Motion-Graphics** (kein Zeichentrick, nur Politur über dem Screencast):
  Callouts (Pfeil/Kreis/Highlight auf Buttons), Kinetic Text (eingeblendete Stichworte), Zoom/Schwenk auf
  den relevanten Ausschnitt, Cursor-Spotlight/Klick-Ripple, Schritt-Titelleisten, animierte Intro/Outro-Karten.
- **Stimme:** **KI-TTS** – Descript-Stimme **„Anneliese"** (native Deutsch, warm; Reserve „Erik"), konstant über alle Tracks (Entscheid 2026-07-15).
  Grund: kein Profi-Sprecher nötig, konsistent, bei Skript-Änderung in Minuten neu generierbar. Eigene Stimme
  oder Untertitel-only bleiben Alternativen (💬).
- **Untertitel:** Pflicht (Deutsch, CH-Hochdeutsch). Landing-Page-Video läuft **stummgeschaltet mit
  Untertiteln** (autoplay muted) + „mit Ton ansehen".
- **Musik:** dezente Blasmusik als Untermalung – **Rechte beachten** (eigene Band-Aufnahmen mit Erlaubnis
  oder lizenzfreie Musik), v. a. beim Hero-Video (💬).

**Technische Vorgaben:**
- **Seitenverhältnis je Track (Entscheid 2026-07-15, mobile-first):** Track **A (Besucher) + B (Fans) =
  9:16 Hochformat, am Handy aufgenommen** (so wird HarmoniQ genutzt; als YouTube-Short + Landing/Social).
  Track **C (Verwalter) = 16:9 Desktop** (Admins pflegen am Computer). Auflösung 1080×1920 bzw. 1920×1080, ≥ 30 fps.
- **Branding an die App angelehnt:** dunkler Grund `#0e0018`, Gold `#D4AF37`, Violett `#9B59B6`, Headline-Font
  „Playfair Display"; Logo in Intro/Outro. Dev-Banner („DEV localhost") in Aufnahmen ausblenden (Prod-Optik).
- **Demo-Daten:** realistische Beispiel-Band/-Konzerte, keine echten personenbezogenen Daten von Dritten.

**Längen-Richtwerte:** Hero ≤ 90 s · Fan-Häppchen 60–90 s · Verwalter 2–4 min (mit Kapiteln).

**Werkzeug-Vorschläge (💬):** Aufnahme OBS / Screen Studio; Schnitt+Motion DaVinci Resolve / CapCut;
**Descript** (Screencast + TTS + Untertitel + Schnitt in einem) als besonders schlanke Option. TTS z. B.
ElevenLabs (sehr natürlich) oder Azure/Google TTS.

---

## 4. Inhalt je Track (Episoden)

Jede Episode = 1 Anwendungsfall. Skript-Kern je Episode: **Hook → Nutzen → 1 Ablauf zeigen → CTA.**
Konkrete Funktionen sind an den aktuellen Stand der App geknüpft.

### Track A · Hero „Neu hier?" 📝 (Prio 1)
- **Dramaturgie (Entscheid 2026-07-13):** **aufbauend** – zuerst der freie Nutzen **ohne Konto**
  (Entdecken/Stöbern), dann sichtbar abgesetzt **„mit Konto kommt dazu …"** (Folgen, Tagebuch,
  „möchte hin"), dann CTA. Phasen im Bild beschriftet („Ohne Konto" → „Mit Konto"), damit nie
  unklar ist, was ein Konto braucht. Ausformuliert in `VideoSkript-A1-Hero.md`.
- **Ton (Entscheid 2026-07-13):** **kein „kostenlos"** – Nutzen-/„mit Konto"-geführt. Gilt für alle Tracks
  UND die App-CTAs (Startseite-Knopf „Jetzt mitmachen", Login-Maske „Konto erstellen"). Begründung: bei einer
  Nischen-Community-DB war der Preis nie die Hürde; „gratis" weckt eher Misstrauen als Anreiz.
- **Hook:** „Gerade ein Blasmusik-Konzert erlebt – und willst wissen, *wer* da spielte und *welches Stück* das war?"
- **3–4 Use-Cases in schneller Folge:**
  1. **Entdecken** – Bands, Konzerte, Stücke, eingebettete Videos nebeneinander.
  2. **Folgen** – Lieblingsvereinen folgen → automatisch erfahren, wann/wo sie als Nächstes spielen.
  3. **Konzert-Tagebuch** – festhalten „ich war dabei" und Stücke bewerten.
  4. **„Ich möchte hin"** – künftige Konzerte vormerken.
- **CTA:** „Jetzt mitmachen / Konto erstellen" → `Account/Login`.

### Track B · Fan-Häppchen 📝 (Prio 2, Drip Wochenmail)
- **B1 – Vereinen folgen:** Band suchen, „Folgen", was der Feed/„Du möchtest hingehen" dann bringt.
- **B2 – Konzert-Tagebuch:** vergangenes Konzert markieren, Stücke **bewerten**, Notizen erfassen.
- **B3 – „Ich möchte hin":** künftiges Konzert vormerken, Notizen schon **vorab**; Übersicht auf Startseite/Tagebuch.
- **B4 – Ein Stück entdecken:** von einem Stück zu Komponist:in, „wer spielt es noch", Videos vergleichen.
- **B5 – Profil & Personen:** eigene Rolle/Person, Sichtbarkeit; (Freundschaften, falls relevant).

### Track C · Verwalter-How-to 📝 (Prio 2–3)
- **C1 – Verein beanspruchen & pflegen:** Band-Admin werden, Stammdaten (Heimatort, Links **inkl.
  YouTube-Kanal**) pflegen.
- **C2 – Konzert erfassen:** Datum + **Uhrzeit**, Programm/Stücke, **Vorschau**, speichern.
- **C3 – Videos hinzufügen:** Einzel-Link (Stück/Komponist:in werden **automatisch erkannt**) **und**
  „YouTube durchsuchen" pro Band; passendes **Konzert** wird vorgeschlagen.
- **C4 – Mitglieder & Vorstand:** Mitgliedschaften/Funktionen pflegen, sichtbare Rollen-Claims bestätigen.

---

## 5. Auslieferung

- **Track A (Hero):** auf der **Startseite/Landing** prominent (autoplay muted + Untertitel), zusätzlich im
  Onboarding nach der Registrierung.
- **Track B (Fan):** **Wochenmail** – ein Clip pro Woche, verlinkt auf das gehostete Video; optional ein
  „Tipps & Tricks"-Bereich in der App.
- **Track C (Verwalter):** **fix per E-Mail an die Band-Admins** (Entscheid 2026-07-13) – als Onboarding-
  Sequenz beim Band-Admin-Werden und danach je Clip nachgeliefert (Verwalter schauen selten „einfach so" in
  die App, daher ist die Mail der verlässliche Kanal). **Zusätzlich** im Band-Admin-Bereich als
  „Hilfe/So geht's"-Verweis dauerhaft verfügbar.
- **Hosting (💬):** entweder **YouTube** (kostenlos, Untertitel, HarmoniQ nutzt YouTube ohnehin) oder
  **Infomaniak VOD** (bereits für SBBW-Videos im Einsatz, mehr Kontrolle/keine Fremdwerbung). Einbettung über
  den bestehenden `VideoEinbettung`-Mechanismus denkbar.

---

## 6. Produktions-Workflow (je Episode)

1. **Skript** (Sprechertext + Szenen/Screens) – Freigabe Kuno.
2. **Demo-Daten** vorbereiten (saubere Beispiel-Band/-Konzerte).
3. **Screencast** aufnehmen (Prod-Optik, Dev-Banner aus).
4. **Schnitt + Motion-Graphics** (Callouts, Zoom, Intro/Outro).
5. **Voiceover** (TTS) generieren + synchronisieren.
6. **Untertitel** erzeugen + korrigieren.
7. **Review** (Kuno) → **Publizieren** → in Landing/Wochenmail/Onboarding einbinden.

---

## 7. Reihenfolge / Fahrplan

1. **Hero-Video (Track A)** – zuerst, weil Akquise/Orientierung den größten Hebel hat.
2. **Format-Setup einmalig festlegen:** Stimme (TTS-Muster wählen), Musik/Rechte, Hosting, Branding-Vorlage
   (Intro/Outro, Untertitel-Stil).
3. **Fan-Häppchen (Track B)** – Drip in der Wochenmail, ~1 Clip/Woche.
4. **Verwalter-Serie (Track C)** – parallel/nachgelagert, sobald Band-Admin-Flows stabil sind.

---

## 8. Offene Punkte (💬)

- **Stimme final:** TTS-Anbieter/Stimme (Muster antesten) oder doch eigene Stimme?
- **Musik & Rechte:** welche Blasmusik dürfen wir verwenden?
- **Hosting:** YouTube vs. Infomaniak VOD.
- **Werkzeug/Wer schneidet:** Selbst (Descript/CapCut) oder Dienstleister?
- A/B sind ohnehin 9:16 → direkt für YouTube-Short/Instagram/TikTok nutzbar (kein separater Schnitt nötig).
- **Sprachversionen:** vorerst nur Deutsch; FR/IT später (die App ist mehrsprachig angelegt)?
