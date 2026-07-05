# Umsetzung: Programm + Bewertung auf Mobile als Karten (statt breiter Tabelle)

> Umsetzungs-Vorschlag aus dem Phase-1-Review (siehe `SpezifikationBenutzerErlebnis.md` §A10,
> Punkt 2 + Block-7-Entscheid „Desktop Tabelle / Smartphone Karten"). **Priorität: hoch** — trifft
> die Kern-Interaktion (Stücke bewerten) auf dem Hauptgerät (Smartphone-first).

## Problem
Das Programm mit den Tagebuch-Spalten wird als **`MudSimpleTable` mit bis zu 5 Spalten** gerendert
(`Stück · Komponist:in · Band · Meine Bewertung · Meine Notiz`). Auf dem Handy (~390 px) ist die
Tabelle **breiter als der Viewport** → horizontales Scrollen, „Stück" und „Meine Notiz" werden
abgeschnitten. Verifiziert im Review (mobil ~520 px: nur 3 von 5 Spalten sichtbar).

**Datei:** `src/HarmoniQ.Web/Components/Shared/KonzertTagebuchPanel.razor`, Programm-Block
Zeilen ~64–127 (`<MudSimpleTable>` … `</MudSimpleTable>`). Die Handler bleiben unverändert
(`SterneSpeichern`, `NotizStueckSetzen`, `StueckSpeichern`, `Eindruck(...)`).

## Ziel
- **Desktop (md und grösser):** Tabelle wie bisher — kein Regressions-Risiko.
- **Mobile (sm und kleiner):** je Programmpunkt eine **Karte** (gestapelt), **kein** H-Scroll,
  Bewerten/Notiz gut mit dem Daumen bedienbar.

## Vorschlag (MudBlazor, responsive Doppel-Render)
1. **Bestehende Tabelle nur noch ab `md`** zeigen:
   ```razor
   <MudHidden Breakpoint="Breakpoint.SmAndDown">
       @* … die bestehende MudSimpleTable … *@
   </MudHidden>
   ```
2. **Karten-Variante für `sm` und kleiner** ergänzen:
   ```razor
   <MudHidden Breakpoint="Breakpoint.MdAndUp">
       <MudStack Spacing="3">
           @foreach (var p in Programm)
           {
               var e = Eindruck(p.KonzertStueckId);
               <MudPaper Class="pa-3" Style="background:#1a0030; border:1px solid #3a1060;">
                   <MudLink Href="@($"/stuecke/{p.StueckId}")" Style="color:#F0E6FF;font-weight:500;">@p.Titel</MudLink>
                   <MudText Typo="Typo.caption" Class="d-block" Style="color:#A78FC0;">
                       @* Komponist:innen (Links) · Band (Link) — analog Tabelle *@
                   </MudText>
                   @if (bewertbar)
                   {
                       <div class="mt-2">
                           <MudRating MaxValue="5" SelectedValue="e.Sterne ?? 0"
                                      SelectedValueChanged="@(v => SterneSpeichern(p.KonzertStueckId, v))" />
                       </div>
                       <MudTextField T="string" Value="e.Notiz"
                                     ValueChanged="@(v => NotizStueckSetzen(p.KonzertStueckId, v))"
                                     Placeholder="Notiz…" Variant="Variant.Outlined" Margin="Margin.Dense"
                                     FullWidth="true" Class="mt-1"
                                     OnBlur="@(() => StueckSpeichern(p.KonzertStueckId))" />
                       </div>
                   }
               </MudPaper>
           }
       </MudStack>
   </MudHidden>
   ```
3. **Duplizierung vermeiden:** Die Darstellung eines Programmpunkts (Titel-Link, Komponist:innen-
   Links, Band-Link) als **gemeinsames `RenderFragment`** oder kleine Teilkomponente
   `ProgrammZeileInhalt` auslagern und in Tabelle *und* Karte verwenden. So bleibt die Logik an
   einer Stelle.

## Akzeptanzkriterien
- Bei **390 px Breite: kein horizontales Scrollen**; Titel, Komponist:in, Band, 5-Sterne-Rating und
  Notizfeld sind alle erreichbar.
- **Sterne tappbar** (Touch-Ziel ≥ ~40 px), Bewertung + Notiz speichern wie bisher (Snackbar/Persistenz).
- **Desktop unverändert** (Tabelle).
- `/account/tagebuch` spiegelt eine mobil gesetzte Bewertung korrekt (⌀, Höhepunkte, Timeline).

## Verifizierung
- Browser-DevTools Responsive 390 px: Stück bewerten → prüfen, dass es speichert und in `/account/
  tagebuch` erscheint. Desktop-Ansicht gegenprüfen (Tabelle unverändert).
- Build grün, keine Konsolenfehler.

## Hinweis (gleiche Tabellen-Falle prüfen)
`src/HarmoniQ.Web/Components/Pages/Konzerte/KonzertDetail.razor` rendert bei **Wettbewerben** eine
Rangliste-Tabelle (Rang·Band·Dirigent·Stücke·Punkte) und normale Programme ggf. ebenfalls als
Tabelle. Dort dieselbe „Mobile → Karten"-Umstellung erwägen (gleiche Ursache, gleicher Fix).
