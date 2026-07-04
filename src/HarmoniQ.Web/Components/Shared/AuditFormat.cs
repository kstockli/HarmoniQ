namespace HarmoniQ.Web.Components.Shared;

/// <summary>Formatierung der Audit-Werte (UTC → lokal) für Tabellen-Zellen.</summary>
public static class AuditFormat
{
    public static string Zeit(DateTime? dt) => dt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "–";
    public static string Wer(string? u) => string.IsNullOrWhiteSpace(u) ? "" : $" · {u}";
}
