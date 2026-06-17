namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Antrag einer verknüpften Person, einer <see cref="Band"/> beizutreten („Bandmitgliedschaft
/// vorschlagen“). Wird von einer Admin-Person geprüft; bei Genehmigung entsteht eine
/// <see cref="BandMitgliedschaft"/>. Verhindert, dass sich Benutzer:innen selbst in beliebige
/// Bands eintragen (und damit alle dortigen Personen voll sehen).
/// </summary>
public class BandbeitrittAntrag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public Guid BandId { get; set; }
    public Band Band { get; set; } = null!;

    /// <summary>Optionales Instrument in dieser Band.</summary>
    public Guid? InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    /// <summary>Antragstellendes Benutzerkonto.</summary>
    public string? BeantragtVonId { get; set; }
    public ApplicationUser? BeantragtVon { get; set; }

    public PersonAnspruchStatus Status { get; set; } = PersonAnspruchStatus.Offen;
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public DateTime? EntschiedenAm { get; set; }
}
