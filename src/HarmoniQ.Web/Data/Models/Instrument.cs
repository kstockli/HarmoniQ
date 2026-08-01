namespace HarmoniQ.Web.Data.Models;

/// <summary>Nachschlage-Tabelle für Instrumente (z. B. Klarinette, Trompete).</summary>
public class Instrument : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Familie – bestimmt das Symbol, solange kein instrumenteigenes <see cref="SymbolUrl"/> gesetzt ist.</summary>
    public InstrumentFamilie? Familie { get; set; }

    /// <summary>Optionales instrumenteigenes Symbol (Pfad zu einer SVG). Überschreibt das Familien-Icon.</summary>
    public string? SymbolUrl { get; set; }

    /// <summary>Optionaler Link auf die Wikipedia-Beschreibung des Instruments.</summary>
    public string? WikipediaUrl { get; set; }

    /// <summary>Alternative Schreibweisen (Find-or-create/Merge) – analog Band/Stück/Person.</summary>
    public ICollection<InstrumentAlias> Aliase { get; set; } = [];

    public ICollection<Stimme> Stimmen { get; set; } = [];
}
