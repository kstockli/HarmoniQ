namespace HarmoniQ.Web.Data.Models;

/// <summary>Welche Rolle(n) eine Person grundsätzlich ausübt. PK = (PersonId, Rolle).</summary>
public class PersonRolle
{
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public PersonRolleTyp Rolle { get; set; }
}
