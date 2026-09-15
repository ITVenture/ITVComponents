using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.WebCoreToolkit.Security.ComponentTrust
{
    /// <summary>
    /// Prueft gespeicherte Typnamen aus Vertrauens-Eintraegen gegen die geladenen Assemblies.
    /// </summary>
    /// <remarks>
    /// Liegt bewusst hier und nicht beim Provider: die Pruefung braucht keine Datenbank, und sowohl der
    /// Start-Check als auch die Verwaltungsmaske wollen denselben Befund - mit derselben Begruendung.
    /// </remarks>
    public static class TrustTypeResolver
    {
        private const string LogContext = "ITVComponents.WebCoreToolkit.Security.ComponentTrust.TrustTypeResolver";

        /// <summary>
        /// Prueft EINEN gespeicherten Typnamen.
        /// </summary>
        /// <param name="storedName">der Name, wie er in der Zeile steht</param>
        /// <param name="role">
        /// wofuer der Name steht - geht woertlich in die Meldung ein, etwa "trusted type" oder "target type"
        /// </param>
        /// <remarks>
        /// Geprueft wird <b>nicht bloss, ob sich der Typ laden laesst</b>, sondern ob der gespeicherte Name
        /// zeichengenau dem entspricht, was der geladene Typ als <see cref="Type.AssemblyQualifiedName"/>
        /// fuehrt - denn genau so schlaegt der Provider nach. Ein Typ kann sich laden lassen (die Bindung ist
        /// versionstolerant) und der Eintrag trotzdem nie greifen.
        /// </remarks>
        public static TrustTypeDiagnostic Check(string storedName, string role)
        {
            if (string.IsNullOrWhiteSpace(storedName))
            {
                // Das Nachschlagen vergleicht gegen einen echten AssemblyQualifiedName; eine leere Zelle kann
                // also nie treffen. Kein Platzhalter, sondern eine unvollstaendige Zeile.
                return new TrustTypeDiagnostic
                {
                    StoredName = string.Empty,
                    Resolvable = false,
                    ExactMatch = false,
                    Hint = $"the {role} is empty - the lookup compares against a real assembly-qualified name, so this entry can never match"
                };
            }

            Type resolved = null;
            try
            {
                resolved = Type.GetType(storedName, false);
            }
            catch (Exception ex)
            {
                // Ein kaputter Name wirft je nach Form trotz throwOnError:false. Das ist ein Befund, kein
                // Grund, die ganze Pruefung abzubrechen.
                LogEnvironment.LogEvent(
                    $"The {role} '{storedName}' of a trust entry could not be parsed: {ex.OutlineException()}",
                    LogSeverity.Warning, LogContext);
            }

            if (resolved == null)
            {
                var sameType = FindTypeIgnoringArity(storedName);
                return new TrustTypeDiagnostic
                {
                    StoredName = storedName,
                    Resolvable = false,
                    ExactMatch = false,
                    ActualName = sameType?.AssemblyQualifiedName,
                    Hint = sameType != null
                        ? $"the {role} does not resolve, but a loaded type of the same name exists with {TrustTypeName.DescribeDifference(sameType.AssemblyQualifiedName, storedName)}: '{sameType.AssemblyQualifiedName}'. This usually means the entry predates a toolkit upgrade that changed the entity set of the security context."
                        : $"the {role} does not resolve, and no loaded assembly carries a type of that name. Either the assembly is not loaded (yet) or the type is gone."
                };
            }

            var actual = resolved.AssemblyQualifiedName;
            var exact = string.Equals(actual, storedName, StringComparison.Ordinal);
            return new TrustTypeDiagnostic
            {
                StoredName = storedName,
                Resolvable = true,
                ExactMatch = exact,
                ActualName = exact ? null : actual,
                Hint = exact
                    ? null
                    : $"the {role} loads, but under a different name than the one stored ('{actual}'). The lookup compares strings, so this entry will not match."
            };
        }

        /// <summary>
        /// Sucht in den geladenen Assemblies einen Typ, der bis auf die Stelligkeit so heisst wie der
        /// gespeicherte Name.
        /// </summary>
        /// <remarks>
        /// Nur fuer die Diagnose gedacht und entsprechend teuer - sie zieht die Typliste der betroffenen
        /// Assembly. Deshalb laeuft sie erst, wenn das guenstige <see cref="Type.GetType(string,bool)"/>
        /// bereits gescheitert ist.
        /// </remarks>
        public static Type FindTypeIgnoringArity(string storedName)
        {
            var wantedName = TrustTypeName.GetArityFreeName(storedName);
            if (wantedName.Length == 0)
            {
                return null;
            }

            var wantedAssembly = TrustTypeName.GetAssemblySimpleName(storedName);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (wantedAssembly.Length != 0 &&
                    !string.Equals(assembly.GetName().Name, wantedAssembly, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var candidate in SafeGetTypes(assembly))
                {
                    if (string.Equals(TrustTypeName.GetArityFreeName(candidate.FullName), wantedName, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Die Typen einer Assembly, ohne an einer halb ladbaren zu scheitern.
        /// </summary>
        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Was sich laden liess, reicht fuer die Diagnose - aber es gehoert gesagt, dass hier etwas
                // fehlt, sonst liest sich ein "nicht gefunden" als Gewissheit.
                LogEnvironment.LogEvent(
                    $"Only part of the types of '{assembly.GetName().Name}' could be listed while validating trust entries: {ex.OutlineException()}",
                    LogSeverity.Warning, LogContext);
                return ex.Types.Where(n => n != null);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"The types of '{assembly.GetName().Name}' could not be listed while validating trust entries: {ex.OutlineException()}",
                    LogSeverity.Warning, LogContext);
                return Array.Empty<Type>();
            }
        }
    }
}
