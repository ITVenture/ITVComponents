using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using ITVComponents.WebCoreToolkit.Extensions;
using Scriban;
using Scriban.Runtime;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets
{
    /// <summary>
    /// The functions a widget template can call, and the context they are called in.
    /// </summary>
    /// <remarks>
    /// Scriban knows nothing but its own built-ins; everything a template needs from the application has to
    /// be handed in. Today that is the translation of a culture record - the same selection the rest of the
    /// toolkit uses (navigation captions, task masks), so a widget does not get a second, slightly different
    /// idea of what "de-CH falls back to de" means.
    /// </remarks>
    public static class WidgetTemplateFunctions
    {
        /// <summary>
        /// Builds the globals a widget template is rendered against: the data model plus the functions.
        /// </summary>
        /// <param name="model">the object whose members the template addresses; may be null</param>
        /// <returns>the globals to push onto a <see cref="TemplateContext"/></returns>
        public static ScriptObject CreateGlobals(object? model)
        {
            var globals = new ScriptObject();
            if (model != null)
            {
                // Identitaets-Renamer: die Eigenschaften heissen im Template so, wie sie im Modell heissen.
                // Scribans Vorgabe waere snake_case - dann hiesse {{ Row.OrderId }} plötzlich order_id, und
                // ein Template gegen eine Datenbankspalte waere nicht mehr zu schreiben.
                globals.Import(model, renamer: member => member.Name);
            }

            ImportFunctions(globals);
            return globals;
        }

        /// <summary>
        /// Adds the template functions to an existing globals object - for callers that build their own (the
        /// title and query-string templates run against the parameter values, not against a data model).
        /// </summary>
        public static void ImportFunctions(ScriptObject globals)
        {
            // Beide Schreibweisen: Scriban-Konvention ist klein, die Frage kam als Translate(...). Derselbe
            // Delegat, damit sie nicht auseinanderlaufen koennen.
            var translate = (Func<object?, string>)(value => Translate(value));
            var translateFor = (Func<object?, string?, string>)((value, culture) => Translate(value, culture));
            globals.Import("translate", translate);
            globals.Import("Translate", translate);
            globals.Import("translate_for", translateFor);
            globals.Import("TranslateFor", translateFor);

            // column(Rows, "Status") und json(wert): zusammen halten sie eine Deklaration kurz, die ein
            // Renderer als JSON zurueckliest. Ohne sie muesste jedes Diagramm-Template seine Listen mit
            // einer Schleife und von Hand gesetzten Kommas bauen.
            var column = (Func<object?, string, IReadOnlyList<object?>>)((rows, name)
                => WidgetRowAccessor.Column(rows as IEnumerable<object?>, name));
            var json = (Func<object?, string>)Json;
            globals.Import("column", column);
            globals.Import("Column", column);
            globals.Import("json", json);
            globals.Import("Json", json);
        }

        /// <summary>
        /// Writes a value as JSON - text with the necessary escaping, numbers invariant, lists as arrays.
        /// </summary>
        /// <remarks>
        /// Ohne diese Funktion muesste ein Template seine Anfuehrungszeichen selbst setzen, und ein Wert
        /// mit einem <c>"</c> darin machte aus der Deklaration Text, den niemand mehr lesen kann.
        /// </remarks>
        public static string Json(object? value)
        {
            switch (value)
            {
                case null:
                    return "null";

                case string text:
                    return JsonSerializer.Serialize(text, JsonText);

                case bool flag:
                    return flag ? "true" : "false";

                // Zahlen invariant und ohne Tausendertrennung - eine Deklaration ist keine Anzeige.
                case IFormattable number when value is byte or sbyte or short or ushort or int or uint
                                              or long or ulong or float or double or decimal:
                    return number.ToString(null, CultureInfo.InvariantCulture);

                case IDictionary<string, object?> map:
                    return "{" + string.Join(",",
                        map.Select(e => $"{JsonSerializer.Serialize(e.Key, JsonText)}:{Json(e.Value)}")) + "}";

                case IEnumerable list:
                    return "[" + string.Join(",", list.Cast<object?>().Select(Json)) + "]";

                default:
                    return JsonSerializer.Serialize(value.ToString(), JsonText);
            }
        }

        /// <summary>
        /// Wie Text in die Deklaration geschrieben wird.
        /// </summary>
        /// <remarks>
        /// Der Vorgabe-Encoder maskiert auch Zeichen, die JSON gar nicht stoeren - ein Umlaut wird dort zu
        /// einer sechsstelligen Escape-Sequenz. Gueltig ist beides; lesbar ist nur das eine, und lesen muss
        /// es der, der eine Kachel repariert. Anfuehrungszeichen und Steuerzeichen maskiert auch dieser
        /// Encoder.
        /// </remarks>
        private static readonly JsonSerializerOptions JsonText = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// Creates a render context for a widget template.
        /// </summary>
        public static TemplateContext CreateContext(ScriptObject globals)
        {
            var context = new TemplateContext { MemberRenamer = member => member.Name };
            context.PushGlobal(globals);
            return context;
        }

        /// <summary>
        /// Resolves a culture record for the reader's culture (or the given one).
        /// </summary>
        /// <param name="value">
        /// Either an inline object (<c>Translate({"de":"Freigabe","Default":"Approval"})</c>), or a string
        /// that is plain text or a culture-JSON record - which is what a database column holds, so
        /// <c>Translate(Row.Caption)</c> works as well.
        /// </param>
        /// <param name="culture">the culture to resolve for; empty = the reader's UI culture</param>
        /// <returns>the resolved text; never null</returns>
        /// <remarks>
        /// Selection order: exact culture, then the neutral one (<c>de-CH</c> -&gt; <c>de</c>), then the key
        /// <c>Default</c>. Keys are case-sensitive and <c>Default</c> is written with a capital D - the same
        /// rules the rest of the toolkit follows, so a record can be moved between a navigation caption and a
        /// widget without changing meaning. For the inline-object form there is one addition: if nothing
        /// matches at all, the FIRST entry is used rather than nothing, because a template author who writes
        /// two languages and no <c>Default</c> means the text, not emptiness.
        /// </remarks>
        public static string Translate(object? value, string? culture = null)
        {
            string language = string.IsNullOrWhiteSpace(culture)
                ? CultureInfo.CurrentUICulture.Name
                : culture!;

            // Bei der invarianten Kultur ist der Name LEER, und die String-Erweiterung betritt den
            // Uebersetzungspfad dann gar nicht - der Leser saehe rohes JSON. Auf "Default" zu gehen ist
            // genau das Gemeinte: keine Sprache gewaehlt, also die Vorgabe.
            if (string.IsNullOrEmpty(language))
            {
                language = "Default";
            }

            switch (value)
            {
                case null:
                    return string.Empty;

                // Die bestehende Erweiterung nimmt Klartext UND Kultur-JSON und protokolliert einen kaputten
                // Datensatz selbst. Sie hier wiederzuverwenden ist der Punkt der ganzen Uebung.
                case string text:
                    return text.Translate(language) ?? string.Empty;

                // Ein Objekt-Literal im Template wird zu einem ScriptObject, und das IST ein
                // IDictionary<string, object> - der haeufigste Fall der Frage.
                case IDictionary<string, object> map:
                    return FromMap(map, language);

                default:
                    return value.ToString() ?? string.Empty;
            }
        }

        private static string FromMap(IDictionary<string, object> map, string language)
        {
            // string? und nicht string: der Aufruf unten gibt null als Vorgabewert mit, damit "nichts
            // gefunden" von "leer gefunden" zu unterscheiden ist - damit ist T = string?.
            var flat = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> entry in map)
            {
                if (entry.Key != null)
                {
                    flat[entry.Key] = entry.Value?.ToString() ?? string.Empty;
                }
            }

            string? resolved = flat.Translate(language, null);
            if (!string.IsNullOrEmpty(resolved))
            {
                return resolved;
            }

            // Weder Kultur noch neutral noch Default: die erste Angabe ist besser als nichts.
            return flat.Values.FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? string.Empty;
        }
    }
}
