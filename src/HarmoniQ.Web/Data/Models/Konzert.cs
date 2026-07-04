namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Ein Konzert / Auftritt, an dem eine oder mehrere <see cref="Band"/>s mitwirken.
/// <see cref="Video"/>s können optional auf ein Konzert verweisen (<see cref="Video.KonzertId"/>).
/// Bewusst schlank: nur <see cref="Datum"/> ist Pflicht, Name/Ort sind optional.
/// </summary>
public class Konzert : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Datum des Konzerts (Pflicht).</summary>
    public DateOnly Datum { get; set; }

    /// <summary>Optionaler Name, z. B. „Jahreskonzert 2025“.</summary>
    public string? Name { get; set; }

    /// <summary>Veranstaltungsort als Referenz (Find-or-create; ersetzt den Freitext-<see cref="Ort"/>).</summary>
    public Guid? LokalId { get; set; }
    public Lokal? Lokal { get; set; }

    /// <summary>Alt/Fallback-Freitext des Orts. Anzeige bevorzugt <see cref="Lokal"/>.Name.</summary>
    public string? Ort { get; set; }

    public string? Beschreibung { get; set; }

    /// <summary>Optionales Plakat/Foto des Konzerts.</summary>
    public string? BildUrl { get; set; }

    public ICollection<KonzertBand> Bands { get; set; } = [];
    public ICollection<KonzertStueck> Programm { get; set; } = [];
    public ICollection<KonzertPerson> Mitwirkende { get; set; } = [];
    public ICollection<Video> Videos { get; set; } = [];
}
