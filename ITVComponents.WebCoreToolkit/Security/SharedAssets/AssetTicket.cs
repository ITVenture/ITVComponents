using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Eine Freigabe, die nirgends steht: alles, was sie ausmacht, reist verschluesselt in der URL.
    /// <para>
    /// Gedacht fuer den schmalen Fall - <b>ein Objekt, ein Empfaenger, kurze Frist</b>. Damit entfallen
    /// Empfaengerlisten, Weitergabe und das ganze Filter-Thema; was bleibt, ist der Verweis auf eine
    /// Vorlage und die Argumentwerte.
    /// </para>
    /// <para>
    /// <b>Rechte reisen NICHT mit.</b> Im Ticket steht nur, welche Vorlage gemeint ist - die Rechte
    /// stehen an der Vorlage, und die ist persistiert. Das haelt die URL kurz und macht einen
    /// Grob-Widerruf moeglich: wer die Vorlage abschaltet, entwertet alle Tickets, die auf sie zeigen.
    /// </para>
    /// </summary>
    public sealed class AssetTicket
    {
        /// <summary>Die Vorlage, aus der Rechte, Features und Argumente kommen.</summary>
        [JsonPropertyName("t")]
        public string TemplateKey { get; set; }

        /// <summary>Worauf das Ticket zeigt.</summary>
        [JsonPropertyName("a")]
        public Dictionary<string, string> ArgumentValues { get; set; } = new();

        /// <summary>Der Pfad, auf dem geteilt wurde.</summary>
        [JsonPropertyName("p")]
        public string RootPath { get; set; }

        /// <summary>Ab wann es gilt. Null = sofort.</summary>
        [JsonPropertyName("nb")]
        public DateTime? NotBefore { get; set; }

        /// <summary>
        /// Bis wann es gilt. <b>Bei einem Ticket ist das Pflicht</b> - was nirgends steht, laesst sich
        /// nicht einzeln zurueckziehen, also muss es von selbst enden.
        /// </summary>
        [JsonPropertyName("na")]
        public DateTime NotAfter { get; set; }

        /// <summary>
        /// Die Kennung dieses Tickets. Sie steht in keiner Tabelle - ausser jemand widerruft es; dann
        /// steht sie auf der Sperrliste.
        /// </summary>
        [JsonPropertyName("n")]
        public string Nonce { get; set; }

        /// <summary>
        /// An wen es gerichtet ist. Verschluesselt und damit nicht im Klartext in der URL - eine
        /// E-Mail-Adresse hat dort nichts verloren (Server-Logs, Referer).
        /// </summary>
        [JsonPropertyName("r")]
        public string RecipientLabel { get; set; }
    }
}
