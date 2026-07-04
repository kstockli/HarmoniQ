namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// „Folgen"-Beziehung einer <see cref="Person"/> zu einer <see cref="Band"/> (UX-Spec 4.2):
/// die leichte Fan-/Interesse-Verbindung für die Wiederkehr-Schleife. Im Gegensatz zur
/// <see cref="BandMitgliedschaft"/> erscheint sie <b>nicht</b> im Roster und ist <b>privat</b>
/// (nur die folgende Person sieht sie; kein öffentlicher Fan-Zähler). Erlaubt v. a.
/// Zuhörer:innen, Neuigkeiten einer Band zu erhalten, ohne Mitglied zu sein.
/// </summary>
public class BandInteresse : AuditierteEntitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public Guid BandId { get; set; }
    public Band Band { get; set; } = null!;
}
