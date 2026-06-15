namespace HarmoniQ.Web.Data.Models;

/// <summary>Beitrag einer Person zu einem Stück (mehrere je Stück möglich, je mit Rolle).</summary>
public class StueckBeitrag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StueckId { get; set; }
    public Stueck Stueck { get; set; } = null!;
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public StueckRolle Rolle { get; set; } = StueckRolle.Komponist;
}
