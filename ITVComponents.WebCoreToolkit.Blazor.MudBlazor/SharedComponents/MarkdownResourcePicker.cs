using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents
{
    /// <summary>Die Arten von Medien, die ein <see cref="MarkdownEditor"/> einfuegen kann.</summary>
    public enum MarkdownResourceKind
    {
        /// <summary>Ein Bild - eingefuegt als <c>![name](schema:name)</c>.</summary>
        Image,

        /// <summary>Ein Video - eingefuegt als Verweis, nicht als Bild-Einbettung.</summary>
        Video
    }

    /// <summary>Eine hochzuladende Datei aus dem Editor (eingefuegt oder hineingezogen).</summary>
    public class MarkdownResourceUpload
    {
        /// <summary>Der Inhalt. Der Aufrufer schliesst ihn; die Umsetzung liest ihn nur.</summary>
        public Stream Content { get; set; } = Stream.Null;

        /// <summary>Der MIME-Typ, wie ihn der Browser meldet - kann fehlen.</summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Der Dateiname, wie ihn der Browser meldet. Aus der Zwischenablage eingefuegte Bilder haben
        /// keinen - die Umsetzung muss also einen Namen bilden koennen.
        /// </summary>
        public string? FileName { get; set; }
    }

    /// <summary>
    /// Die Bruecke vom <see cref="MarkdownEditor"/> zu einer Medien-Ablage: Auswaehlen, Hochladen und
    /// das Aufloesen der Schema-Adressen fuer die Anzeige.
    /// </summary>
    /// <remarks>
    /// **Warum eine Abstraktion und nicht direkt das Hilfesystem.** Der Editor gehoert zur Basis und
    /// darf die Ressourcen-Bibliothek der Hilfe nicht kennen - die liegt in den AdminViews und bringt
    /// EF-Kontext und Berechtigungen mit. Umgekehrt soll der Knopf ueberall erscheinen, wo ein
    /// Markdown-Feld steht, nicht nur im Hilfe-Editor. Ist kein Dienst registriert, zeigt der Editor
    /// den Knopf einfach nicht - eine Maske ohne Bild-Ablage bleibt damit vollstaendig benutzbar.
    ///
    /// Die Umsetzung fuer das Hilfesystem kommt mit <c>AddMudBlazorHelpViews</c> und greift auf die
    /// Bibliothek unter <c>/Help/Admin/Resources</c> zu.
    /// </remarks>
    public interface IMarkdownResourcePicker
    {
        /// <summary>
        /// Die Schema-Praefixe, mit denen der Editor Bilder fuer die ANZEIGE aufloest - etwa
        /// <c>"resource:"</c> auf <c>"/{mandant}/help/res/"</c>. Im gespeicherten Text bleibt das
        /// Schema stehen; aufgeloest wird nur das <c>&lt;img src&gt;</c> im Editor.
        /// </summary>
        IReadOnlyDictionary<string, string> UrlPrefixes { get; }

        /// <summary>
        /// Ob der angemeldete Benutzer die Ablage ueberhaupt sehen darf. Steuert, ob der Knopf in der
        /// Werkzeugleiste erscheint - ein Knopf, der nur eine Rechte-Meldung oeffnet, ist keiner.
        /// </summary>
        Task<bool> CanPickAsync(CancellationToken ct = default);

        /// <summary>Ob der Benutzer schreiben darf - steuert das Hochladen eingefuegter Bilder.</summary>
        Task<bool> CanUploadAsync(CancellationToken ct = default);

        /// <summary>
        /// Oeffnet die Auswahl und liefert fertiges Markdown (z.B. <c>![name](resource:name)</c>),
        /// oder null, wenn abgebrochen wurde.
        /// </summary>
        Task<string?> PickAsync(MarkdownResourceKind kind, CancellationToken ct = default);

        /// <summary>
        /// Legt eine eingefuegte Datei in der Ablage ab und liefert den NAMEN der neuen Ressource
        /// (nicht die Adresse) - der Editor baut daraus den Verweis. Null, wenn es nicht geklappt hat;
        /// die Umsetzung meldet den Grund dem Benutzer und protokolliert ihn.
        /// </summary>
        Task<string?> UploadAsync(MarkdownResourceUpload upload, CancellationToken ct = default);
    }
}
