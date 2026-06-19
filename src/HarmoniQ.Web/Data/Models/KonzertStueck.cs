namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Programmpunkt eines <see cref="Konzert"/>s: welches <see cref="Stueck"/> von welcher
/// <see cref="Band"/> gespielt wurde. Unabhängig davon, ob eine Aufnahme (<see cref="Video"/>)
/// existiert. Surrogat-PK, da ein Stück mehrfach vorkommen kann (z. B. von zwei Bands).
/// </summary>
public class KonzertStueck
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid KonzertId { get; set; }
    public Konzert Konzert { get; set; } = null!;

    public Guid StueckId { get; set; }
    public Stueck Stueck { get; set; } = null!;

    /// <summary>Welche Band das Stück spielte (optional, falls unbekannt).</summary>
    public Guid? BandId { get; set; }
    public Band? Band { get; set; }

    /// <summary>Optionale Position im Programm.</summary>
    public int? Reihenfolge { get; set; }
}
