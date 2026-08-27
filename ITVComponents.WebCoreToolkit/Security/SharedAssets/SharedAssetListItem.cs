using System;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Eine Zeile in der Uebersicht der Freigaben eines Mandanten.
    /// </summary>
    public class SharedAssetListItem
    {
        public string AssetKey { get; set; }

        public string AssetTitle { get; set; }

        public string TemplateKey { get; set; }

        public string TemplateTitle { get; set; }

        public string RootPath { get; set; }

        public DateTime? NotBefore { get; set; }

        public DateTime? NotAfter { get; set; }

        /// <summary>
        /// An wen die Freigabe gerichtet ist. Eine Behauptung, kein Nachweis - siehe das Modell.
        /// </summary>
        public string RecipientLabel { get; set; }

        /// <summary>
        /// Worauf sie zeigt, lesbar zusammengefasst (<c>orderId=4711</c>). Leer bei einer reinen
        /// Pfadfreigabe.
        /// </summary>
        public string ArgumentSummary { get; set; }

        /// <summary>
        /// Ob sie anonym zugaenglich ist - also jeder, der den Link hat, hineinkommt.
        /// <b>Die Zeile, die man in einer Uebersicht sehen koennen muss.</b>
        /// </summary>
        public bool IsAnonymous { get; set; }

        /// <summary>
        /// Ob sie an ALLE geht (Platzhalter <c>%</c> in einem der Filter) - die zweite Zeile, die
        /// auffallen muss.
        /// </summary>
        public bool IsPublic { get; set; }
    }
}
