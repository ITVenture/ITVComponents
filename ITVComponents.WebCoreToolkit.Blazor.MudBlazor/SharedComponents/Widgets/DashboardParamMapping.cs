using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ITVComponents.WebCoreToolkit.EntityFramework.DataSources;
using ITVComponents.WebCoreToolkit.EntityFramework.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets
{
    /// <summary>
    /// The optional, host-neutral shape of a dashboard-parameter's <c>InputConfig</c>.
    /// </summary>
    /// <remarks>
    /// The column historically held a raw Kendo widget configuration (<c>kendoComboBox</c> and friends),
    /// which only the Telerik dashboard could interpret. Unknown properties are therefore ignored rather
    /// than treated as an error: an old Kendo config simply degrades to a plain input of the matching kind
    /// instead of breaking the mask.
    /// </remarks>
    public class WidgetParamConfig
    {
        /// <summary>Overrides the label; without it the parameter name is shown.</summary>
        public string? Label { get; set; }

        /// <summary>Optional hint below the field.</summary>
        public string? HelpText { get; set; }

        /// <summary>Must the parameter be filled before the widget can be added?</summary>
        public bool Required { get; set; }

        /// <summary>Render a text parameter as a multi-line field.</summary>
        public bool Multiline { get; set; }

        /// <summary>Fixed choices for a <see cref="InputType.Combo"/> parameter.</summary>
        public List<WidgetParamChoice>? Choices { get; set; }

        /// <summary>
        /// Foreign-key table the choices of a <see cref="InputType.Combo"/> parameter are read from -
        /// the same source the ForeignKey components use. Ignored when <see cref="Choices"/> is set.
        /// </summary>
        public string? FkTable { get; set; }

        /// <summary>The connection the <see cref="FkTable"/> is read from.</summary>
        public string? Connection { get; set; }
    }

    /// <summary>One fixed choice of a Combo parameter.</summary>
    public class WidgetParamChoice
    {
        /// <summary>The value that goes into the query argument.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>The caption; empty shows the <see cref="Value"/>.</summary>
        public string? Label { get; set; }
    }

    /// <summary>
    /// Translates the stored dashboard-parameter definitions into the field declaration that
    /// <c>DeclaredFieldsForm</c> renders - the same mask the workflow user-tasks and the onboarding
    /// extra-details use. The dashboard does not get an input mask of its own.
    /// </summary>
    public static class DashboardParamMapping
    {
        private static readonly JsonSerializerOptions ConfigOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Builds the field declaration for a widget's parameters.
        /// </summary>
        /// <param name="parameters">the parameter definitions of the widget</param>
        /// <param name="services">used to resolve <c>FkTable</c>-backed choices; may be null</param>
        /// <param name="logger">receives malformed configurations and failing lookups; may be null</param>
        /// <returns>the fields to render, in the order the parameters were defined</returns>
        public static IReadOnlyList<DeclaredField> ToFields(
            IEnumerable<DashboardParamDefinition>? parameters,
            IServiceProvider? services,
            ILogger? logger)
        {
            var result = new List<DeclaredField>();
            if (parameters == null)
            {
                return result;
            }

            foreach (DashboardParamDefinition parameter in parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter?.ParameterName))
                {
                    continue;
                }

                WidgetParamConfig config = ParseConfig(parameter.InputConfig, parameter.ParameterName, logger);
                var field = new DeclaredField
                {
                    Name = parameter.ParameterName,
                    Label = config.Label,
                    HelpText = config.HelpText,
                    Required = config.Required,
                    Kind = KindFor(parameter.InputType, config)
                };

                if (field.Kind == DeclaredFieldKind.Choice)
                {
                    field.Choices = ChoicesFor(config, parameter.ParameterName, services, logger);
                    if (field.Choices.Count == 0)
                    {
                        // Ein Auswahlfeld ohne Auswahl waere eine Sackgasse: der Benutzer koennte ein
                        // Pflichtfeld nicht fuellen. Dann lieber Freitext, als die Maske zu blockieren.
                        logger?.LogWarning(
                            "Dashboard parameter {Parameter} is declared as a choice but no choices could be determined - falling back to a text field.",
                            parameter.ParameterName);
                        field.Kind = DeclaredFieldKind.Text;
                        field.Choices = null;
                    }
                }

                result.Add(field);
            }

            return result;
        }

        private static DeclaredFieldKind KindFor(InputType inputType, WidgetParamConfig config)
            => inputType switch
            {
                InputType.Switch => DeclaredFieldKind.Boolean,
                InputType.Number => DeclaredFieldKind.Number,
                InputType.Combo => DeclaredFieldKind.Choice,
                // MaskedText hat in der gemeinsamen Maske keine Entsprechung - die Maskierung war eine
                // Kendo-Eigenschaft. Das Feld wird zum Textfeld; der Wert ist derselbe, nur ungefuehrt.
                _ => config.Multiline ? DeclaredFieldKind.MultilineText : DeclaredFieldKind.Text
            };

        private static WidgetParamConfig ParseConfig(string? json, string parameterName, ILogger? logger)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new WidgetParamConfig();
            }

            try
            {
                return JsonSerializer.Deserialize<WidgetParamConfig>(json, ConfigOptions) ?? new WidgetParamConfig();
            }
            catch (JsonException ex)
            {
                // Nicht durchwerfen: ein alter Kendo-Config oder ein Tippfehler soll die Maske nicht
                // verhindern. Aber protokolliert wird es, sonst sucht man den fehlenden Hinweistext lange.
                logger?.LogWarning(ex,
                    "InputConfig of dashboard parameter {Parameter} is not a readable configuration object - the parameter is rendered without it.",
                    parameterName);
                return new WidgetParamConfig();
            }
        }

        private static IReadOnlyList<DeclaredChoice> ChoicesFor(
            WidgetParamConfig config,
            string parameterName,
            IServiceProvider? services,
            ILogger? logger)
        {
            if (config.Choices is { Count: > 0 })
            {
                return config.Choices
                    .Where(c => c != null && !string.IsNullOrEmpty(c.Value))
                    .Select(c => new DeclaredChoice { Value = c.Value, Label = c.Label })
                    .ToArray();
            }

            if (string.IsNullOrWhiteSpace(config.FkTable) || services == null)
            {
                return Array.Empty<DeclaredChoice>();
            }

            try
            {
                IWrappedFkSource? source = services.ContextForFkQuery(config.Connection ?? "sys", null);
                if (source == null)
                {
                    logger?.LogWarning(
                        "No foreign-key source for connection {Connection} - choices of dashboard parameter {Parameter} stay empty.",
                        config.Connection ?? "sys", parameterName);
                    return Array.Empty<DeclaredChoice>();
                }

                return source.ReadForeignKey<string>(config.FkTable)
                    .Where(o => o.Key != null)
                    .Select(o => new DeclaredChoice { Value = o.Key, Label = o.Label })
                    .ToArray();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex,
                    "Could not read the choices of dashboard parameter {Parameter} from foreign-key table {Table}.",
                    parameterName, config.FkTable);
                return Array.Empty<DeclaredChoice>();
            }
        }
    }
}
