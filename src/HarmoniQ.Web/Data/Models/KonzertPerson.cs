namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Beteiligung einer <see cref="Person"/> an einem <see cref="Konzert"/> mit einer Rolle.
/// Die Rolle ist kontextabhängig: dieselbe Person kann an einem Konzert als Musikant:in
/// auftreten und an einem anderen als Zuhörer:in dabei sein. Bei der Erfassung wird die
/// übliche Rolle der Person vorgeschlagen, ist aber überschreibbar.
/// </summary>
public class KonzertPerson
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid KonzertId { get; set; }
    public Konzert Konzert { get; set; } = null!;

    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;

    public PersonRolleTyp Rolle { get; set; }

    /// <summary>Optional: mit welcher Band die Person auftrat.</summary>
    public Guid? BandId { get; set; }
    public Band? Band { get; set; }
}
