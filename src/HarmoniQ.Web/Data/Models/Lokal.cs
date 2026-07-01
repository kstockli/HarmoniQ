namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Veranstaltungsort eines <see cref="Konzert"/>s (z. B. „KKL Luzern"). Ersetzt den früheren
/// Freitext-<c>Ort</c> durch eine referenzierbare Entität – ermöglicht Region-/Kanton-Filter,
/// Gruppierung „Konzerte an diesem Lokal", Dublettenfreiheit (Find-or-create über Name/Alias)
/// und später Karte/Geocoding. Siehe UX-Spec 4.3.
/// </summary>
public class Lokal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = null!;
    /// <summary>Optionaler Saal-/Detailname (z. B. „Konzertsaal").</summary>
    public string? Saal { get; set; }
    public string? Adresse { get; set; }
    public string? Stadt { get; set; }
    /// <summary>Kanton/Region-Kürzel (z. B. „LU") für den „Demnächst"-Region-Filter.</summary>
    public string? Kanton { get; set; }

    /// <summary>Koordinaten (Geocoding via Nominatim – später).</summary>
    public double? Lat { get; set; }
    public double? Lng { get; set; }

    public string? Webseite { get; set; }

    public ICollection<LokalAlias> Aliase { get; set; } = [];
}
