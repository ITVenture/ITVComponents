using Microsoft.Extensions.Logging;
using QRCoder;
using QRCoder.Exceptions;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

/// <summary>
/// Turns the link of a share into a QR code.
/// </summary>
/// <remarks>
/// Der Weg auf ein Mobilgeraet, an dem niemand einen Link abtippt - und auf Papier, wo ein Link
/// ueberhaupt nicht anklickbar ist. Deshalb entsteht hier ein PNG und kein SVG: dasselbe Bild wird
/// angezeigt, gespeichert und gedruckt, und ein gespeichertes SVG waere in den Werkzeugen, mit denen
/// ein Aushang entsteht, die unbequemere Datei.
/// </remarks>
internal static class ShareQrCode
{
    /// <summary>
    /// Wie viele Bildpunkte ein Modul des Codes wird.
    /// </summary>
    /// <remarks>
    /// Das Bild entsteht EINMAL und dient beiden Zwecken: auf dem Bildschirm wird es per CSS
    /// heruntergerechnet, in die Datei geht es unveraendert. Den unteren Rand setzt also das Drucken -
    /// ein aus 4 Punkten je Modul hochskalierter Code wird unscharf, und ein unscharfer Code ist einer,
    /// den niemand einliest.
    /// </remarks>
    private const int PixelsPerModule = 12;

    /// <summary>
    /// Creates the QR image for the given link.
    /// </summary>
    /// <param name="link">the share link</param>
    /// <param name="logger">the log of the caller - every case that yields no picture lands there</param>
    /// <returns>the picture, or the reason why there is none</returns>
    internal static ShareQrResult Create(string? link, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            logger.LogError("No QR code was built: the share link is empty.");
            return ShareQrResult.Failed("There is no link to turn into a QR code.");
        }

        try
        {
            using var generator = new QRCodeGenerator();

            // ECC M: genug Reserve fuer ein Blatt, das Falten und Fingerabdruecke abbekommt, ohne den Code
            // so gross zu machen, dass ein langer Link gar nicht mehr hineinpasst.
            using var data = generator.CreateQrCode(link, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(data).GetGraphic(PixelsPerModule);
            return ShareQrResult.Ok(Convert.ToBase64String(png));
        }
        catch (DataTooLongException ex)
        {
            // Selten: die Grenze liegt bei ECC M bei rund 2300 Zeichen, und die laengste Linkform, die der
            // Linkbau erzeugt, ist ein Ad-hoc-Ticket mit rund 940. Nur ein Ticket mit sehr vielen oder
            // sehr langen Argumentwerten kommt ueberhaupt in die Naehe. Das ist dann eine Eigenschaft
            // dieser Freigabe und kein Fehlgriff des Benutzers - nachvollziehbar bleiben muss es trotzdem.
            logger.LogWarning(ex, "A share link of {Length} characters does not fit into a QR code.", link.Length);
            return ShareQrResult.Failed(
                "This link is too long for a QR code. Temporary shares carry everything inside the link - "
                + "hand this one over as a link, or create a stored share instead.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not build the QR code for a share link of {Length} characters.", link.Length);
            return ShareQrResult.Failed("The QR code could not be built.");
        }
    }

    /// <summary>
    /// Builds the name the saved picture gets.
    /// </summary>
    /// <remarks>
    /// Aus dem Titel der Freigabe, damit ein Ordner voller Ausdrucke noch unterscheidbar ist. Alles, was
    /// in einem Dateinamen Aerger macht, wird zu einem Bindestrich - der Browser bekommt den Namen so,
    /// wie er hier entsteht, und darf ihn nicht erst reparieren muessen.
    /// </remarks>
    /// <param name="title">the title of the share, may be empty</param>
    /// <returns>a file name ending in .png</returns>
    internal static string FileName(string? title)
    {
        var slug = new string((title ?? string.Empty)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')
            .ToArray())
            .Trim('-');

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        if (slug.Length > 60)
        {
            slug = slug[..60].Trim('-');
        }

        return slug.Length == 0 ? "share-qr.png" : $"{slug}-qr.png";
    }
}

/// <summary>
/// The outcome of building a QR code: a picture, or a sentence saying why there is none.
/// </summary>
internal sealed class ShareQrResult
{
    private ShareQrResult(string? pngBase64, string? error)
    {
        PngBase64 = pngBase64;
        Error = error;
    }

    /// <summary>Gets the PNG, Base64-encoded, or null when none was built.</summary>
    internal string? PngBase64 { get; }

    /// <summary>Gets the reason why no picture was built, or null when one was.</summary>
    internal string? Error { get; }

    /// <summary>Gets a value indicating whether there is a picture.</summary>
    internal bool Success => PngBase64 != null;

    /// <summary>Gets the picture in the form an <c>img</c> element takes.</summary>
    internal string DataUri => $"data:image/png;base64,{PngBase64}";

    internal static ShareQrResult Ok(string pngBase64) => new(pngBase64, null);

    internal static ShareQrResult Failed(string error) => new(null, error);
}
