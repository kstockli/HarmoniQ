namespace HarmoniQ.Web.Data.Models;

/// <summary>Zusätzlicher (alternativer) Name einer Person – z. B. „J. Mackey" neben „John Mackey"
/// oder ein früherer Name. Wird beim Find-or-create (Import/Crawler) und beim Zusammenführen (Merge)
/// zur Erkennung derselben Person genutzt. Analog <see cref="BandAlias"/> / <see cref="StueckAlias"/>.</summary>
public class PersonAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}
