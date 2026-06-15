namespace HarmoniQ.Web.Data.Models;

/// <summary>Eine Stimme, die zu einem Instrument gehört (z. B. „1. Klarinette").</summary>
public class Stimme
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstrumentId { get; set; }
    public Instrument Instrument { get; set; } = null!;
    public string Bezeichnung { get; set; } = string.Empty;
}
