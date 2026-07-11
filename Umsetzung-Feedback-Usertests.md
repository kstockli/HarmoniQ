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

## 3. Filter allgemein vereinfachen (öffentliche Seiten) — `/bands` ✅ · `/konzerte` 🔜
**Feedback:** „Einfacher: nur **1 Filter** und ein Knopf **‚Mehr Filter'**, wo man Standort/Stärkeklasse
etc. detailliert filtern kann."

**Plan:** Ein Hauptfilter (Suche) sichtbar; sekundäre Filter (Standort/Umkreis/Stärkeklasse/Kategorie/
Zeitraum) hinter **„Mehr Filter"** (aufklappbar). **`/bands` umgesetzt** (Suche + „Mehr Filter"), das
gleiche Muster steht für **`/konzerte`** noch aus.

---

## 4. Einstieg / Bottom-Navigation 🔜
**Feedback:** „Knöpfe unten: solange man kein Tagebuch hat, soll **Bands** da stehen."

**Plan:** Bottom-Nav zeigt **„Bands"** statt „Tagebuch", solange das Konto **keinen Tagebuch-Eintrag**
hat (führt neue Nutzer zum Folgen → macht den Feed erst wertvoll). Sobald ein Eintrag existiert →
„Tagebuch".

---

## 5. Konzerte: „Ich möchte hingehen" (statt „war da") 🔜
**Feedback:** „Bei zukünftigen Konzerten kann man nicht ‚ich war da' sagen, sondern **‚ich möchte an
dieses Konzert gehen'**. Solange das Konzert nicht durch ist, müsste das in meiner Übersicht sichtbar sein."

**Plan (Datenmodell → Spezifikation.md mitpflegen):**
- `KonzertBesuch` bekommt einen **Status: Geplant / Besucht**.
- **Zukünftiges** Konzert: Aktion **„Ich möchte hingehen"** (Status Geplant). **Vergangenes**: „Ich war dabei"
  (Besucht). Nach dem Konzertdatum fragt die bestehende **Tagebuch-Nachfrage** „Warst du da?" → Geplant→Besucht.
- **Übersicht:** geplante (künftige) Konzerte erscheinen auf der **Startseite** und im **Tagebuch**
  (Abschnitt „Geplant"), getrennt von den besuchten.

Größter Brocken, aber hoher Nutzen.

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

## 7. Anmelden / Registrieren – eine „clevere" Maske 🔜
**Feedback:** „Nicht klar, ob anmelden oder ‚Kostenlos registrieren'. Besser: **ein** Knopf, dort zuerst
**Google/Microsoft**, dann E-Mail/Passwort. Ist die E-Mail neu, direkt die Registrierung ergänzen
(2× Passwort o. Ä.). Also **eine** Login/Anmelde-Maske, evtl. clever. Wie machen es andere?"

**Wie andere es machen (identifier-first):** Notion/Slack/Google zeigen erst **E-Mail eingeben**, danach
entscheidet das System, ob bekannt (→ Passwort) oder neu (→ Registrierung). Social-Login-Knöpfe stehen
**oben** („Weiter mit Google/Microsoft"), Trenner „oder", darunter das E-Mail-Feld.

**Plan:** Eine kombinierte Seite:
1. Oben **„Weiter mit Google" / „Weiter mit Microsoft"**, dann Trenner „oder".
2. **E-Mail** eingeben → „Weiter".
3. Bekannte E-Mail → Passwort-Feld. Neue E-Mail → Passwort **+ Wiederholung** (Registrierung inline),
   ohne separate „Registrieren"-Seite. Ein einziger Einstieg, kein „Login vs. Registrieren"-Rätsel.

Auth-sensibel → sorgfältig umsetzen und testen (bestehende Identity-Flows/Bestätigungsmail beachten).

## 8. Admin: Benutzer löschen 🔜
**Feedback:** „Wieso kann ich als Admin keine Benutzer löschen?"
**Antwort/Diagnose:** Auf `/admin/benutzer` gibt es nur „Verknüpfung" (Person zuordnen) – ein **Löschen
wurde nie gebaut**.
**Plan:** Lösch-Aktion mit Bestätigung. Sauber verknüpfte Daten behandeln: `Person.BenutzerId` → null
(Person bleibt), `BandAdministrator`/`KonzertBesuch`/`PushSubscription`/`BenachrichtigungPraeferenz`
des Kontos entfernen, `BandAdminEinladung.EingeladenVon` → null; dann `UserManager.DeleteAsync`
(räumt AspNetUserRoles/Claims/Logins). FK-Verhalten je Tabelle vor der Umsetzung prüfen.

## 9. Person-Löschen „hängt" (Prod) 🔜
**Feedback:** „Beim Person-Löschen hängt es auch auf harmoniq.q-no.ch."
**Diagnose:** `db.Personen.Remove` + `SaveChanges` löst DB-**Cascade** über viele abhängige Tabellen aus
(KonzertPersonen, StueckBeitraege, Aktivitäten, Aliase, Rollen, Instrumente, Links, Mitgliedschaften,
Interessen …). Lokal (wenig Daten) schnell, auf Prod (mehr Daten) langsam – vermutlich fehlende Indizes
auf einzelnen `PersonId`-FKs oder tiefe Cascade-Kette.
**Plan:** mit Prod-Logs/`EXPLAIN` die langsame(n) Cascade-Query(s) finden; fehlende FK-Indizes ergänzen;
zudem im UI einen **Lade-/„läuft…"-Zustand** zeigen, damit es nicht „eingefroren" wirkt.

## Empfohlene Reihenfolge
1. **Performance** ✅ Code (Stream-Rendering + Cache); Railway-Keep-warm bleibt Kunos Infra-Schritt.
2. **Bands-Liste & Filter** (Punkt 2) – Filter-Bug + Folgen-Knopf + Enter→Resultat (billig, sofort spürbar).
3. **Filter vereinfachen** (Punkt 3) – 1 Filter + „Mehr Filter", einheitlich.
4. **Bottom-Nav** (Punkt 4) – klein.
5. **Konzert „möchte hingehen"** (Punkt 5) – Feature mit Datenmodell-Änderung.
6. **Erklär-Video + Anwendungsfälle** (Punkt 6) – am Schluss.
