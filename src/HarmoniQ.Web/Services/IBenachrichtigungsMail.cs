namespace HarmoniQ.Web.Services;

/// <summary>
/// Generischer HTML-Mailversand für App-Benachrichtigungen (z. B. Freundschaftsanfragen),
/// unabhängig von den Identity-spezifischen Methoden. Implementiert von denselben Sendern
/// (Resend bzw. SMTP), die auch die Identity-Mails verschicken.
/// </summary>
public interface IBenachrichtigungsMail
{
    Task SendAsync(string to, string subject, string htmlBody);
}
