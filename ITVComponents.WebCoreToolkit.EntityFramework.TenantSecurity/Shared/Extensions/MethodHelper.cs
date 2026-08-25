using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions
{
    public static class MethodHelper
    {
        /// <summary>
        /// Sucht in <paramref name="staticClass"/> die generische Methode <paramref name="methodName"/> und
        /// fuellt ihre Typparameter aus dem Sicherheits-Kontext - <b>ueber ihre Namen</b>.
        /// </summary>
        /// <typeparam name="TMethod">die Signatur, als Delegat</typeparam>
        /// <param name="staticClass">die Klasse mit den Registrierungs-Methoden</param>
        /// <param name="contextType">der DbContext, aus dem die Typargumente stammen</param>
        /// <param name="methodName">der Name der gesuchten Methode</param>
        /// <returns>die fertige Methode als Delegat, oder null - dann ist der Grund protokolliert</returns>
        /// <remarks>
        /// <para>
        /// <b>Die Bindung laeuft ueber den NAMEN des Typparameters, nicht ueber seine Position.</b> Damit
        /// sind die Namen faktisch Vertrag: wer einen Typparameter einer Registrierungs-Methode umbenennt,
        /// bricht die Verdrahtung, und kein Compiler sagt etwas dazu - es faellt erst beim Start des Hosts
        /// auf. Genau deshalb meldet dieser Weg jeden Fehlschlag ausdruecklich.
        /// </para>
        /// <para>
        /// Eine einzelne nicht passende Ueberladung ist dabei <b>kein</b> Fehler: die Suche probiert alle
        /// gleichnamigen Methoden und braucht nur eine, die aufgeht. Gemeldet wird deshalb erst, wenn
        /// KEINE passt - dann aber mit allen gesammelten Gruenden, sonst sucht der Leser im Nebel.
        /// </para></remarks>
        public static TMethod GetMethod<TMethod>(this Type staticClass, Type contextType, string methodName)
            where TMethod : Delegate
        {
            var rawTypes = contextType.GetSecurityContextArguments();
            var reasons = new List<string>();
            var candidates = staticClass.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(n => n.IsGenericMethodDefinition && n.Name == methodName)
                .ToArray();
            foreach (MethodInfo candidate in candidates)
            {
                MethodInfo finalized = TryFinalizeMethod(candidate, rawTypes, reasons);
                if (finalized != null)
                {
                    return finalized.CreateDelegate<TMethod>();
                }
            }

            // Ein stilles null waere hier das Schlimmste: der Aufrufer ruft den Delegaten direkt auf und
            // bekommt eine NullReferenceException, deren Meldung nichts ueber die Ursache sagt. Die
            // Registrierung unterbleibt, und was fehlt, merkt man an einer Ansicht, die es nicht gibt.
            LogEnvironment.LogEvent(
                candidates.Length == 0
                    ? $"'{staticClass.FullName}' has no generic method '{methodName}' - the registration "
                      + $"for context '{contextType.FullName}' will NOT happen."
                    : $"None of the {candidates.Length} overload(s) of '{staticClass.FullName}.{methodName}' "
                      + $"could be built for context '{contextType.FullName}' - the registration will NOT "
                      + $"happen. Reasons: {string.Join(" | ", reasons)}",
                LogSeverity.Error);
            return null;
        }

        /// <summary>
        /// Versucht, die Typparameter dieser Ueberladung zu fuellen. Liefert null und traegt den Grund in
        /// <paramref name="reasons"/> ein - gemeldet wird er erst vom Aufrufer, und nur wenn keine
        /// Ueberladung aufgeht.
        /// </summary>
        private static MethodInfo TryFinalizeMethod(MethodInfo method, Dictionary<string, Type> typeArguments,
            List<string> reasons)
        {
            var arg = method.GetGenericArguments();
            var nt = new Type[arg.Length];
            var missing = new List<string>();
            var defaultArgs = Attribute.GetCustomAttributes(method).Where(n => n is CustomGenericTypeArgAttribute).Cast<CustomGenericTypeArgAttribute>().ToArray();
            for (var index = 0; index < arg.Length; index++)
            {
                var t = arg[index];
                if (typeArguments.ContainsKey(t.Name))
                {
                    nt[index] = typeArguments[t.Name];
                }
                else if (defaultArgs.Any(n => n.Name == t.Name))
                {
                    nt[index] = defaultArgs.First(n => n.Name == t.Name).Type;
                }
                else
                {
                    // Nicht abbrechen: ALLE fehlenden Namen sammeln. Wer nach dem ersten aufhoert,
                    // schickt den Leser durch so viele Runden, wie Namen fehlen.
                    missing.Add(t.Name);
                }
            }

            if (missing.Count != 0)
            {
                reasons.Add($"{arg.Length} type argument(s), missing: {string.Join(", ", missing)}");
                return null;
            }

            try
            {
                return method.MakeGenericMethod(nt);
            }
            catch (Exception ex)
            {
                // Alle Namen waren da, die Bedingungen passen trotzdem nicht - der interessantere Fall,
                // deshalb mit vollem Fehlerbild und nicht bloss dessen erster Zeile.
                reasons.Add($"all {arg.Length} names resolved, but the constraints were not satisfied: "
                            + ex.OutlineException());
                return null;
            }
        }
    }
}
