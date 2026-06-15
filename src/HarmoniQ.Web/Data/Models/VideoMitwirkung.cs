namespace HarmoniQ.Web.Data.Models;

/// <summary>Eine Zeile der Besetzungsliste eines Videos: wer dirigiert / wer spielt was.</summary>
public class VideoMitwirkung
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VideoId { get; set; }
    public Video Video { get; set; } = null!;
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public MitwirkungRolle Rolle { get; set; }

    /// <summary>Bei Musikant:in; bei Dirigent:in null.</summary>
    public Guid? InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    /// <summary>Optionale Stimme (z. B. „1. Klarinette").</summary>
    public Guid? StimmeId { get; set; }
    public Stimme? Stimme { get; set; }

    public string? Anmerkung { get; set; }

    /// <summary>Für User-Vorschläge: Ausstehend bis ein Admin genehmigt.</summary>
    public VideoStatus Status { get; set; } = VideoStatus.Genehmigt;
    public string? VorgeschlagenVonId { get; set; }
    public ApplicationUser? VorgeschlagenVon { get; set; }
}
