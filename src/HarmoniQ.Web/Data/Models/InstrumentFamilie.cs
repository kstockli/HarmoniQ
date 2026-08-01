namespace HarmoniQ.Web.Data.Models;

/// <summary>Instrumenten-Familie – bestimmt das Familien-Symbol, solange kein instrumenteigenes
/// <see cref="Instrument.SymbolUrl"/> gesetzt ist (ausbaufähig auf Einzel-Icons).</summary>
public enum InstrumentFamilie
{
    Sonstige = 0,
    Holzblaeser = 1,
    Blechblaeser = 2,
    Schlagwerk = 3,
    Saiten = 4,
    Tasten = 5
}
