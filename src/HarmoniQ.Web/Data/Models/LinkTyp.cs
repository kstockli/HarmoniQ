namespace HarmoniQ.Web.Data.Models;

/// <summary>Art eines Personen-Links.</summary>
public enum LinkTyp
{
    Webseite = 0,
    Instagram = 1,
    X = 2,
    Facebook = 3,
    YouTube = 4,
    EMail = 5,
    Mobile = 6,
    Wikipedia = 7,
    /// <summary>Image-/Vorstellungsfilm (YouTube- oder direkte Datei-URL); wird eingebettet abgespielt.</summary>
    Imagefilm = 8,
    Sonstige = 99
}
