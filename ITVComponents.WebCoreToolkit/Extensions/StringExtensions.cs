using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Json;
using ITVComponents.Logging;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// The records whose fallback was already reported. Translate runs on every render; one line per record
        /// and process is enough to find it, more would bury the log. Bounded by the number of distinct records
        /// the application carries.
        /// </summary>
        private static readonly ConcurrentDictionary<string, byte> ReportedFallbacks = new(StringComparer.Ordinal);

        /// <summary>
        /// Uebersetzt einen Text, der ENTWEDER Klartext ODER ein JSON-Objekt nach Kultur ist
        /// (<c>{"de":"Freigabe","fr":"Approbation"}</c>). Der Uebersetzungspfad wird nur betreten, wenn der
        /// Wert mit '{' beginnt und mit '}' endet - Klartext bleibt damit unveraendert.
        /// </summary>
        /// <param name="original">Klartext oder Kultur-JSON</param>
        /// <param name="jsonLanguageRecord">die gewuenschte Kultur (z.B. "de-CH"); leer (invariante Kultur) heisst <c>Default</c></param>
        /// <returns>
        /// den uebersetzten Text. Die Auswahl trifft <see cref="DictionaryExtensions.Translate{T}"/>:
        /// exakte Kultur, sonst die neutrale (<c>de-CH</c> -&gt; <c>de</c>), sonst der Schluessel
        /// <c>Default</c>, sonst <c>en</c>, sonst der erste Eintrag - nie der rohe Datensatz, ausser er laesst
        /// sich nicht lesen oder ist leer. Der Rueckfall ueber <c>en</c>/den ersten Eintrag wird einmal je
        /// Datensatz und Prozess als Warnung protokolliert: ihm fehlt dann die Sprache und ein <c>Default</c>.
        /// </returns>
        public static string Translate(this string original, string jsonLanguageRecord)
        {
            if (!string.IsNullOrEmpty(original) && original.StartsWith("{") && original.EndsWith("}"))
            {
                try
                {
                    var op = JsonHelper.FromJsonString<Dictionary<string, string>>(original, SerializationTypingMode.StaticTyping);
                    if (op == null || op.Count == 0)
                    {
                        return original;
                    }

                    if (op.TryTranslate(jsonLanguageRecord, out var translated))
                    {
                        return translated;
                    }

                    if (ReportedFallbacks.TryAdd(original, 0))
                    {
                        LogEnvironment.LogEvent(
                            $"The language record '{original}' has neither an entry for '{jsonLanguageRecord}' (nor its language) nor a 'Default'; 'en' or the first entry is shown instead. Further occurrences of this record are not reported.",
                            LogSeverity.Warning);
                    }

                    return op.Fallback(original);
                }
                catch (Exception ex)
                {
                    // Bewusst nicht weiterwerfen: ein kaputter Uebersetzungs-Datensatz darf keine Navigation
                    // und keine Aufgabenliste zerlegen - der Aufrufer bekommt den Rohwert. Ohne diese Zeile
                    // sieht der Benutzer aber nur '{"de":...}' und niemand erfaehrt, warum.
                    LogEnvironment.LogEvent(
                        $"Could not resolve the language record '{original}' for '{jsonLanguageRecord}': {ex.OutlineException()}",
                        LogSeverity.Error);
                }
            }

            return original;
        }
    }
}
