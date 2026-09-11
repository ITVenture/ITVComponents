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

    /// <summary>Ein ausgewaehlter Verweis auf eine Ressource - zerlegt, nicht als fertiger Text.</summary>
    /// <remarks>
    /// **Warum zerlegt und nicht als Markdown-Zeichenkette.** Der Editor fuegt einen Verweis ueber
    /// seine eigenen Befehle ein (<c>addImage</c>/<c>addLink</c>), und die brauchen Adresse und Text
    /// getrennt. Rohtext an die Einfuegemarke zu schieben sieht zwar im Quelltext-Modus richtig aus,
    /// landet im WYSIWYG-Modus aber als TEXT im Dokument - beim Speichern wird er dann escaped
    /// (<c>!\[name\](resource:name)</c>), und im fertigen Dokument steht statt des Bildes sein
    /// Alt-Text. <see cref="Markdown"/> gibt es weiterhin fuer Wege ohne Editor.
    /// </remarks>
    public class MarkdownResourceReference
    {
        /// <summary>Bild oder Video - entscheidet ueber Einbettung oder Verweis.</summary>
        public MarkdownResourceKind Kind { get; set; }

        /// <summary>Die Adresse im Text, z.B. <c>resource:handbuch-start</c>.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Alt-Text bzw. Beschriftung des Verweises.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Der fertige Markdown-Verweis - fuer das Rueckfall-Textfeld.</summary>
        public string Markdown
            => Kind == MarkdownResourceKind.Image ? $"![{Text}]({Url})" : $"[{Text}]({Url})";
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
        /// Oeffnet die Auswahl und liefert den gewaehlten Verweis, oder null, wenn abgebrochen wurde.
        /// </summary>
        Task<MarkdownResourceReference?> PickAsync(MarkdownResourceKind kind, CancellationToken ct = default);

        /// <summary>
        /// Legt eine eingefuegte Datei in der Ablage ab und liefert den NAMEN der neuen Ressource
        /// (nicht die Adresse) - der Editor baut daraus den Verweis. Null, wenn es nicht geklappt hat;
        /// die Umsetzung meldet den Grund dem Benutzer und protokolliert ihn.
        /// </summary>
        Task<string?> UploadAsync(MarkdownResourceUpload upload, CancellationToken ct = default);
    }
}
