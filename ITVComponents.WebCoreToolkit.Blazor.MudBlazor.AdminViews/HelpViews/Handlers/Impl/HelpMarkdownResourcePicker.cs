using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Components.Admin;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Options;
using ITVComponents.WebCoreToolkit.Routing;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Handlers.Impl
{
    /// <summary>
    /// Verbindet den allgemeinen <see cref="IMarkdownResourcePicker"/> der Basis mit der Ressourcen-
    /// Bibliothek des Hilfesystems.
    /// </summary>
    /// <remarks>
    /// **Warum diese Bibliothek und keine eigene Ablage.** Sie ist da: Pflege unter
    /// <c>/Help/Admin/Resources</c>, eine Datei je Sprache, Auslieferung ueber den anonymen Endpunkt
    /// <c>/help/res/{name}</c> - und die Schreibweise <c>resource:</c> versteht der
    /// <c>IHelpContentRenderer</c>, mit dem redaktioneller Markdown-Text ohnehin gerendert wird. Eine
    /// zweite Bilderablage daneben hiesse, Hochladen, Ausliefern und Rechtefrage ein zweites Mal zu
    /// bauen.
    ///
    /// Damit taugt die Bibliothek als allgemeine Bildablage fuer redaktionelle Inhalte - nicht nur
    /// fuer Hilfe-Themen.
    /// </remarks>
    public class HelpMarkdownResourcePicker : IMarkdownResourcePicker
    {
        /// <summary>Wie oft ein Name mit Zaehler nachgereicht wird, wenn er schon vergeben ist.</summary>
        private const int NameAttempts = 6;

        private readonly IHelpResourceHandler resources;
        private readonly IDialogService dialogs;
        private readonly AuthenticationStateProvider authState;
        private readonly ISnackbar snackbar;
        private readonly IGlobalSettings<HelpSystemOptions> options;
        private readonly ILogger<HelpMarkdownResourcePicker> logger;
        private readonly Dictionary<string, string> urlPrefixes;

        /// <summary>Legt eine neue Instanz der <see cref="HelpMarkdownResourcePicker"/>-Klasse an.</summary>
        public HelpMarkdownResourcePicker(IHelpResourceHandler resources, IDialogService dialogs,
            AuthenticationStateProvider authState, ISnackbar snackbar, IUrlFormat urlFormat,
            IGlobalSettings<HelpSystemOptions> options, ILogger<HelpMarkdownResourcePicker> logger)
        {
            this.resources = resources;
            this.dialogs = dialogs;
            this.authState = authState;
            this.snackbar = snackbar;
            this.options = options;
            this.logger = logger;

            // Dieselbe Mandanten-Bindung wie beim Rendern: [SlashPermissionScope] loest im
            // PathSegment-Modus zu /{mandant} auf und sonst zu nichts. Ohne sie zeigte der Editor
            // Bilder eines anderen Mandanten-Pfades - oder gar keine.
            var basePath = urlFormat.FormatUrl("[SlashPermissionScope]" + HelpRoutes.ResourcePrefix) + "/";
            urlPrefixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["resource:"] = basePath,
                // Die zweite Schreibweise, die der Renderer ebenfalls annimmt - Altbestand aus der
                // Zeit vor `resource:`.
                ["/resource/"] = basePath
            };
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> UrlPrefixes => urlPrefixes;

        /// <inheritdoc />
        public async Task<bool> CanPickAsync(CancellationToken ct = default)
            => resources.CanManage(await CurrentUserAsync());

        /// <inheritdoc />
        public async Task<bool> CanUploadAsync(CancellationToken ct = default)
            => resources.CanWrite(await CurrentUserAsync());

        /// <inheritdoc />
        public async Task<MarkdownResourceReference?> PickAsync(MarkdownResourceKind kind,
            CancellationToken ct = default)
        {
            var parameters = new DialogParameters<HelpResourcePickerDialog>
            {
                { x => x.Kind, kind == MarkdownResourceKind.Video ? HelpResourceKind.Video : HelpResourceKind.Image }
            };

            var title = kind == MarkdownResourceKind.Video ? "Insert video" : "Insert image";
            var dialog = await dialogs.ShowAsync<HelpResourcePickerDialog>(title, parameters,
                new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true });

            var result = await dialog.Result;
            return result is { Canceled: false, Data: MarkdownResourceReference reference } ? reference : null;
        }

        /// <inheritdoc />
        public async Task<string?> UploadAsync(MarkdownResourceUpload upload, CancellationToken ct = default)
        {
            var user = await CurrentUserAsync();
            if (!resources.CanWrite(user))
            {
                logger.LogWarning("A pasted image was rejected: the user may not write help resources.");
                snackbar.Add("You are not allowed to add media to the library.", Severity.Warning);
                return null;
            }

            byte[] bytes;
            using (var buffer = new MemoryStream())
            {
                await upload.Content.CopyToAsync(buffer, ct);
                bytes = buffer.ToArray();
            }

            var kind = KindOf(upload.ContentType);
            var folderName = (options.ValueOrDefault ?? new HelpSystemOptions()).PastedMediaFolder?.Trim();

            // Der Name ist der Schluessel der Ressource und steht gleich als `resource:name` im Text.
            // Ihn hier zu erfragen kostet einen Tastendruck; ihn nachtraeglich zu aendern kostet einen
            // Gang in die Ressourcen-Verwaltung UND das Nachziehen jedes Verweises im Text.
            var name = await AskForNameAsync(user, upload, bytes.Length, folderName, ct);
            if (name is null)
            {
                // Abgebrochen - der Editor fuegt dann auch nichts ein.
                return null;
            }

            // SaveResourceAsync meldet eine Namenskollision mit null. Der Dialog hat freie Namen
            // angeboten, aber zwischen Vorschlag und Speichern kann jemand anderes einen belegt haben;
            // deshalb zaehlt dieser Weg im Notfall weiter hoch, statt den Vorgang wegzuwerfen.
            var baseName = name;
            int? resourceId = null;
            for (var attempt = 1; attempt <= NameAttempts && resourceId is null; attempt++)
            {
                name = attempt == 1 ? baseName : $"{baseName}-{attempt}";
                resourceId = await resources.SaveResourceAsync(user, new HelpResourceEditViewModel
                {
                    Name = name,
                    Kind = kind,
                    Description = "Added from the Markdown editor."
                }, ct);
            }

            if (resourceId is null)
            {
                logger.LogError("Could not create a help resource for a pasted file: the names '{BaseName}' "
                                + "through '{LastName}' are all taken.", baseName, name);
                snackbar.Add("Could not store the image: no free name in the library.", Severity.Error);
                return null;
            }

            var error = await resources.SaveResourceFileAsync(user, resourceId.Value, HelpCulture.Default, bytes,
                upload.ContentType, upload.FileName, ct);
            if (error is not null)
            {
                // Die Ressource steht schon, die Datei fehlt - eine leere Ressource waere Muell in der
                // Bibliothek und der Verweis im Text zeigte auf nichts.
                logger.LogError("Could not store the file of the pasted resource '{Name}': {Error}", name, error);
                snackbar.Add(error, Severity.Error);
                if (!await resources.DeleteResourceAsync(user, resourceId.Value, ct))
                {
                    logger.LogError("The empty help resource '{Name}' ({Id}) could not be removed after the "
                                    + "failed upload - it stays in the library without a file.", name, resourceId.Value);
                }

                return null;
            }

            await MoveToUploadFolderAsync(user, resourceId.Value, name, folderName, ct);
            return name;
        }

        /// <summary>
        /// Legt die frisch hochgeladene Ressource in den dafuer vorgesehenen Ordner.
        /// </summary>
        /// <remarks>
        /// Rein kosmetisch, und deshalb bewusst NICHT scharf gestellt: schlaegt es fehl, bleibt die
        /// Ressource in der Wurzel und ist ueber ihren Namen trotzdem erreichbar. Ein abgebrochener
        /// Einfuegevorgang waere der teurere Ausgang.
        /// </remarks>
        private async Task MoveToUploadFolderAsync(ClaimsPrincipal user, int resourceId, string name,
            string? folderName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            try
            {
                var roots = await resources.ListNodesAsync(user, null, ct);
                var folder = roots.FirstOrDefault(n => n.IsFolder
                                                       && string.Equals(n.Name, folderName, StringComparison.OrdinalIgnoreCase));
                if (folder is null)
                {
                    var folderError = await resources.SaveFolderAsync(user, 0, null, folderName, ct);
                    if (folderError is not null)
                    {
                        logger.LogWarning("The folder '{Folder}' for pasted media could not be created ({Error}); "
                                          + "'{Name}' stays at the root.", folderName, folderError, name);
                        return;
                    }

                    roots = await resources.ListNodesAsync(user, null, ct);
                    folder = roots.FirstOrDefault(n => n.IsFolder
                                                       && string.Equals(n.Name, folderName, StringComparison.OrdinalIgnoreCase));
                }

                if (folder is null)
                {
                    logger.LogWarning("The folder '{Folder}' for pasted media was neither found nor created; "
                                      + "'{Name}' stays at the root.", folderName, name);
                    return;
                }

                var moveError = await resources.MoveNodeAsync(user, HelpResourceNodeKey.For(false, resourceId),
                    HelpResourceNodeKey.For(true, folder.Id), ct);
                if (moveError is not null)
                {
                    logger.LogWarning("'{Name}' could not be moved into '{Folder}' ({Error}); it stays at the root.",
                        name, folderName, moveError);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sorting '{Name}' into the folder '{Folder}' failed; it stays at the root.",
                    name, folderName);
            }
        }

        /// <summary>
        /// Erfragt den Namen der neuen Ressource - mit einem Vorschlag, der schon frei ist.
        /// </summary>
        /// <returns>der gewaehlte Name, oder null bei Abbruch</returns>
        /// <remarks>
        /// Die vergebenen Namen werden EINMAL geholt und dem Dialog mitgegeben, statt je Tastendruck
        /// nachzufragen: die Liste ist klein genug, und eine Kollisionsmeldung, die erst beim Speichern
        /// kommt, kostet den Anwender den ganzen Vorgang.
        /// </remarks>
        private async Task<string?> AskForNameAsync(ClaimsPrincipal user, MarkdownResourceUpload upload,
            int byteCount, string? folderName, CancellationToken ct)
        {
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var existing in await resources.ListResourcesAsync(user, ct))
                {
                    taken.Add(existing.Name);
                }
            }
            catch (Exception ex)
            {
                // Ohne die Liste ist der Vorschlag nur schlechter geraten - der Dialog erscheint
                // trotzdem, und das Speichern faengt eine Kollision ohnehin ab.
                logger.LogError(ex, "Could not read the existing resource names; the suggested name may "
                                    + "already be taken.");
            }

            var parameters = new DialogParameters<HelpResourceImportDialog>
            {
                { x => x.SuggestedName, FreeName(BuildName(upload.FileName), taken) },
                { x => x.TakenNames, taken },
                { x => x.Description, Describe(upload, byteCount, folderName) }
            };

            var dialog = await dialogs.ShowAsync<HelpResourceImportDialog>("Add image to the library",
                parameters, new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, CloseButton = true });

            var result = await dialog.Result;
            return result is { Canceled: false, Data: string chosen } ? chosen : null;
        }

        /// <summary>Der erste freie Name: "Image", sonst "Image-2", "Image-3", ...</summary>
        private static string FreeName(string baseName, ICollection<string> taken)
        {
            if (!taken.Contains(baseName))
            {
                return baseName;
            }

            for (var i = 2; i < 1000; i++)
            {
                var candidate = $"{baseName}-{i}";
                if (!taken.Contains(candidate))
                {
                    return candidate;
                }
            }

            // Tausend gleichnamige Bilder sind kein realer Fall - aber ein Vorschlag muss trotzdem
            // herauskommen, und das Speichern prueft ohnehin noch einmal.
            return $"{baseName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        }

        /// <summary>Was da hochgeladen wird - damit im Dialog nicht nur ein nacktes Feld steht.</summary>
        private static string Describe(MarkdownResourceUpload upload, int byteCount, string? folderName)
        {
            var type = string.IsNullOrWhiteSpace(upload.ContentType) ? "unknown type" : upload.ContentType;
            var size = byteCount < 1024 ? $"{byteCount} bytes" : $"{byteCount / 1024} KB";
            var where = string.IsNullOrWhiteSpace(folderName) ? "the root of the library" : $"folder '{folderName}'";
            return $"{type}, {size} - goes into {where}.";
        }

        private async Task<ClaimsPrincipal> CurrentUserAsync()
            => (await authState.GetAuthenticationStateAsync()).User;

        private static HelpResourceKind KindOf(string? contentType)
        {
            if (contentType is null)
            {
                return HelpResourceKind.Other;
            }

            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return HelpResourceKind.Image;
            }

            return contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                ? HelpResourceKind.Video
                : HelpResourceKind.Other;
        }

        /// <summary>
        /// Bildet den Ressourcen-Namen aus dem Dateinamen.
        /// </summary>
        /// <remarks>
        /// Der Name ist zugleich der Alt-Text des eingefuegten Bildes - er soll also lesbar bleiben und
        /// nicht nur eindeutig sein. Aus der Zwischenablage eingefuegte Bilder bringen keinen
        /// Dateinamen mit; fuer sie ist "Image" der Anfang, den der Dialog zu einem freien Namen
        /// hochzaehlt und den der Anwender dort ueberschreiben kann.
        /// </remarks>
        private static string BuildName(string? fileName)
        {
            var raw = string.IsNullOrWhiteSpace(fileName)
                ? null
                : Path.GetFileNameWithoutExtension(fileName).Trim();

            if (string.IsNullOrEmpty(raw))
            {
                return "Image";
            }

            var builder = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                {
                    builder.Append(c);
                }
                else if (builder.Length != 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }

            var name = builder.ToString().Trim('-');
            if (name.Length > 80)
            {
                name = name.Substring(0, 80).TrimEnd('-');
            }

            return name.Length == 0 ? "Image" : name;
        }
    }
}
