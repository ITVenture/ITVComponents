using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels
{
    /// <summary>
    /// Was die Vorlage ueber die Teilen-Maske vorgibt: welche Felder sie zeigt und womit sie belegt sind.
    /// </summary>
    /// <remarks>
    /// Der Fall dahinter: eine Vorlage, bei der alles vorher feststeht. Der QR-Code am Ladeneingang ist
    /// immer anonym, immer gespeichert, fuehrt kein Argument und heisst immer gleich - uebrig bleibt eine
    /// einzige Entscheidung, naemlich "ja, jetzt ausstellen". Wer dafuer eine Meinung ueber
    /// "Temporary - store nothing" bilden muss, kreuzt es irgendwann versehentlich an und druckt einen
    /// Aushang, der nach einer Stunde tot ist.
    /// <para>
    /// <b>Eine Vorlage ohne diese Angabe verhaelt sich unveraendert.</b> Alles ist sichtbar, alles hat die
    /// bisherigen Vorgaben - genau der heutige Dialog.
    /// </para>
    /// <para>
    /// <b>Unsichtbar heisst fest</b>, nicht "eingeklappt". Wer ein Feld ausblendet, trifft die
    /// Entscheidung an der Vorlage; sie soll dann nicht an der Maske umgangen werden koennen. Ein drittes
    /// Verhalten ("vorbelegt, aber aufklappbar") gibt es bewusst noch nicht - es waere ein zweiter
    /// Begriff fuer einen Fall, den bisher niemand hatte.
    /// </para>
    /// <para>
    /// <b>Warum das hier liegt und nicht im Kern:</b> es beschreibt die Felder EINER Maske, und eines
    /// davon (<see cref="ShareReach"/>) gibt es nur dort - es ist die Zusammenfassung der Benutzer- und
    /// Mandantenfilter zu einer Frage, die man einem Menschen stellen kann. Der Kern reicht den Text der
    /// Vorlage durch (<c>AssetTemplateInfo.ShareDialogConfig</c>) und deutet ihn nicht; was ein Dialog
    /// fuer Felder hat, geht ihn nichts an.
    /// </para>
    /// </remarks>
    public sealed class ShareDialogOptions
    {
        /// <summary>Gilt, wenn die Vorlage nichts sagt: alles sichtbar, nichts vorbelegt.</summary>
        public static readonly ShareDialogOptions Default = new();

        [JsonPropertyName("Title")]
        public ShareFieldOption<string> Title { get; set; } = new();

        [JsonPropertyName("Recipient")]
        public ShareFieldOption<string> Recipient { get; set; } = new();

        [JsonPropertyName("AdHoc")]
        public ShareFieldOption<bool?> AdHoc { get; set; } = new();

        [JsonPropertyName("Lifetime")]
        public ShareFieldOption<int?> Lifetime { get; set; } = new();

        [JsonPropertyName("Anonymous")]
        public ShareFieldOption<bool?> Anonymous { get; set; } = new();

        [JsonPropertyName("Reach")]
        public ShareFieldOption<ShareReach?> Reach { get; set; } = new();

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
            WriteIndented = true
        };

        /// <summary>
        /// Liest die Angabe einer Vorlage.
        /// </summary>
        /// <param name="json">der gespeicherte Text, oder null</param>
        /// <param name="error">warum nichts gelesen werden konnte, sonst null</param>
        /// <returns>
        /// die Angabe, oder <see cref="Default"/>. <b>Nie null</b> - eine unlesbare Angabe darf das Teilen
        /// nicht verhindern, sie darf nur nichts vorgeben.
        /// </returns>
        public static ShareDialogOptions Parse(string json, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return Default;
            }

            try
            {
                return JsonSerializer.Deserialize<ShareDialogOptions>(json, SerializerOptions) ?? Default;
            }
            catch (JsonException ex)
            {
                // Fail open, nicht fail closed: das hier steuert die BEDIENUNG, nicht den Zugriff. Wer die
                // Maske wegen eines Tippfehlers im JSON gar nicht mehr oeffnen kann, hat ein groesseres
                // Problem als eine fehlende Vorbelegung. Der Grund geht aber mit nach oben.
                error = ex.Message;
                return Default;
            }
        }

        /// <summary>Schreibt die Angabe zurueck, oder null, wenn sie nichts sagt.</summary>
        public string ToJson()
            => IsEmpty ? null : JsonSerializer.Serialize(this, SerializerOptions);

        /// <summary>Sagt diese Angabe ueberhaupt etwas?</summary>
        [JsonIgnore]
        public bool IsEmpty
            => Title.IsEmpty && Recipient.IsEmpty && AdHoc.IsEmpty && Lifetime.IsEmpty
               && Anonymous.IsEmpty && Reach.IsEmpty;

        /// <summary>
        /// Prueft, was erst beim Oeffnen der Maske auffallen wuerde: ein ausgeblendetes Pflichtfeld ohne
        /// Vorgabewert.
        /// </summary>
        /// <remarks>
        /// Das ist ein Konfigurationsfehler und kein Bedienfehler - er gehoert deshalb sichtbar gemacht,
        /// sobald jemand die Maske oeffnet, und nicht erst, wenn das Ausstellen scheitert. Wer ein
        /// Pflichtfeld ausblendet, muss sagen, was stattdessen gilt.
        /// </remarks>
        /// <returns>die Beanstandungen, leer wenn alles stimmt</returns>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            if (!Title.Shown && string.IsNullOrWhiteSpace(Title.Value))
            {
                problems.Add("The title is hidden but the template gives no default for it.");
            }

            // Die Reichweite ist nur dann Pflicht, wenn die Freigabe gespeichert und nicht anonym ist -
            // sonst IST sie schon beantwortet. Zaehlt nur, was hier auch wirklich zusammenkommen kann.
            var needsReach = Anonymous.Value != true && AdHoc.Value != true;
            if (needsReach && !Reach.Shown && Reach.Value == null)
            {
                problems.Add("The reach is hidden but the template gives no default for it, "
                             + "and the share is neither anonymous nor temporary.");
            }

            return problems;
        }
    }

    /// <summary>
    /// Ein Feld der Teilen-Maske, so wie die Vorlage es haben will.
    /// </summary>
    /// <typeparam name="T">der Wert des Feldes</typeparam>
    public sealed class ShareFieldOption<T>
    {
        /// <summary>Womit das Feld belegt ist. Fehlt die Angabe, gilt die bisherige Vorgabe.</summary>
        [JsonPropertyName("Value")]
        public T Value { get; set; }

        /// <summary>
        /// Ob das Feld in der Maske erscheint. Fehlt die Angabe, erscheint es - wie bisher.
        /// </summary>
        [JsonPropertyName("Visible")]
        public bool? Visible { get; set; }

        /// <summary>Erscheint das Feld?</summary>
        [JsonIgnore]
        public bool Shown => Visible != false;

        /// <summary>Sagt dieses Feld ueberhaupt etwas?</summary>
        [JsonIgnore]
        public bool IsEmpty => Visible == null && Equals(Value, default(T));
    }
}
