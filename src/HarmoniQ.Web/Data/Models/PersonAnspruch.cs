namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Antrag eines eingeloggten Benutzers, mit einer <see cref="Person"/> verknüpft zu werden
/// („das bin ich“). Wird von einer Admin-Person geprüft; bei Genehmigung wird
/// <see cref="Person.BenutzerId"/> gesetzt.
/// </summary>
public class PersonAnspruch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;

    /// <summary>Antragstellendes Benutzerkonto.</summary>
    public string BenutzerId { get; set; } = string.Empty;
    public ApplicationUser? Benutzer { get; set; }

    /// <summary>Optionale Begründung des Antragstellers.</summary>
    public string? Begruendung { get; set; }

    public PersonAnspruchStatus Status { get; set; } = PersonAnspruchStatus.Offen;
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public DateTime? EntschiedenAm { get; set; }
}

public enum PersonAnspruchStatus
{
    Offen = 0,
    Genehmigt = 1,
    Abgelehnt = 2
}
