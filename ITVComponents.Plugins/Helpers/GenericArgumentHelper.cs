using ITVComponents.ExtendedFormatting;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core;
using System;
using System.Collections.Generic;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;

namespace ITVComponents.Plugins.Helpers
{
    /// <summary>
    /// Die Ableitung der Typargumente eines offen-generischen Plugins - fuer ALLE Lader an einer Stelle.
    /// </summary>
    /// <remarks>
    /// Lag vorher Wort fuer Wort in jedem Lader. Zwei Kopien derselben Regel driften auseinander, sobald
    /// eine davon einmal angefasst wird, und der Unterschied faellt erst bei einer Konfiguration auf, die
    /// nur ueber den einen Weg laeuft.
    /// </remarks>
    public static class GenericArgumentHelper
    {
        /// <summary>
        /// Leitet die Typargumente ab und setzt sie fest.
        /// </summary>
        /// <remarks>
        /// Die Zeile mit dem Namen <c>$$genericArgumentProvider</c> benennt keinen Parameter, sondern einen
        /// fertigen TYP, aus dem die uebrigen Argumente abgeleitet werden; alle anderen Zeilen setzen je
        /// einen Parameter. Ohne eine solche Zeile dient <c>typeof(object)</c> als Quelle - dann muessen die
        /// festgesetzten Zeilen allein genuegen.
        /// </remarks>
        public static void BuildGenericArguments(this List<GenericTypeArgument> genericTypeArguments, string uniqueName, GenericTypeDefinition[] list, Dictionary<string,object> customVariables, StringFormatProvider formatter)
        {
            Dictionary<string, object> dic = new Dictionary<string, object>();
            customVariables ??= new Dictionary<string, object>();
            foreach (var n in customVariables)
            {
                // Als SmartProperty und nicht als blanker Wert: der Ausdruck soll den Wert erst beim
                // Auswerten holen.
                dic.Add(n.Key, new SmartProperty
                {
                    GetterMethod = t => n.Value
                });
            }

            List<(string name, Type type)> fixTypes = new List<(string name, Type type)>();
            Type argumentProvider = null;
            foreach (var j in list)
            {
                var t = (Type)ExpressionParser.Parse(j.TypeExpression.ApplyFormat(formatter), dic);
                if (j.TypeParameterName != "$$genericArgumentProvider")
                {
                    fixTypes.Add((name: j.TypeParameterName,
                        type: t));
                }
                else
                {
                    argumentProvider = t;
                }
            }

            if (argumentProvider == null)
            {
                var rawTypes = typeof(object).GetInterfaceGenericArgumentsOf(fixTypeEntries: fixTypes.ToArray());
                if (!genericTypeArguments.FinalizeTypeArguments(rawTypes))
                {
                    throw new InvalidOperationException(
                        $"Unable to finalize Type with given Information for Plugin {uniqueName}.");
                }
            }
            else
            {
                var rawTypes = argumentProvider.GetInterfaceGenericArgumentsOf(fixTypeEntries: fixTypes.ToArray());
                if (!genericTypeArguments.FinalizeTypeArguments(rawTypes))
                {
                    throw new InvalidOperationException(
                        $"Unable to finalize Type with given Information for Plugin {uniqueName}.");
                }
            }
        }
    }
}
