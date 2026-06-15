using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (await db.Personen.AnyAsync()) return;

        var mackey = new Person
        {
            Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
            Name = "John Mackey",
            Biografie = "John Mackey (* 1973) ist ein US-amerikanischer Komponist, der vor allem für seine Werke für Blasorchester bekannt ist. Seine Musik verbindet zeitgenössische klassische Kompositionstechniken mit Elementen aus Rock, Jazz und Elektronik.",
            Sichtbarkeit = Sichtbarkeit.Oeffentlich
        };
        mackey.Rollen.Add(new PersonRolle { Rolle = PersonRolleTyp.Komponist });
        mackey.Links.Add(new PersonLink { Url = "https://www.johnmackey.com", Typ = LinkTyp.Webseite });
        db.Personen.Add(mackey);

        // Bands / Ensembles (aus den erfassten YouTube-Aufnahmen)
        var jblLuzern = new Band { Id = Guid.Parse("b1000000-0000-0000-0000-000000000001"), Name = "JBL Luzern", Land = "Schweiz", Webseite = "https://www.youtube.com/@jbluzern" };
        var njbo = new Band { Id = Guid.Parse("b1000000-0000-0000-0000-000000000002"), Name = "Nationales Jugendblasorchester (NJBO)", Land = "Schweiz", Webseite = "https://www.youtube.com/@nationalesjugendblasorchester" };
        var stadtmusikLuzern = new Band { Id = Guid.Parse("b1000000-0000-0000-0000-000000000003"), Name = "Blasorchester Stadtmusik Luzern", Land = "Schweiz", Webseite = "https://www.youtube.com/@BlasorchesterStadtmusikLuzern" };
        var hikarigaoka = new Band { Id = Guid.Parse("b1000000-0000-0000-0000-000000000004"), Name = "Hikarigaoka Girls' High School Wind Orchestra", Land = "Japan", Webseite = "https://www.youtube.com/@hkrOG2017" };
        var mackeyChannel = new Band { Id = Guid.Parse("b1000000-0000-0000-0000-000000000005"), Name = "John Mackey (offizieller Kanal)", Land = "USA", Webseite = "https://www.youtube.com/@JohnMackey" };
        db.Bands.AddRange(jblLuzern, njbo, stadtmusikLuzern, hikarigaoka, mackeyChannel);

        // ── Stücke ────────────────────────────────────────────────────────────────
        // Wind Ensemble / Blasorchester
        var redlineTango = Stueck(mackey, "Redline Tango", 2004, Schwierigkeitsgrad.Schwer, "Blasorchester", "Eines der bekanntesten Werke Mackeys – treibende Rhythmen, groovige Passagen, dramatische Höhepunkte.");
        var asphaltCocktail = Stueck(mackey, "Asphalt Cocktail", 2009, Schwierigkeitsgrad.Schwer, "Blasorchester", "Energiegeladenes Werk, inspiriert von der Energie einer belebten Stadt.");
        var auroraAwakes = Stueck(mackey, "Aurora Awakes", 2009, Schwierigkeitsgrad.Schwer, "Blasorchester", "Lyrisches, atmosphärisches Werk über das Erwachen der Morgenröte.");
        var wineDarkSea = Stueck(mackey, "Wine-Dark Sea: Symphony for Band", 2014, Schwierigkeitsgrad.SehrSchwer, "Blasorchester", "Viersätzige Sinfonie – Mackeys ambitioniertestes Werk für Blasorchester.");
        var undertow = Stueck(mackey, "Undertow", 2008, Schwierigkeitsgrad.Mittel, "Blasorchester", "Zieht den Hörer mit unwiderstehlicher Sogwirkung mit.");
        var turbine = Stueck(mackey, "Turbine", 2006, Schwierigkeitsgrad.SehrSchwer, "Blasorchester", "Technisch anspruchsvoll, maschinenhaft, virtuos.");
        var strangeHumorsBand = Stueck(mackey, "Strange Humors (band)", 2006, Schwierigkeitsgrad.Mittel, "Blasorchester", "Für Blasorchester bearbeitete Version des originalen Percussion-Duos.");
        var kingfishersCatchFire = Stueck(mackey, "Kingfishers Catch Fire", 2007, Schwierigkeitsgrad.Schwer, "Blasorchester", "Inspiriert vom Gedicht von Gerard Manley Hopkins.");
        var frozenCathedral = Stueck(mackey, "The Frozen Cathedral", 2013, Schwierigkeitsgrad.SehrSchwer, "Blasorchester", "Atmosphärisch, majestätisch, tiefgründig.");
        var foundry = Stueck(mackey, "Foundry", 2011, Schwierigkeitsgrad.Leicht, "Blasorchester", "Perkussiv, rhythmisch präzise – ideal für jüngere Ensembles.");
        var highWire = Stueck(mackey, "High Wire", 2012, Schwierigkeitsgrad.Schwer, "Blasorchester", "Akrobatisch, schwebend – wie ein Seiltänzer hoch oben.");
        var shelteringSky = Stueck(mackey, "Sheltering Sky", 2012, Schwierigkeitsgrad.Leicht, "Blasorchester", "Ruhig, weiträumig, lyrisch.");
        var xerxes = Stueck(mackey, "Xerxes", 2010, Schwierigkeitsgrad.Mittel, "Blasorchester", "Dramatisch, an antike Schlachtszenen erinnernd.");
        var hymnBlueBand = Stueck(mackey, "Hymn to a Blue Hour (band)", 2010, Schwierigkeitsgrad.Mittel, "Blasorchester", "Meditativ und fließend – eine Hymne an die blaue Stunde.");
        var nightOnFire = Stueck(mackey, "Night on Fire", 2013, Schwierigkeitsgrad.Mittel, "Blasorchester", "Mitreißend, leidenschaftlich, feurig.");
        var ringmastersMarch = Stueck(mackey, "The Ringmaster's March", 2013, Schwierigkeitsgrad.Mittel, "Blasorchester", "Ausgelassen und zirkushaft.");
        var unquietSpirits = Stueck(mackey, "Unquiet Spirits (band)", 2013, Schwierigkeitsgrad.Mittel, "Blasorchester", "Unruhig, spannungsgeladen.");
        var redacted = Stueck(mackey, "[Redacted]", 2013, Schwierigkeitsgrad.Schwer, "Blasorchester", null);
        var theNightGarden = Stueck(mackey, "The Night Garden", 2017, Schwierigkeitsgrad.Schwer, "Blasorchester", "Poetisch und traumbaft.");
        var thisCruelMoon = Stueck(mackey, "This Cruel Moon", 2017, Schwierigkeitsgrad.Mittel, "Blasorchester", null);
        var liminal = Stueck(mackey, "Liminal", 2016, Schwierigkeitsgrad.SehrSchwer, "Blasorchester", "An der Grenze – zwischen Zuständen, Welten, Momenten.");
        var lightningField = Stueck(mackey, "Lightning Field", 2015, Schwierigkeitsgrad.Leicht, "Blasorchester", "Inspiriert von Walter De Marias Kunstinstallation in New Mexico.");
        var rumorSecretKing = Stueck(mackey, "The Rumor of a Secret King (band)", 2018, Schwierigkeitsgrad.Mittel, "Blasorchester", null);
        var snarl = Stueck(mackey, "Snarl", 2018, Schwierigkeitsgrad.Leicht, "Blasorchester", null);
        var untilTheScars = Stueck(mackey, "Until the Scars", 2019, Schwierigkeitsgrad.Mittel, "Blasorchester", null);
        var sacredSpaces = Stueck(mackey, "Sacred Spaces", 2019, Schwierigkeitsgrad.Schwer, "Blasorchester", null);
        var fission = Stueck(mackey, "Fission", 2024, Schwierigkeitsgrad.Schwer, "Blasorchester", null);
        var hauntedObjects = Stueck(mackey, "Haunted Objects (Tsukumogami)", 2024, Schwierigkeitsgrad.Mittel, "Blasorchester", "Inspiriert von japanischen Geistern, die in alten Objekten wohnen.");
        var teethOfTheMechanism = Stueck(mackey, "Teeth of the Mechanism", 2025, Schwierigkeitsgrad.Leicht, "Blasorchester", null);
        var isleFullOfNoises = Stueck(mackey, "The isle is full of noises: Symphony #2", 2026, Schwierigkeitsgrad.SehrSchwer, "Blasorchester", "Zweite Sinfonie, inspiriert von Shakespeares Sturm.");
        var sasparilla = Stueck(mackey, "Sasparilla", 2005, Schwierigkeitsgrad.Schwer, "Blasorchester", null);
        var clocking = Stueck(mackey, "Clocking", 2007, Schwierigkeitsgrad.Mittel, "Blasorchester", null);
        var turning = Stueck(mackey, "Turning", 2007, Schwierigkeitsgrad.SehrSchwer, "Blasorchester", null);
        var nightOnFireAdaptable = Stueck(mackey, "Night on Fire – adaptable", 2021, Schwierigkeitsgrad.Mittel, "Flexible Besetzung", "Flexible Besetzung für verschiedene Ensemble-Grössen.");
        // Blechbläser
        var hymnBlueTromboneChoir = Stueck(mackey, "Hymn to a Blue Hour (trombone choir)", 2012, Schwierigkeitsgrad.Schwer, "Posaunenchor", null);
        var hymnBlueSymphonicBrass = Stueck(mackey, "Hymn to a Blue Hour (symphonic brass)", 2022, Schwierigkeitsgrad.Mittel, "Blechbläser", null);
        var fanfareFullFathomFive = Stueck(mackey, "Fanfare for Full Fathom Five", 2015, Schwierigkeitsgrad.Schwer, "Blechbläser", null);
        var harvestTrombone = Stueck(mackey, "Harvest: Concerto for Trombone", 2009, Schwierigkeitsgrad.SehrSchwer, "Blechbläser", null);
        var asphaltBrassBand = Stueck(mackey, "Asphalt Cocktail (brass band)", 2009, Schwierigkeitsgrad.Schwer, "Brass Band", null);
        var antiqueViolences = Stueck(mackey, "Antique Violences: Concerto for Trumpet", 2017, Schwierigkeitsgrad.SehrSchwer, "Blechbläser", null);
        // Saxophon-Ensemble
        var strangeHumorsSaxQuartet = Stueck(mackey, "Strange Humors (sax quartet)", 2008, Schwierigkeitsgrad.Mittel, "Saxophon-Quartett", null);
        var venomousDevices = Stueck(mackey, "Venomous Devices", 2023, Schwierigkeitsgrad.SehrSchwer, "Saxophon-Ensemble", null);
        var wrongMountainStomp = Stueck(mackey, "Wrong-Mountain Stomp (sax quartet)", 2018, Schwierigkeitsgrad.SehrSchwer, "Saxophon-Quartett", null);
        var lightningFieldSax = Stueck(mackey, "Lightning Field – sax ensemble", 2021, Schwierigkeitsgrad.Mittel, "Saxophon-Ensemble", null);
        var strangeHumorsSaxEnsemble = Stueck(mackey, "Strange Humors (sax ensemble)", 2022, Schwierigkeitsgrad.Mittel, "Saxophon-Ensemble", null);
        var asphaltSaxEnsemble = Stueck(mackey, "Asphalt Cocktail (sax ensemble)", 2009, Schwierigkeitsgrad.Schwer, "Saxophon-Ensemble", null);
        // Konzert / Orchester
        var drumMusic = Stueck(mackey, "Drum Music: Concerto for Percussion", 2011, Schwierigkeitsgrad.SehrSchwer, "Konzert", null);
        var redlineTangoOrch = Stueck(mackey, "Redline Tango (orchestra)", 2004, Schwierigkeitsgrad.Schwer, "Orchester", null);
        var sopranoConcerto = Stueck(mackey, "Soprano Sax Concerto", 2007, Schwierigkeitsgrad.SehrSchwer, "Konzert", null);
        var divineClarinetConcerto = Stueck(mackey, "Divine Mischief: Concerto for Clarinet", 2022, Schwierigkeitsgrad.SehrSchwer, "Konzert", null);
        var concertoPercussionOrch = Stueck(mackey, "Concerto for Percussion & Orchestra", 2000, Schwierigkeitsgrad.Schwer, "Konzert", null);
        var songsFromEndOrch = Stueck(mackey, "Songs from the End of the World (orchestra)", 2019, Schwierigkeitsgrad.Schwer, "Orchester", null);
        // Kammermusik
        var strangeHumorsStrings = Stueck(mackey, "Strange Humors (strings)", 1998, Schwierigkeitsgrad.Mittel, "Streicher", null);
        var strangeHumorsClarinet = Stueck(mackey, "Strange Humors (clarinet)", 2012, Schwierigkeitsgrad.Mittel, "Kammermusik", null);
        var hymnBlueChamber = Stueck(mackey, "Hymn to a Blue Hour (chamber winds)", 2021, Schwierigkeitsgrad.Mittel, "Kammermusik", null);
        var songsFromEnd = Stueck(mackey, "Songs from the End of the World", 2015, Schwierigkeitsgrad.Schwer, "Kammermusik", null);
        var sultana = Stueck(mackey, "Sultana", 2009, Schwierigkeitsgrad.Mittel, "Kammermusik", null);
        var breakdownTango = Stueck(mackey, "Breakdown Tango", 2000, Schwierigkeitsgrad.SehrSchwer, "Kammermusik", null);
        var rushHour = Stueck(mackey, "Rush Hour", 1999, Schwierigkeitsgrad.Schwer, "Kammermusik", null);
        var damn = Stueck(mackey, "Damn", 1998, Schwierigkeitsgrad.Schwer, "Kammermusik", null);
        var mass = Stueck(mackey, "Mass", 2004, Schwierigkeitsgrad.SehrSchwer, "Kammermusik", null);
        var wrongMountainString = Stueck(mackey, "Wrong-Mountain Stomp (string trio)", 2004, Schwierigkeitsgrad.SehrSchwer, "Kammermusik", null);
        var aDeepReverberation = Stueck(mackey, "A deep reverberation fills with stars", 2022, Schwierigkeitsgrad.Schwer, "Blasorchester", null);
        var someTreasures = Stueck(mackey, "Some treasures are heavy with human tears", 2022, Schwierigkeitsgrad.Mittel, "Blasorchester", null);
        var letMeBeFrank = Stueck(mackey, "Let Me Be Frank With You (band)", 2022, Schwierigkeitsgrad.Mittel, "Blasorchester", null);
        var unquietSaxQuartet = Stueck(mackey, "Unquiet Spirits (sax quartet)", 2012, Schwierigkeitsgrad.SehrSchwer, "Saxophon-Quartett", null);
        // Chor / Vokal
        var cradleSong = Stueck(mackey, "Cradle Song", 2021, Schwierigkeitsgrad.Schwer, "Chor", null);
        var rumorSecretKingChoir = Stueck(mackey, "The Rumor of a Secret King (choir)", 2017, Schwierigkeitsgrad.SehrSchwer, "Chor", null);
        var alleluia = Stueck(mackey, "Alleluia", 1992, Schwierigkeitsgrad.Leicht, "Chor", null);
        var placesWeCanNoLongerGo = Stueck(mackey, "Places we can no longer go", 2019, Schwierigkeitsgrad.Schwer, "Vokalmusik", null);
        // Frühe Werke
        var elegieFantasie = Stueck(mackey, "Elegy and Fantasie", 1989, Schwierigkeitsgrad.Mittel, "Kammermusik", null);
        var pianoTrio = Stueck(mackey, "Piano Trio in Two Movements", 1992, Schwierigkeitsgrad.Mittel, "Kammermusik", null);
        var theOtherSide = Stueck(mackey, "The Other Side", 1994, Schwierigkeitsgrad.Leicht, "Kammermusik", null);
        var tango = Stueck(mackey, "Tango", 1991, Schwierigkeitsgrad.Schwer, "Kammermusik", null);
        var momSong = Stueck(mackey, "Mom Song", 1991, Schwierigkeitsgrad.Leicht, "Kammermusik", null);
        var voicesAndEchoes = Stueck(mackey, "Voices and Echoes", 1999, Schwierigkeitsgrad.Mittel, "Kammermusik", null);
        var twelfthNight = Stueck(mackey, "Twelfth Night", 2001, Schwierigkeitsgrad.Mittel, "Musik für Theater", null);
        var doNotGoGentle = Stueck(mackey, "Do Not Go Gentle", 2004, Schwierigkeitsgrad.Mittel, "Orchester", null);
        var underTheRug = Stueck(mackey, "Under the Rug", 2004, Schwierigkeitsgrad.Mittel, "Orchester", null);

        db.Stuecke.AddRange(
            redlineTango, asphaltCocktail, auroraAwakes, wineDarkSea, undertow, turbine,
            strangeHumorsBand, kingfishersCatchFire, frozenCathedral, foundry, highWire,
            shelteringSky, xerxes, hymnBlueBand, nightOnFire, ringmastersMarch, unquietSpirits,
            redacted, theNightGarden, thisCruelMoon, liminal, lightningField, rumorSecretKing,
            snarl, untilTheScars, sacredSpaces, fission, hauntedObjects, teethOfTheMechanism,
            isleFullOfNoises, sasparilla, clocking, turning, nightOnFireAdaptable,
            hymnBlueTromboneChoir, hymnBlueSymphonicBrass, fanfareFullFathomFive, harvestTrombone,
            asphaltBrassBand, antiqueViolences,
            strangeHumorsSaxQuartet, venomousDevices, wrongMountainStomp, lightningFieldSax,
            strangeHumorsSaxEnsemble, asphaltSaxEnsemble,
            drumMusic, redlineTangoOrch, sopranoConcerto, divineClarinetConcerto,
            concertoPercussionOrch, songsFromEndOrch,
            strangeHumorsStrings, strangeHumorsClarinet, hymnBlueChamber, songsFromEnd,
            sultana, breakdownTango, rushHour, damn, mass, wrongMountainString,
            aDeepReverberation, someTreasures, letMeBeFrank, unquietSaxQuartet,
            cradleSong, rumorSecretKingChoir, alleluia, placesWeCanNoLongerGo,
            elegieFantasie, pianoTrio, theOtherSide, tango, momSong, voicesAndEchoes,
            twelfthNight, doNotGoGentle, underTheRug
        );

        // ── Videos ───────────────────────────────────────────────────────────────
        // Verifizierte YouTube-Aufnahmen (Metadaten via YouTube oEmbed bestätigt).
        db.Videos.AddRange(
            Video(wineDarkSea, jblLuzern, "vdaa8F6frWM", "WINE DARK SEA by John Mackey"),
            Video(wineDarkSea, njbo, "RrrXJNPzQNI", "Wine-Dark Sea – World Music Contest Kerkrade, NJBO 2022"),
            Video(auroraAwakes, njbo, "XmbIvlJwsC4", "Aurora Awakes – NJBO 2020"),
            Video(shelteringSky, njbo, "GLyTs-xpk3c", "Sheltering Sky – NJBO 2020"),
            Video(isleFullOfNoises, stadtmusikLuzern, "_8HEtY7VBVQ", "The isle is full of noises (Symphony No. 2) – Uraufführung"),
            Video(antiqueViolences, stadtmusikLuzern, "uEQzHbDI0Zg", "Antique Violences – Stadtmusik Luzern"),
            Video(fanfareFullFathomFive, stadtmusikLuzern, "wpIRr4m_eM4", "Fanfare for Full Fathom Five"),
            Video(divineClarinetConcerto, mackeyChannel, "C2aT-s3udWE", "Divine Mischief (Concerto for Clarinet)"),
            Video(hauntedObjects, hikarigaoka, "WB7qlvexBXo", "付喪神 Tsukumogami (Haunted Objects) – 光ヶ丘女子高等学校吹奏楽部")
        );

        await db.SaveChangesAsync();
    }

    private static Stueck Stueck(Person k, string titel, int? jahr,
        Schwierigkeitsgrad grad, string? besetzung, string? beschreibung) => new()
    {
        Titel = titel,
        Jahr = jahr,
        Schwierigkeitsgrad = grad,
        Besetzung = besetzung,
        Beschreibung = beschreibung,
        OriginalUrl = "https://www.johnmackey.com/music",
        Beitraege = [ new StueckBeitrag { Person = k, Rolle = StueckRolle.Komponist } ]
    };

    private static Video Video(Stueck s, Band b, string youtubeId, string titel) => new()
    {
        StueckId = s.Id,
        BandId = b.Id,
        YouTubeVideoId = youtubeId,
        Titel = titel,
        Status = VideoStatus.Genehmigt
    };
}
