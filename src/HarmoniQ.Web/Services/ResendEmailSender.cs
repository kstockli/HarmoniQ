using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using HarmoniQ.Web.Data;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Versendet Identity-E-Mails über die Resend-HTTPS-API (statt SMTP). Wird in Umgebungen
/// genutzt, die ausgehenden SMTP blockieren (z. B. Railway). Konfiguration:
/// "Email:Resend:ApiKey" und "Email:From" (verifizierte Absender-Domain bei Resend).
/// </summary>
public class ResendEmailSender(HttpClient http, IConfiguration config, ILogger<ResendEmailSender> logger)
    : IEmailSender<ApplicationUser>, IBenachrichtigungsMail
{
    private readonly string _apiKey = config["Email:Resend:ApiKey"] ?? "";
    private readonly string _from = config["Email:From"] ?? config["Email:User"] ?? "";

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Bestätige dein HarmoniQ-Konto",
            $"<p>Willkommen bei HarmoniQ!</p><p>Bitte bestätige dein Konto, indem du auf diesen Link klickst:</p>" +
            $"<p><a href=\"{confirmationLink}\">Konto bestätigen</a></p>");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "HarmoniQ – Passwort zurücksetzen",
            $"<p>Du kannst dein Passwort über folgenden Link zurücksetzen:</p>" +
            $"<p><a href=\"{resetLink}\">Passwort zurücksetzen</a></p>");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "HarmoniQ – Passwort zurücksetzen",
            $"<p>Dein Code zum Zurücksetzen des Passworts lautet: <strong>{resetCode}</strong></p>");

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            logger.LogWarning("Resend nicht konfiguriert – Nachricht an {To} wird nicht gesendet: {Subject}", to, subject);
            return;
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = JsonContent.Create(new
        {
            from = _from,
            to = new[] { to },
            subject,
            html = htmlBody
        });

        var resp = await http.SendAsync(req);
        if (resp.IsSuccessStatusCode)
        {
            logger.LogInformation("E-Mail (Resend) an {To} gesendet: {Subject}", to, subject);
        }
        else
        {
            var fehler = await resp.Content.ReadAsStringAsync();
            logger.LogError("Resend-Versand an {To} fehlgeschlagen ({Status}): {Fehler}", to, (int)resp.StatusCode, fehler);
        }
    }
}
