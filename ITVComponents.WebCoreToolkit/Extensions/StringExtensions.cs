using System;
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
        /// Uebersetzt einen Text, der ENTWEDER Klartext ODER ein JSON-Objekt nach Kultur ist
        /// (<c>{"de":"Freigabe","fr":"Approbation"}</c>). Der Uebersetzungspfad wird nur betreten, wenn der
        /// Wert mit '{' beginnt und mit '}' endet - Klartext bleibt damit unveraendert.
        /// </summary>
        /// <param name="original">Klartext oder Kultur-JSON</param>
        /// <param name="jsonLanguageRecord">die gewuenschte Kultur (z.B. "de-CH")</param>
        /// <returns>
        /// den uebersetzten Text. Die Auswahl trifft <see cref="DictionaryExtensions.Translate{T}"/>:
        /// exakte Kultur, sonst die neutrale (<c>de-CH</c> -&gt; <c>de</c>), sonst der Schluessel
        /// <c>Default</c>, sonst der unveraenderte Eingabewert.
        /// </returns>
        public static string Translate(this string original, string jsonLanguageRecord)
        {
            if (!string.IsNullOrEmpty(jsonLanguageRecord) && !string.IsNullOrEmpty(original) && original.StartsWith("{") && original.EndsWith("}"))
            {
                try
                {
                    var op = JsonHelper.FromJsonString<Dictionary<string, string>>(original, SerializationTypingMode.StaticTyping);
                    original = op.Translate(jsonLanguageRecord, original);
                }
                catch (Exception ex)
                {
                    // Bewusst nicht weiterwerfen: ein kaputter Uebersetzungs-Datensatz darf keine Navigation
                    // und keine Aufgabenliste zerlegen - der Aufrufer bekommt den Rohwert. Ohne diese Zeile
                    // sieht der Benutzer aber nur '{"de":...}' und niemand erfaehrt, warum.
                    LogEnvironment.LogEvent(
                        $"Konnte den Sprach-Datensatz '{original}' nicht fuer '{jsonLanguageRecord}' aufloesen: {ex.OutlineException()}",
                        LogSeverity.Error);
                }
            }

            return original;
        }
    }
}
