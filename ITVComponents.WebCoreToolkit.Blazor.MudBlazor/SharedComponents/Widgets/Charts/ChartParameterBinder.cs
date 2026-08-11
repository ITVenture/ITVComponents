using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ITVComponents.TypeConversion;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets.Charts
{
    /// <summary>
    /// Turns the remaining fields of a chart declaration into parameters of the chart component.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Grund, warum es diese Klasse gibt und nicht einfach eine Liste erlaubter Felder: die Deklaration
    /// soll nicht vorschreiben, was MudBlazor kann. Was ein Parameter der Komponente ist, weiss die
    /// Komponente selbst - hier wird nachgesehen statt nachgepflegt.
    /// </para>
    /// <para>
    /// <b>Die Pruefung ist dabei kein Beiwerk.</b> <c>MudComponentBase.UserAttributes</c> traegt
    /// <c>[Parameter(CaptureUnmatchedValues = true)]</c>: ein verschriebener Name wirft NICHT, sondern
    /// landet still als HTML-Attribut am Wurzelelement. Die Einstellung waere wirkungslos, und niemand
    /// erfuehre warum.
    /// </para>
    /// </remarks>
    public static class ChartParameterBinder
    {
        /// <summary>
        /// Parameter, die der Renderer selbst setzt oder die sich aus einer Konfiguration nicht bauen
        /// lassen. Sie werden abgewiesen statt uebergangen - wer sie schreibt, erwartet eine Wirkung.
        /// </summary>
        private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(MudChart<double>.ChartType),
            nameof(MudChart<double>.ChartLabels),
            nameof(MudChart<double>.ChartSeries),
            nameof(MudChart<double>.SelectedIndex),
            nameof(MudChart<double>.SelectedIndexChanged),
            "UserAttributes",
            "ChildContent",
            "CustomGraphics",
            "TooltipTemplate",
            "TooltipPositionFunc"
        };

        /// <summary>
        /// Checks and converts the pass-through fields.
        /// </summary>
        /// <param name="extra">the declaration's remaining fields</param>
        /// <param name="componentType">the chart component they are bound to</param>
        /// <param name="errors">receives every field that is unknown, reserved or not convertible</param>
        /// <returns>the parameters to splat onto the component</returns>
        public static Dictionary<string, object?> Bind(IReadOnlyDictionary<string, object?> extra,
            Type componentType, List<string> errors)
        {
            ArgumentNullException.ThrowIfNull(componentType);
            ArgumentNullException.ThrowIfNull(errors);

            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (extra == null || extra.Count == 0)
            {
                return result;
            }

            PropertyInfo[] parameters = componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<ParameterAttribute>() != null)
                .ToArray();

            foreach (KeyValuePair<string, object?> entry in extra)
            {
                if (Reserved.Contains(entry.Key))
                {
                    errors.Add($"'{entry.Key}' is set by the renderer and cannot come from the configuration.");
                    continue;
                }

                PropertyInfo? target = parameters.FirstOrDefault(
                    p => string.Equals(p.Name, entry.Key, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                {
                    errors.Add($"'{entry.Key}' is not a parameter of {componentType.Name}.");
                    continue;
                }

                if (TryConvert(entry.Value, target.PropertyType, out object? converted, out string? why))
                {
                    // Der Parametername in SEINER Schreibweise: Blazor vergleicht die Schluessel des
                    // Woerterbuchs gross-/kleinschreibungsempfindlich.
                    result[target.Name] = converted;
                }
                else
                {
                    errors.Add($"'{entry.Key}': {why}");
                }
            }

            return result;
        }

        private static bool TryConvert(object? value, Type targetType, out object? converted, out string? why)
        {
            converted = null;
            why = null;
            Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (value == null)
            {
                if (type.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                {
                    why = $"no value for a {type.Name}.";
                    return false;
                }

                return true;
            }

            if (type.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            if (type.IsEnum)
            {
                string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                if (Enum.TryParse(type, text, ignoreCase: true, out object? parsed))
                {
                    converted = parsed;
                    return true;
                }

                why = $"'{text}' is not one of {string.Join(", ", Enum.GetNames(type))}.";
                return false;
            }

            if (type.IsArray && value is IEnumerable list and not string)
            {
                Type elementType = type.GetElementType()!;
                object?[] items = list.Cast<object?>().ToArray();
                Array array = Array.CreateInstance(elementType, items.Length);
                for (int i = 0; i < items.Length; i++)
                {
                    if (!TryConvert(items[i], elementType, out object? element, out why))
                    {
                        return false;
                    }

                    array.SetValue(element, i);
                }

                converted = array;
                return true;
            }

            // Ein Objekt-Wert auf einem Objekt-Parameter (chartOptions) wird rekursiv nach derselben Regel
            // befuellt - so muss diese Klasse die Optionen von MudBlazor nicht kennen.
            if (AsMap(value) is IDictionary<string, object?> map && !type.IsPrimitive && type != typeof(string))
            {
                return TryFill(map, type, out converted, out why);
            }

            if (TypeConverter.TryConvert(value, type, out object? result))
            {
                converted = result;
                return true;
            }

            why = $"cannot be read as {type.Name}.";
            return false;
        }

        private static bool TryFill(IDictionary<string, object?> map, Type type, out object? filled, out string? why)
        {
            filled = null;
            why = null;

            Type concrete = type;
            if (type.IsInterface)
            {
                // MudChart nimmt seine Optionen als IChartOptions entgegen; eine Schnittstelle laesst sich
                // nicht erzeugen. Die mitgelieferte Umsetzung ist die einzige sinnvolle Vorgabe.
                if (type.IsAssignableFrom(typeof(ChartOptions)))
                {
                    concrete = typeof(ChartOptions);
                }
                else
                {
                    why = $"{type.Name} is an interface - there is no default implementation to fill.";
                    return false;
                }
            }

            object instance;
            try
            {
                instance = Activator.CreateInstance(concrete)!;
            }
            catch (Exception ex) when (ex is MissingMethodException or MemberAccessException)
            {
                why = $"{concrete.Name} cannot be created from a configuration.";
                return false;
            }

            PropertyInfo[] properties = concrete.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToArray();

            foreach (KeyValuePair<string, object?> entry in map)
            {
                PropertyInfo? target = properties.FirstOrDefault(
                    p => string.Equals(p.Name, entry.Key, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                {
                    why = $"'{entry.Key}' is not a property of {concrete.Name}.";
                    return false;
                }

                if (!TryConvert(entry.Value, target.PropertyType, out object? converted, out why))
                {
                    why = $"'{entry.Key}': {why}";
                    return false;
                }

                target.SetValue(instance, converted);
            }

            filled = instance;
            return true;
        }

        /// <remarks>
        /// Ein Fall genuegt - siehe die Anmerkung an <c>ChartWidgetDeclaration.AsMap</c>.
        /// </remarks>
        private static IDictionary<string, object?>? AsMap(object? raw)
            => raw as IDictionary<string, object?>;
    }
}
