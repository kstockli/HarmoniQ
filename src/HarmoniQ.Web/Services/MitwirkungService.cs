using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Erfasst eine Video-Mitwirkung und legt dabei Person / Instrument / Stimme bei Bedarf
/// automatisch an (Autocomplete-mit-Anlegen). Genutzt vom Admin-Cast-Editor (Status Genehmigt)
/// und von Community-Vorschlägen (Status Ausstehend).
/// </summary>
public static class MitwirkungService
{
    public record Eingabe(
        string PersonName,
        MitwirkungRolle Rolle,
        Sichtbarkeit SichtbarkeitFuerNeu,
        string? InstrumentName,
        string? StimmeBezeichnung,
        string? Anmerkung);

    public static async Task ErfasseAsync(ApplicationDbContext db, Guid videoId, Eingabe e,
        VideoStatus status, string? vorgeschlagenVonId)
    {
        var name = e.PersonName.Trim();
        if (name.Length == 0) return;

        // Person finden oder anlegen (+ passende Rolle ergänzen) – auch über Alias-Namen.
        var person = await db.Personen.Include(p => p.Rollen).FirstOrDefaultAsync(p => p.Name == name)
                     ?? await db.Personen.Include(p => p.Rollen).FirstOrDefaultAsync(p => p.Aliase.Any(a => a.Name == name));
        if (person == null)
        {
            person = new Person { Name = name, Sichtbarkeit = e.SichtbarkeitFuerNeu };
            db.Personen.Add(person);
        }
        var prt = e.Rolle == MitwirkungRolle.Dirigent ? PersonRolleTyp.Dirigent : PersonRolleTyp.Musikant;
        if (person.Rollen.All(r => r.Rolle != prt))
            person.Rollen.Add(new PersonRolle { Rolle = prt });

        Guid? instrumentId = null;
        Guid? stimmeId = null;
        if (e.Rolle == MitwirkungRolle.Musikant && !string.IsNullOrWhiteSpace(e.InstrumentName))
        {
            var iname = e.InstrumentName.Trim();
            var instrument = await db.Instrumente.FirstOrDefaultAsync(i => i.Name == iname)
                             ?? db.Instrumente.Local.FirstOrDefault(i => i.Name == iname);
            if (instrument == null)
            {
                instrument = new Instrument { Name = iname };
                db.Instrumente.Add(instrument);
            }
            if (!await db.PersonInstrumente.AnyAsync(pi => pi.PersonId == person.Id && pi.InstrumentId == instrument.Id))
                db.PersonInstrumente.Add(new PersonInstrument { Person = person, Instrument = instrument });
            instrumentId = instrument.Id;

            if (!string.IsNullOrWhiteSpace(e.StimmeBezeichnung))
            {
                var bez = e.StimmeBezeichnung.Trim();
                var stimme = await db.Stimmen.FirstOrDefaultAsync(s => s.InstrumentId == instrument.Id && s.Bezeichnung == bez)
                             ?? db.Stimmen.Local.FirstOrDefault(s => s.Instrument == instrument && s.Bezeichnung == bez);
                if (stimme == null)
                {
                    stimme = new Stimme { Instrument = instrument, Bezeichnung = bez };
                    db.Stimmen.Add(stimme);
                }
                stimmeId = stimme.Id;
            }
        }

        db.VideoMitwirkungen.Add(new VideoMitwirkung
        {
            VideoId = videoId,
            Person = person,
            Rolle = e.Rolle,
            InstrumentId = instrumentId,
            StimmeId = stimmeId,
            Anmerkung = string.IsNullOrWhiteSpace(e.Anmerkung) ? null : e.Anmerkung.Trim(),
            Status = status,
            VorgeschlagenVonId = vorgeschlagenVonId
        });

        // Feed-Ereignis nur für bestätigte (öffentliche) Mitwirkungen.
        if (status == VideoStatus.Genehmigt)
            db.Aktivitaeten.Add(new Aktivitaet
            {
                AkteurPerson = person,
                Typ = AktivitaetTyp.MitwirkungHinzugefuegt,
                ZielTyp = AktivitaetZielTyp.Video,
                ZielId = videoId
            });

        await db.SaveChangesAsync();
    }
}
