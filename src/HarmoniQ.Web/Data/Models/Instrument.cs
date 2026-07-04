namespace HarmoniQ.Web.Data.Models;

/// <summary>Nachschlage-Tabelle für Instrumente (z. B. Klarinette, Trompete).</summary>
public class Instrument : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public ICollection<Stimme> Stimmen { get; set; } = [];
}
