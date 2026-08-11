using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets
{
    /// <summary>
    /// Reads a column out of a query row - whatever shape that row has.
    /// </summary>
    /// <remarks>
    /// Es gibt genau zwei Herkuenfte, und die Liste ist abschliessend (<c>ContextForDiagnosticsQuery</c>
    /// laesst nichts anderes zu):
    /// <list type="bullet">
    /// <item>ueber einen <c>DbContext</c> laeuft die Abfrage als LINQ-/CScript-Skript und liefert, was das
    /// Skript zurueckgibt - typischerweise anonyme Typen oder Entitaeten, also <b>Reflection</b>;</item>
    /// <item>ueber einen <c>DynamicDataAdapter</c> kommen laut Signatur
    /// <c>IEnumerable&lt;IDictionary&lt;string, object&gt;&gt;</c>, also <b>Woerterbuecher</b>.</item>
    /// </list>
    /// Dazu der Fall "die Abfrage liefert eine nackte Zahl". Scriban loest das fuer sich selbst; jeder
    /// andere Renderer erbt das Problem - deshalb liegt es hier und nicht in jedem Renderer.
    /// </remarks>
    public static class WidgetRowAccessor
    {
        /// <summary>Reads one column of a row.</summary>
        /// <returns>false when the row has no such column</returns>
        public static bool TryGetValue(object? row, string column, out object? value)
        {
            value = null;
            if (row == null || string.IsNullOrEmpty(column))
            {
                return false;
            }

            // Ein Fall fuer beide Schreibweisen: IDictionary<string, object> und
            // IDictionary<string, object?> sind derselbe Typ.
            if (row is IDictionary<string, object?> map)
            {
                return TryFromMap(map, column, out value);
            }

            PropertyInfo? property = row.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.GetIndexParameters().Length == 0
                                     && string.Equals(p.Name, column, StringComparison.OrdinalIgnoreCase));
            if (property == null)
            {
                return false;
            }

            value = property.GetValue(row);
            return true;
        }

        /// <summary>The column names of a row - for a renderer that has to pick one itself.</summary>
        public static IReadOnlyList<string> Columns(object? row)
        {
            switch (row)
            {
                case null:
                    return Array.Empty<string>();

                case IDictionary<string, object?> typed:
                    return typed.Keys.ToArray();

                default:
                    return row.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.GetIndexParameters().Length == 0)
                        .Select(p => p.Name)
                        .ToArray();
            }
        }

        /// <summary>Reads the same column out of every row.</summary>
        public static IReadOnlyList<object?> Column(IEnumerable<object?>? rows, string column)
        {
            if (rows == null)
            {
                return Array.Empty<object?>();
            }

            var values = new List<object?>();
            foreach (object? row in rows)
            {
                // Fehlt die Spalte in einer Zeile, steht dort null statt einer kuerzeren Liste: eine
                // Zeile stillschweigend zu ueberspringen wuerde Beschriftungen und Werte gegeneinander
                // verschieben.
                TryGetValue(row, column, out object? value);
                values.Add(value);
            }

            return values;
        }

        private static bool TryFromMap(IDictionary<string, object?> map, string column, out object? value)
        {
            if (map.TryGetValue(column, out value))
            {
                return true;
            }

            // Zweiter Anlauf ohne Ruecksicht auf die Schreibweise: Spaltennamen aus einer Datenbank und
            // die Schreibweise im Template stammen selten von derselben Person.
            foreach (KeyValuePair<string, object?> entry in map)
            {
                if (string.Equals(entry.Key, column, StringComparison.OrdinalIgnoreCase))
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
