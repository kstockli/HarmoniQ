using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using MimeKit;
using HarmoniQ.Web.Data;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Versendet Identity-E-Mails (Bestätigung, Passwort-Reset) über einen SMTP-Server.
/// Konfiguration unter "Email" (Host/Port/User/Password/From) – Secrets via user-secrets.
/// Nutzt MailKit, da Port 465 implizites SSL (SslOnConnect) verlangt.
/// </summary>
public class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    : IEmailSender<ApplicationUser>, IBenachrichtigungsMail
{
    private readonly string _host = config["Email:Host"] ?? "";
    private readonly int _port = int.TryParse(config["Email:Port"], out var p) ? p : 465;
    private readonly string _user = config["Email:User"] ?? "";
    private readonly string _password = config["Email:Password"] ?? "";
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
        if (string.IsNullOrWhiteSpace(_host))
        {
            logger.LogWarning("E-Mail nicht konfiguriert – Nachricht an {To} wird nicht gesendet: {Subject}", to, subject);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_host, _port, SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(_user, _password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        logger.LogInformation("E-Mail an {To} gesendet: {Subject}", to, subject);
    }
}
