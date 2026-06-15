using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Einmaliger, idempotenter Import der aktuellen Besetzung des Jugendblasorchesters
/// Luzern (Quelle: https://jbl-luzern.ch/orchester/aktuelle-besetzung).
/// Bestehende Personen werden per Namen wiederverwendet (KEINE Dubletten – viele
/// Mitglieder spielen auch bei der Stadtmusik). Find-or-create für Band/Instrument/Person.
/// </summary>
public static class JBLLuzernImport
{
    private const string BandName = "Jugendblasorchester Luzern";
    private const string BandUrl = "https://jbl-luzern.ch";

    private static readonly (string Instrument, string[] Namen)[] Register =
    [
        ("Waldhorn", ["Selma Fischli", "Arno Wigger", "Fynn Felder", "Dominik Scherrer", "Marius Leisegang", "Vincent Dittli", "Eline Portmann", "Mona Dillier", "Karim Gadri"]),
        ("Posaune", ["Philipp Häusler", "Lia Weber", "Linus Schumacher", "Amon Bolliger", "Pascal Unternährer", "Matteo Emanuele"]),
        ("Tuba", ["Simon Lampart", "Matteo Wermelinger"]),
        ("Trompete/Kornett", ["Marlon Arnold", "Nils Bernet", "Noe Albrecht", "Ladina Häfliger", "Julie Felder", "Mario Schmidig", "Michael Gnos", "Armando Fähndrich", "Flurin Koch", "Vera Brunner", "Ray Bucher"]),
        ("Euphonium", ["Tim Rogenmoser", "Dominic Iten"]),
        ("Klarinette", ["Leo Rustemovski", "Salome Portmann", "Vivian Bokorny", "Florian Peters", "Mateusz Jaworski", "Fabia Rosenberg", "Stephanie Portmann", "Sina Lanicca", "Laurin Steinmann", "Natalia Bertschmann", "Corine Schnyder", "Paula Studer", "Salomé Garbely", "Basil Arnold", "Raphael Haag", "Lea Araujo Gomes", "Nikolaj Grabowsky", "Gioanna Klaus"]),
        ("Baritonsaxophon", ["Matteo Hodel"]),
        ("Altsaxophon", ["Raul Burri", "Luana Buck", "Marius Häfliger", "Jayme Strub", "Sina Fuchs", "Ennio Pfenniger"]),
        ("Tenorsaxophon", ["Elena Kurmann"]),
        ("Fagott", ["Julian Lisibach", "Dario Gocht", "Luv Flueler", "Valeria Schatt"]),
        ("Querflöte/Piccolo", ["Lena Finger", "Corinne Imfeld", "Selina Zimmermann", "Juliette Duay", "Madlaina Caprez", "Pascale Römer", "Silja Hermann"]),
        ("Bassklarinette", ["Patrick Stalder", "Thierry Kall"]),
        ("Oboe/Englischhorn", ["Mia Verbiest", "Silja Infanger", "Lara Stöckli"]),
        ("Harfe", ["Till Ole Walter"]),
        ("Perkussion", ["Janis Amrein", "Linus Ettlin", "Lionel Schönbächler", "Silena Wespi", "Julian Wolf", "Jari Brunner", "Mauro Wigger", "Remo Blum"]),
        ("Piano", ["Athina Waser"]),
        ("Kontrabass", ["Linda Matile"]),
    ];

    public static async Task ImportAsync(ApplicationDbContext db, ILogger logger)
    {
        var band = await db.Bands.FirstOrDefaultAsync(b => b.Name == BandName || b.Webseite == BandUrl);
        if (band == null)
        {
            band = new Band { Name = BandName, Land = "Schweiz", Webseite = BandUrl };
            db.Bands.Add(band);
        }
        else
        {
            band.Name = BandName;
            band.Land ??= "Schweiz";
            band.Webseite ??= BandUrl;
        }
        await db.SaveChangesAsync();

        var neuePersonen = 0;
        var neueInstrumente = 0;
        var neueMitgliedschaften = 0;

        async Task<Guid> InstrumentIdAsync(string name)
        {
            var i = await db.Instrumente.FirstOrDefaultAsync(x => x.Name == name);
            if (i == null)
            {
                i = new Instrument { Name = name };
                db.Instrumente.Add(i);
                await db.SaveChangesAsync();
                neueInstrumente++;
            }
            return i.Id;
        }

        foreach (var (instrumentName, namen) in Register)
        {
            var instrumentId = await InstrumentIdAsync(instrumentName);
            foreach (var name in namen)
            {
                // Person find-or-create (keine Dubletten – evtl. bereits via Stadtmusik vorhanden).
                var person = await db.Personen.FirstOrDefaultAsync(p => p.Name == name);
                if (person == null)
                {
                    person = new Person { Name = name, Sichtbarkeit = Sichtbarkeit.NurInitialen };
                    db.Personen.Add(person);
                    neuePersonen++;
                }

                if (!await db.PersonRollen.AnyAsync(r => r.PersonId == person.Id && r.Rolle == PersonRolleTyp.Musikant))
                    db.PersonRollen.Add(new PersonRolle { PersonId = person.Id, Rolle = PersonRolleTyp.Musikant });

                if (!await db.PersonInstrumente.AnyAsync(pi => pi.PersonId == person.Id && pi.InstrumentId == instrumentId))
                    db.PersonInstrumente.Add(new PersonInstrument { PersonId = person.Id, InstrumentId = instrumentId });

                if (!await db.BandMitgliedschaften.AnyAsync(m => m.BandId == band.Id && m.PersonId == person.Id))
                {
                    db.BandMitgliedschaften.Add(new BandMitgliedschaft
                    {
                        BandId = band.Id,
                        PersonId = person.Id,
                        InstrumentId = instrumentId
                    });
                    neueMitgliedschaften++;
                }

                await db.SaveChangesAsync();
            }
        }

        logger.LogInformation("JBL-Luzern-Import: {Personen} neue Personen, {Instrumente} neue Instrumente, {Mitgliedschaften} neue Mitgliedschaften.",
            neuePersonen, neueInstrumente, neueMitgliedschaften);
    }
}
