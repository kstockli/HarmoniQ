namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Technische Audit-Felder (Spalten createtime/createuser/modifytime/modifyuser). Entitäten mit
/// eigenem CRUD-GUI erben davon, damit die Werte direkt bindbar sind (Anzeige in Detail/Tabellen).
/// Automatisch in <c>ApplicationDbContext.SaveChanges</c> gesetzt. Übrige Entitäten tragen dieselben
/// Spalten als Shadow-Properties (nur erfasst, nicht angezeigt).
/// </summary>
public interface IAuditiert
{
    DateTime? CreateTime { get; set; }
    string? CreateUser { get; set; }
    DateTime? ModifyTime { get; set; }
    string? ModifyUser { get; set; }
}

public abstract class AuditierteEntitaet : IAuditiert
{
    public DateTime? CreateTime { get; set; }
    public string? CreateUser { get; set; }
    public DateTime? ModifyTime { get; set; }
    public string? ModifyUser { get; set; }
}
