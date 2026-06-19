namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Gegenseitige Verbindung zweier <see cref="Person"/>en. Eine eingeloggte:r Nutzer:in
/// (mit verknüpfter Person = <see cref="AnfragerPerson"/>) stellt die Anfrage an
/// <see cref="EmpfaengerPerson"/>; diese bestätigt oder lehnt ab. Ist der Status
/// <see cref="FreundschaftStatus.Bestaetigt"/>, sehen beide einander voll (Name + Bild),
/// analog zu Bandkolleg:innen. Die Beziehung wird für die Sichtprüfung in beide Richtungen gewertet.
/// </summary>
public class Freundschaft
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AnfragerPersonId { get; set; }
    public Person AnfragerPerson { get; set; } = null!;

    public Guid EmpfaengerPersonId { get; set; }
    public Person EmpfaengerPerson { get; set; } = null!;

    public FreundschaftStatus Status { get; set; } = FreundschaftStatus.Offen;
    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
    public DateTime? EntschiedenAm { get; set; }
}

public enum FreundschaftStatus
{
    Offen = 0,
    Bestaetigt = 1,
    Abgelehnt = 2
}
