namespace HarmoniQ.Web.Data.Models;

/// <summary>Ein Durchlauf einer <see cref="CrawlQuelle"/> (Spec §5).</summary>
public class CrawlLauf : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid QuelleId { get; set; }
    public CrawlQuelle Quelle { get; set; } = null!;

    public CrawlLaufStatus Status { get; set; } = CrawlLaufStatus.Laufend;

    public DateTime StartAm { get; set; } = DateTime.UtcNow;
    public DateTime? EndeAm { get; set; }

    public int SeitenBesucht { get; set; }
    public int FundeAnzahl { get; set; }

    /// <summary>Fehlertext oder Zusammenfassung.</summary>
    public string? Meldung { get; set; }

    public ICollection<CrawlFund> Funde { get; set; } = [];
}
