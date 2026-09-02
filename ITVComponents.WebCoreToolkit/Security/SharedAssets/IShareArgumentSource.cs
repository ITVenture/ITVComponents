using System;
using System.Collections.Generic;
using System.Globalization;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Was die aktuell gezeigte Stelle ueber sich selbst weiss - die Argumentwerte, auf die sie sich
    /// bezieht. Gefuellt von der Seite, gelesen vom Teilen-Knopf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Grund fuer diesen Umweg: der Teilen-Knopf sitzt im <b>Mantel</b>, die Werte kennt aber die
    /// <b>Detail-Seite</b>. Beide sehen sich nicht - der Mantel rendert die Seite, nicht umgekehrt.
    /// </para>
    /// <para>
    /// <b>Die Seite erklaert diese Werte ohnehin schon</b>, naemlich dem Riegel
    /// (<c>AssetScope.Args</c> bzw. <c>ISharedAssetContext.Require</c>). Sie ein zweites Mal an den
    /// Knopf zu geben waere nicht nur Doppelarbeit, sondern die Sorte Doppelung, die auseinanderlaeuft:
    /// ein Link, der auf einen Datensatz zeigt, waehrend der Riegel einen anderen erwartet. Deshalb
    /// veroeffentlicht <c>AssetScope</c> hier, was es ohnehin bekommt.
    /// </para>
    /// <para>
    /// Der Halter lebt <b>je Scope</b> (in Blazor: je Circuit). Er ist eine Auskunft, keine Schranke -
    /// ueber Zugriff entscheidet allein der Riegel.
    /// </para>
    /// </remarks>
    public interface IShareArgumentSource
    {
        /// <summary>
        /// Die zuletzt gemeldeten Werte der aktuellen Stelle. Nie null; leer, wenn nichts gemeldet wurde.
        /// </summary>
        IReadOnlyDictionary<string, string> Current { get; }

        /// <summary>
        /// Meldet die Werte der aktuellen Stelle. Ein erneuter Aufruf <b>ersetzt</b> den Stand - eine
        /// Seite beschreibt sich vollstaendig, und der Rest eines frueheren Aufrufs waere die Auskunft
        /// einer Seite, die nicht mehr da ist.
        /// </summary>
        /// <param name="arguments">die Werte, oder null zum Zuruecksetzen</param>
        void Publish(IEnumerable<KeyValuePair<string, object>> arguments);

        /// <summary>Vergisst den gemeldeten Stand.</summary>
        void Clear();
    }

    /// <summary>
    /// Die Standardfassung: ein Woerterbuch je Scope, ohne Benachrichtigung.
    /// </summary>
    /// <remarks>
    /// <b>Bewusst ohne Aenderungsereignis.</b> Der Teilen-Knopf liest die Werte erst beim Klick, nicht
    /// beim Zeichnen - er muss also nicht erfahren, wann sie sich aendern, sondern nur, was gerade gilt.
    /// Ein Ereignis haette hier nur Abmelde-Pflichten erzeugt, die niemand braucht.
    /// </remarks>
    public sealed class ShareArgumentSource : IShareArgumentSource
    {
        private static readonly IReadOnlyDictionary<string, string> Empty =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyDictionary<string, string> current = Empty;

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> Current => current;

        /// <inheritdoc/>
        public void Publish(IEnumerable<KeyValuePair<string, object>> arguments)
        {
            if (arguments == null)
            {
                Clear();
                return;
            }

            var next = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> pair in arguments)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                next[pair.Key] = Text(pair.Value);
            }

            current = next;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            current = Empty;
        }

        /// <summary>
        /// Bringt einen Wert auf die Textform, in der die Freigabe ihn ablegt.
        /// </summary>
        /// <remarks>
        /// <b>Invariant</b> und nicht nach Kultur: derselbe Datensatz muss auf einem deutschen und einem
        /// englischen Arbeitsplatz denselben Link ergeben. Der harte Vergleich passiert spaeter in
        /// <see cref="AssetArgumentValues"/>, aber ein Komma statt eines Punktes waere schon hier
        /// verloren.
        /// </remarks>
        /// <param name="value">der Wert</param>
        /// <returns>die Textform, oder eine leere Zeichenkette</returns>
        private static string Text(object value)
        {
            return value switch
            {
                null => string.Empty,
                string s => s,
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }
    }
}
