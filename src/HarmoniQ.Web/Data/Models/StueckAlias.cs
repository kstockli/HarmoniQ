namespace HarmoniQ.Web.Data.Models;

/// <summary>Zusätzlicher (alternativer) Titel eines Stücks – z. B. „Lord Tullamore" neben
/// „Lord Tullamore March". Wird beim Find-or-create (Crawler-Import) und beim Zusammenführen
/// (Merge) zur Erkennung desselben Stücks genutzt. Analog <see cref="BandAlias"/>.</summary>
public class StueckAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StueckId { get; set; }
    public Stueck Stueck { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}
