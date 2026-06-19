namespace HarmoniQ.Web.Data.Models;

/// <summary>Tätigkeit einer Person im Musik-Kontext (nicht mit App-Benutzerrollen verwechseln).</summary>
public enum PersonRolleTyp
{
    Komponist = 0,
    Dirigent = 1,
    Musikant = 2,
    /// <summary>Nur-Hörer:in / vernetzte:r Nutzer:in ohne aktive musikalische Tätigkeit
    /// (typischer Start-Status neu registrierter Konten).</summary>
    Zuhoerer = 3
}
