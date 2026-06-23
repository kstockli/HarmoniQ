namespace HarmoniQ.Web.Data.Models;

/// <summary>Zusätzlicher (alternativer) Name einer Band – z. B. „Blasorchester Neuenkirch"
/// neben dem Hauptnamen „Blasorchester Feldmusik Neuenkirch". Wird beim Find-or-create und
/// beim Zusammenführen (Merge) zur Erkennung derselben Band genutzt.</summary>
public class BandAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BandId { get; set; }
    public Band Band { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}
