namespace HarmoniQ.Web.Data.Models;

/// <summary>Mögliche Instrumente einer Musikant:in (n:m). PK = (PersonId, InstrumentId).</summary>
public class PersonInstrument
{
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public Guid InstrumentId { get; set; }
    public Instrument Instrument { get; set; } = null!;
}
