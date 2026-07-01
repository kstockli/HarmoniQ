namespace HarmoniQ.Web.Data.Models;

/// <summary>Alternativ-Name eines <see cref="Lokal"/>s (z. B. „KKL" / „Kultur- und Kongresszentrum
/// Luzern"). Find-or-create matcht Name ODER Alias – analog <see cref="BandAlias"/>/<see cref="StueckAlias"/>.</summary>
public class LokalAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LokalId { get; set; }
    public Lokal Lokal { get; set; } = null!;

    public string Name { get; set; } = null!;
}
