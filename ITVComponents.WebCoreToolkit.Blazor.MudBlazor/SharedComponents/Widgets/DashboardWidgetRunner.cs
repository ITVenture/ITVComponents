using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.ServiceShared.Diagnostics;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets
{
    /// <summary>
    /// What a widget template is rendered against.
    /// </summary>
    /// <remarks>
    /// Property names are kept as-is (the renderer uses an identity member-renamer), so a template
    /// addresses <c>{{ Rows }}</c>, <c>{{ Row.Total }}</c>, <c>{{ Count }}</c>. The rows themselves are
    /// passed through exactly as the Diagnostics-Query returned them - a dictionary-per-row for a SQL
    /// query, whatever the script returned for a C# one - so a template uses the column/property names of
    /// that query.
    /// </remarks>
    public sealed class WidgetTemplateModel
    {
        /// <summary>All rows the query returned.</summary>
        public IReadOnlyList<object?> Rows { get; init; } = Array.Empty<object?>();

        /// <summary>
        /// The first row, or null when the query returned nothing - the convenience for the common
        /// single-figure widget that would otherwise need a loop over one row.
        /// </summary>
        public object? Row { get; init; }

        /// <summary>The number of rows.</summary>
        public int Count { get; init; }

        /// <summary>The parameter values the widget was configured with.</summary>
        public IReadOnlyDictionary<string, string?> Params { get; init; }
            = new Dictionary<string, string?>(StringComparer.Ordinal);

        /// <summary>The widget's resolved title.</summary>
        public string Title { get; init; } = string.Empty;
    }

    /// <summary>
    /// Runs a dashboard widget: turns its stored query-string template plus the user's parameter values
    /// into query arguments, executes the Diagnostics-Query and hands back what the template needs.
    /// </summary>
    public static class DashboardWidgetRunner
    {
        /// <summary>
        /// Builds the query arguments from the stored query-string template.
        /// </summary>
        /// <param name="customQueryString">the template, e.g. <c>from={{From}}&amp;to={{To}}</c></param>
        /// <param name="paramValues">the user's parameter values</param>
        /// <param name="logger">receives template errors and duplicate argument names; may be null</param>
        /// <returns>the arguments to pass to the Diagnostics-Query</returns>
        /// <remarks>
        /// The template is split into name/value pairs FIRST and only each value is then rendered. Doing it
        /// the other way round - render the whole string, then split - would let a parameter value that
        /// happens to contain an <c>&amp;</c> introduce further arguments of its own. Percent-escapes in the
        /// stored template are decoded before substitution, never after: otherwise a <c>%</c> inside a
        /// user's value would be read as an escape.
        /// </remarks>
        public static IDictionary<string, string> BuildArguments(
            string? customQueryString,
            IReadOnlyDictionary<string, string?>? paramValues,
            ILogger? logger)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(customQueryString))
            {
                return result;
            }

            string raw = customQueryString.TrimStart('?');
            foreach (string pair in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int split = pair.IndexOf('=');
                string nameTemplate = split < 0 ? pair : pair.Substring(0, split);
                string valueTemplate = split < 0 ? string.Empty : pair.Substring(split + 1);

                string name = Decode(nameTemplate).Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                string value = RenderText(Decode(valueTemplate), paramValues, logger) ?? string.Empty;
                if (result.ContainsKey(name))
                {
                    logger?.LogWarning(
                        "Query-string template contains the argument {Argument} more than once - the last occurrence wins.",
                        name);
                }

                result[name] = value;
            }

            return result;
        }

        /// <summary>
        /// Resolves the text a widget's caption is built from: first the placeholders, then the language.
        /// </summary>
        /// <param name="text">the stored text - plain, a culture record, or either of them with placeholders</param>
        /// <param name="paramValues">the user's parameter values</param>
        /// <param name="logger">receives template errors; may be null</param>
        /// <returns>the caption in the reader's language</returns>
        /// <remarks>
        /// The order matters. Rendering FIRST and translating afterwards means a title template that is
        /// only a placeholder (<c>{{From}}</c>) is not mistaken for a culture record - it starts with '{'
        /// and ends with '}' just like one, and the translation would log a parse error for it. It also
        /// lets a culture record carry placeholders in each of its languages.
        /// </remarks>
        public static string? RenderCaption(
            string? text,
            IReadOnlyDictionary<string, string?>? paramValues,
            ILogger? logger)
        {
            string? rendered = RenderText(text, paramValues, logger);
            return string.IsNullOrEmpty(rendered) ? rendered : WidgetTemplateFunctions.Translate(rendered);
        }

        /// <summary>
        /// Renders a short text template (query-string value, title) against the parameter values.
        /// </summary>
        /// <returns>the rendered text, or the unchanged input when it holds no template at all</returns>
        public static string? RenderText(
            string? text,
            IReadOnlyDictionary<string, string?>? paramValues,
            ILogger? logger)
        {
            // Ohne Platzhalter gar nicht durch Scriban: das spart nicht nur den Aufwand, es haelt auch
            // Zeichen unangetastet, die Scriban sonst deuten wuerde.
            if (string.IsNullOrEmpty(text) || !text.Contains("{{", StringComparison.Ordinal))
            {
                return text;
            }

            try
            {
                Template template = Template.Parse(text);
                if (template.HasErrors)
                {
                    logger?.LogWarning("Text template could not be parsed ({Messages}) - it is used unchanged.",
                        string.Join("; ", template.Messages));
                    return text;
                }

                var globals = new ScriptObject();
                foreach (KeyValuePair<string, string?> parameter in paramValues
                                                                   ?? new Dictionary<string, string?>())
                {
                    globals[parameter.Key] = parameter.Value;
                }

                // Dieselben Funktionen wie im Widget-Template: ein Titel-Template soll uebersetzen koennen,
                // ohne dass der Autor sich merken muss, wo genau er sich befindet.
                WidgetTemplateFunctions.ImportFunctions(globals);
                return template.Render(WidgetTemplateFunctions.CreateContext(globals));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Rendering a text template failed - it is used unchanged.");
                return text;
            }
        }

        /// <summary>
        /// Executes the widget's Diagnostics-Query and builds the model its template is rendered against.
        /// </summary>
        /// <param name="widget">the widget to run</param>
        /// <param name="paramValues">the user's parameter values</param>
        /// <param name="queryService">the host-neutral query service</param>
        /// <param name="context">identity and services the query runs with</param>
        /// <param name="logger">receives execution errors; may be null</param>
        /// <returns>the template model, or null when the query could not be run</returns>
        public static WidgetTemplateModel? Load(
            DashboardWidgetDefinition widget,
            IReadOnlyDictionary<string, string?>? paramValues,
            IDiagnosticsQueryService queryService,
            IDiagnosticsQueryContext context,
            ILogger? logger)
        {
            if (widget?.DiagnosticsQuery == null)
            {
                logger?.LogError("Widget {Widget} has no Diagnostics-Query and cannot be run.",
                    widget?.SystemName);
                return null;
            }

            IDictionary<string, string> arguments =
                BuildArguments(widget.CustomQueryString, paramValues, logger);

            try
            {
                IEnumerable? result = queryService.Execute(
                    widget.DiagnosticsQuery.DiagnosticsQueryName, widget.Area, context, arguments);
                if (result == null)
                {
                    logger?.LogWarning(
                        "Diagnostics-Query {Query} of widget {Widget} returned no result - either it does not exist or access was denied.",
                        widget.DiagnosticsQuery.DiagnosticsQueryName, widget.SystemName);
                    return null;
                }

                // Vollstaendig aufzaehlen, bevor gerendert wird: der Query-Service gibt die Datenquelle
                // erst am ENDE der Aufzaehlung frei. Wer die Aufzaehlung ins Template traegt, rendert
                // gegen eine Quelle, die waehrenddessen zugeht.
                var rows = new List<object?>();
                foreach (object? row in result)
                {
                    rows.Add(row);
                }

                var values = paramValues ?? new Dictionary<string, string?>(StringComparer.Ordinal);
                return new WidgetTemplateModel
                {
                    Rows = rows,
                    Row = rows.FirstOrDefault(),
                    Count = rows.Count,
                    Params = values,
                    // Ueber RenderCaption und nicht ueber RenderText: DisplayName und TitleTemplate duerfen
                    // ein Kultur-JSON sein, und der Titel im Modell soll die Sprache des Lesers tragen -
                    // nicht den Rohsatz aller Sprachen.
                    Title = RenderCaption(
                                string.IsNullOrEmpty(widget.TitleTemplate)
                                    ? widget.DisplayName
                                    : widget.TitleTemplate,
                                values, logger)
                            ?? widget.DisplayName
                            ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                // Ein Widget darf das Dashboard nicht mitnehmen - aber stillschweigend leer bleiben darf
                // es auch nicht: der Aufrufer zeigt eine Meldung, hier landet der Grund.
                logger?.LogError(ex, "Diagnostics-Query {Query} of widget {Widget} failed.",
                    widget.DiagnosticsQuery.DiagnosticsQueryName, widget.SystemName);
                return null;
            }
        }

        private static string Decode(string value)
        {
            try
            {
                return Uri.UnescapeDataString(value);
            }
            catch (UriFormatException)
            {
                // Eine kaputte Prozent-Sequenz im gespeicherten Template: unveraendert weiterverwenden ist
                // besser als das Widget deswegen fallen zu lassen.
                return value;
            }
        }
    }
}
