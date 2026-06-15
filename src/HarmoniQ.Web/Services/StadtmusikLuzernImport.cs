using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Einmaliger, idempotenter Import der aktuellen Besetzung des Blasorchesters
/// Stadtmusik Luzern (Quelle: https://stadtmusik-luzern.ch/ueber-uns/informationen/).
/// Legt Band, Instrumente und Personen (Rolle Musikant:in bzw. Dirigent:in inkl.
/// Instrument-Zuordnung) an. Find-or-create – mehrfaches Ausführen ist gefahrlos.
/// Musiker:innen erhalten gemäss Datenschutz-Default die Sichtbarkeit „NurInitialen“,
/// der Dirigent „Oeffentlich“.
/// </summary>
public static class StadtmusikLuzernImport
{
    private const string BandName = "Blasorchester Stadtmusik Luzern";
    private const string BandUrl = "https://stadtmusik-luzern.ch";

    // Instrument/Register -> Namen. Reihenfolge wie auf der Webseite.
    private static readonly (string Instrument, string[] Namen)[] Register =
    [
        ("Piccolo", ["Eva-Maria Wobmann-Boppart"]),
        ("Flöte", ["Madeleine Bischof", "Alexandra Brönimann", "Leandra Brunner", "Katie Hyland", "Clara Yepes", "Michaela Zellweger"]),
        ("Oboe", ["Annina Losey", "Esther Wigger Birrer"]),
        ("Es-Klarinette", ["Manuel Müller"]),
        ("Klarinette", ["Basil Arnold", "Denise Banz", "André Bernhard", "Selina Burch", "Caroline Di Gallo", "Loris Felber", "Salomé Garbely", "Ruedi Hauri", "Daniel Koch", "Caroline Krattiger", "Armin Müller", "Jérémie Pierre", "Nadine Salvisberg", "Michael Stucki", "Ursula Sury", "Ramona Troxler", "Johannes Wechselberger"]),
        ("Bass-Klarinette", ["Raphael Haag", "Julien Leuenberger", "Annemarie Stoessel"]),
        ("Kontrabass-Klarinette", ["Andreas Nydegger"]),
        ("Fagott", ["Patrik Gnos", "Janina Surek", "Nino Wrede"]),
        ("Kontra-Fagott", ["David Brunner"]),
        ("Saxophon", ["Manuel Andergassen", "Corinne Burkart", "Manuel Herren", "Alain Kamm", "Sandro Pedrazzini", "Lea Raas", "Ueli Scherrer", "Vera Wahl"]),
        ("Trompete", ["Joël Arnet", "Nicolas Blättler", "Domenico Emanuele", "Christian Kaufmann", "Anneluise Keiser", "Oliver Kost", "Albert Marbacher", "Sabine Schnyder-Buchser", "Martin Schulthess"]),
        ("Waldhorn", ["Marc Akermann", "Lukas Blaser", "Kilian Dörle", "Ursula Jurt", "Jérôme Koller", "Livia Kuster", "Erik Mayr", "Marius Schwander", "Barbara Wigger-Bircher"]),
        ("Posaune", ["Thomas Blümli", "Lukas Hochstrasser", "Manuel Imhof", "Lorena Seiler", "Pascal Unternährer"]),
        ("Bass-Posaune", ["Andreas Thürig"]),
        ("Euphonium", ["Martin Zihlmann", "Philipp Zimmermann"]),
        ("Tuba", ["Markus Aregger", "Nicola Schaller", "Urs Stucki Müller", "Benjamin Wey"]),
        ("Kontrabass", ["Gonçalo Cardoso"]),
        ("Schlagzeug", ["Reto Aeppli", "Daniel Balmer", "Basil Bättig", "Simon Eymann", "Tobias Gröflin", "Noé Schrag", "Stephan Schrag", "Nico von Moos"]),
        ("Klavier", ["Leandra Hodel", "Patricia Ulrich"]),
        ("Harfe", ["Anne-Martine Hofstetter"]),
    ];

    private const string DirigentName = "Hervé Grélat"; // Chefdirigent

    public static async Task ImportAsync(ApplicationDbContext db, ILogger logger)
    {
        // Band find-or-create (frühere Kurzbezeichnung „Stadtmusik Luzern“ wird mit-erkannt
        // und auf den vollen Namen umbenannt).
        var band = await db.Bands.FirstOrDefaultAsync(b =>
            b.Name == BandName || b.Name == "Stadtmusik Luzern" || b.Webseite == BandUrl);
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

        async Task<Instrument> InstrumentAsync(string name)
        {
            var i = await db.Instrumente.FirstOrDefaultAsync(x => x.Name == name);
            if (i == null)
            {
                i = new Instrument { Name = name };
                db.Instrumente.Add(i);
                await db.SaveChangesAsync();
                neueInstrumente++;
            }
            return i;
        }

        async Task PersonAsync(string name, PersonRolleTyp rolle, Sichtbarkeit sichtbarkeit, Guid? instrumentId, string? funktion)
        {
            // Existenz gegen die DbSets prüfen und nur fehlende Zeilen ergänzen
            // (keine Mutation geladener Navigations-Collections → kein Tracking-Konflikt).
            var person = await db.Personen.FirstOrDefaultAsync(p => p.Name == name);
            if (person == null)
            {
                person = new Person { Name = name, Sichtbarkeit = sichtbarkeit };
                db.Personen.Add(person);
                neuePersonen++;
            }

            if (!await db.PersonRollen.AnyAsync(r => r.PersonId == person.Id && r.Rolle == rolle))
                db.PersonRollen.Add(new PersonRolle { PersonId = person.Id, Rolle = rolle });

            if (instrumentId is Guid iid &&
                !await db.PersonInstrumente.AnyAsync(pi => pi.PersonId == person.Id && pi.InstrumentId == iid))
                db.PersonInstrumente.Add(new PersonInstrument { PersonId = person.Id, InstrumentId = iid });

            // Band-Mitgliedschaft (aktuell aktiv) – einmal je Person/Band.
            if (!await db.BandMitgliedschaften.AnyAsync(m => m.BandId == band.Id && m.PersonId == person.Id))
            {
                db.BandMitgliedschaften.Add(new BandMitgliedschaft
                {
                    BandId = band.Id,
                    PersonId = person.Id,
                    InstrumentId = instrumentId,
                    Funktion = funktion
                });
                neueMitgliedschaften++;
            }

            await db.SaveChangesAsync();
        }

        foreach (var (instrumentName, namen) in Register)
        {
            var instrument = await InstrumentAsync(instrumentName);
            foreach (var name in namen)
                await PersonAsync(name, PersonRolleTyp.Musikant, Sichtbarkeit.NurInitialen, instrument.Id, null);
        }

        await PersonAsync(DirigentName, PersonRolleTyp.Dirigent, Sichtbarkeit.Oeffentlich, null, "Chefdirigent");

        logger.LogInformation("Stadtmusik-Luzern-Import: {Personen} neue Personen, {Instrumente} neue Instrumente, {Mitgliedschaften} neue Mitgliedschaften.",
            neuePersonen, neueInstrumente, neueMitgliedschaften);
    }
}
