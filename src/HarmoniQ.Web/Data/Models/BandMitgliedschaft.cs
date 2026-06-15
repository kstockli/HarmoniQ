using System.ComponentModel.DataAnnotations.Schema;

namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Mitgliedschaft einer <see cref="Person"/> in einer <see cref="Band"/> – über die Zeit
/// (Phase 6, Punkt 22). Optional mit Instrument, Zeitraum (Von/Bis Jahr) und Funktion
/// (z. B. „Chefdirigent“, „Präsident“, „Registerleitung“). Ist <see cref="BisJahr"/> leer,
/// gilt die Mitgliedschaft als aktuell.
/// </summary>
public class BandMitgliedschaft
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BandId { get; set; }
    public Band Band { get; set; } = null!;

    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;

    /// <summary>Optionales Instrument, das die Person in dieser Band spielt.</summary>
    public Guid? InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    /// <summary>Beginn der Mitgliedschaft (Jahr), falls bekannt.</summary>
    public int? VonJahr { get; set; }

    /// <summary>Ende der Mitgliedschaft (Jahr); leer = aktuell aktiv.</summary>
    public int? BisJahr { get; set; }

    /// <summary>Freie Funktionsbezeichnung (z. B. „Chefdirigent“, „Präsident“).</summary>
    public string? Funktion { get; set; }

    [NotMapped]
    public bool IstAktiv => BisJahr is null;
}
