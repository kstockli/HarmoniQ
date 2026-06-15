namespace HarmoniQ.Web.Data.Models;

/// <summary>Ein Link einer Person (Webseite, Social Media …). Beliebig viele je Person.</summary>
public class PersonLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string Url { get; set; } = string.Empty;
    public LinkTyp Typ { get; set; } = LinkTyp.Webseite;
}
