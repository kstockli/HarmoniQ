namespace HarmoniQ.Web.Data.Models;

/// <summary>Alternativer Name eines Instruments – z. B. „Bassklarinette" neben „Bass-Klarinette"
/// oder „Klarinette in Es" neben „Es-Klarinette". Wird beim Find-or-create (Import/Crawler) zur
/// Erkennung desselben Instruments genutzt. Analog <see cref="BandAlias"/> / <see cref="StueckAlias"/>.</summary>
public class InstrumentAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstrumentId { get; set; }
    public Instrument Instrument { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}
