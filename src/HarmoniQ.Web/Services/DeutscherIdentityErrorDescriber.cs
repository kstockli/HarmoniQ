using Microsoft.AspNetCore.Identity;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Deutsche Fehlermeldungen für ASP.NET Core Identity (Passwort-Regeln, doppelte
/// E-Mail/Benutzername, ungültige Tokens usw.).
/// </summary>
public class DeutscherIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) => new()
    { Code = nameof(PasswordTooShort), Description = $"Das Passwort muss mindestens {length} Zeichen lang sein." };

    public override IdentityError PasswordRequiresNonAlphanumeric() => new()
    { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Das Passwort muss mindestens ein Sonderzeichen enthalten (z. B. ! ? . _ -)." };

    public override IdentityError PasswordRequiresDigit() => new()
    { Code = nameof(PasswordRequiresDigit), Description = "Das Passwort muss mindestens eine Ziffer (0–9) enthalten." };

    public override IdentityError PasswordRequiresLower() => new()
    { Code = nameof(PasswordRequiresLower), Description = "Das Passwort muss mindestens einen Kleinbuchstaben (a–z) enthalten." };

    public override IdentityError PasswordRequiresUpper() => new()
    { Code = nameof(PasswordRequiresUpper), Description = "Das Passwort muss mindestens einen Großbuchstaben (A–Z) enthalten." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new()
    { Code = nameof(PasswordRequiresUniqueChars), Description = $"Das Passwort muss mindestens {uniqueChars} verschiedene Zeichen enthalten." };

    public override IdentityError DuplicateEmail(string email) => new()
    { Code = nameof(DuplicateEmail), Description = $"Die E-Mail-Adresse „{email}“ wird bereits verwendet." };

    public override IdentityError DuplicateUserName(string userName) => new()
    { Code = nameof(DuplicateUserName), Description = $"Der Benutzername „{userName}“ ist bereits vergeben." };

    public override IdentityError InvalidEmail(string? email) => new()
    { Code = nameof(InvalidEmail), Description = "Die E-Mail-Adresse ist ungültig." };

    public override IdentityError InvalidToken() => new()
    { Code = nameof(InvalidToken), Description = "Der Link ist ungültig oder abgelaufen. Bitte fordere einen neuen an." };

    public override IdentityError PasswordMismatch() => new()
    { Code = nameof(PasswordMismatch), Description = "Falsches Passwort." };

    public override IdentityError DefaultError() => new()
    { Code = nameof(DefaultError), Description = "Es ist ein unbekannter Fehler aufgetreten." };
}
