using System;
using System.Text;

namespace ITVComponents.WebCoreToolkit.Security.ComponentTrust
{
    /// <summary>
    /// Zerlegt assembly-qualifizierte Typnamen, wie sie in den Vertrauens-Eintraegen stehen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Warum das nicht mit <c>Split(',')</c> geht:</b> ein GESCHLOSSENER generischer Typ traegt seine
    /// Typargumente in eckigen Klammern <i>vor</i> dem Assembly-Teil, und jedes Argument ist selbst wieder
    /// assembly-qualifiziert:
    /// <code>
    /// Outer`2[[Arg1, AsmA, Version=...],[Arg2, AsmB, Version=...]], AsmC, Version=...
    ///          ^ das erste Komma - es trennt NICHT Typ von Assembly
    /// </code>
    /// Bei den Typen rund um den Sicherheitskontext mit ihren 45 Typparametern ist dieser Teil einige
    /// Kilobyte lang. Getrennt wird deshalb am ersten Komma auf <b>Klammertiefe 0</b>.
    /// </para>
    /// <para>
    /// <b>Wozu die stelligkeitsfreie Form:</b> im AQN eines generischen Typs steckt die Zahl seiner
    /// Typparameter (<c>`46</c>). Nimmt oder gibt eine Toolkit-Version dem Sicherheitskontext eine
    /// Entitaet, aendert sich diese Zahl bei jedem Typ, der den Kontext generisch fuehrt - und ein
    /// geseedeter Vertrauens-Eintrag passt nicht mehr. Der Vergleich ohne Stelligkeit erkennt genau
    /// diesen Fall und macht aus einem stummen Nichttreffer eine Diagnose.
    /// </para>
    /// </remarks>
    public static class TrustTypeName
    {
        /// <summary>
        /// Trennt einen assembly-qualifizierten Namen in Typ- und Assembly-Teil.
        /// </summary>
        /// <param name="assemblyQualifiedName">der zu zerlegende Name</param>
        /// <param name="typeName">der Typname (mit Stelligkeit und Typargumenten, so wie er dasteht)</param>
        /// <param name="assemblyQualification">
        /// alles hinter dem trennenden Komma (Assemblyname, Version, Kultur, Token) - leer, wenn der Name
        /// gar nicht qualifiziert ist
        /// </param>
        /// <returns>true, wenn ein nicht-leerer Typname herauskam</returns>
        public static bool TrySplit(string assemblyQualifiedName, out string typeName, out string assemblyQualification)
        {
            typeName = null;
            assemblyQualification = null;
            if (string.IsNullOrWhiteSpace(assemblyQualifiedName))
            {
                return false;
            }

            var depth = 0;
            for (var i = 0; i < assemblyQualifiedName.Length; i++)
            {
                var c = assemblyQualifiedName[i];
                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    typeName = assemblyQualifiedName.Substring(0, i).Trim();
                    assemblyQualification = assemblyQualifiedName.Substring(i + 1).Trim();
                    return typeName.Length != 0;
                }
            }

            typeName = assemblyQualifiedName.Trim();
            assemblyQualification = string.Empty;
            return typeName.Length != 0;
        }

        /// <summary>
        /// Liefert den Typteil eines assembly-qualifizierten Namens (oder den Namen selbst, wenn er nicht
        /// qualifiziert ist).
        /// </summary>
        public static string GetTypeName(string assemblyQualifiedName)
            => TrySplit(assemblyQualifiedName, out var typeName, out _) ? typeName : null;

        /// <summary>
        /// Liefert den einfachen Assemblynamen - ohne Version, Kultur und Token.
        /// </summary>
        /// <remarks>
        /// Fuer den Vergleich ist nur dieser Teil brauchbar: ein Bump aendert die Version, und wer nach dem
        /// Grund eines Nichttreffers sucht, will nicht an der Version haengenbleiben.
        /// </remarks>
        public static string GetAssemblySimpleName(string assemblyQualifiedName)
        {
            if (!TrySplit(assemblyQualifiedName, out _, out var qualification) || string.IsNullOrEmpty(qualification))
            {
                return string.Empty;
            }

            var comma = qualification.IndexOf(',');
            return (comma < 0 ? qualification : qualification.Substring(0, comma)).Trim();
        }

        /// <summary>
        /// Liefert den Typnamen ohne Stelligkeit und ohne Typargumente - <c>NS.Outer`2[[...]]</c> wird zu
        /// <c>NS.Outer</c>.
        /// </summary>
        /// <remarks>
        /// Die Typargumente fallen mit weg: aendert sich die Stelligkeit, aendert sich die Argumentliste
        /// ohnehin vollstaendig, und ein Vergleich, der sie mitfuehrte, koennte den Fall nie erkennen, um
        /// dessentwillen es diese Methode gibt. Array- und Zeiger-Suffixe fallen aus demselben Grund
        /// (erste eckige Klammer auf Tiefe 0) mit heraus.
        /// </remarks>
        public static string GetArityFreeName(string typeOrAssemblyQualifiedName)
        {
            var typeName = GetTypeName(typeOrAssemblyQualifiedName);
            if (string.IsNullOrEmpty(typeName))
            {
                return string.Empty;
            }

            var bracket = typeName.IndexOf('[');
            if (bracket >= 0)
            {
                typeName = typeName.Substring(0, bracket);
            }

            var sb = new StringBuilder(typeName.Length);
            for (var i = 0; i < typeName.Length; i++)
            {
                if (typeName[i] == '`')
                {
                    // Die Zahl hinter dem Backtick ueberspringen; alles andere (etwa das '+' geschachtelter
                    // Typen) bleibt stehen, damit NS.A+B nicht mit NS.A verwechselt wird.
                    i++;
                    while (i < typeName.Length && char.IsDigit(typeName[i]))
                    {
                        i++;
                    }

                    i--;
                    continue;
                }

                sb.Append(typeName[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Der Schluessel, unter dem zwei Namen "derselbe Typ" heissen: stelligkeitsfreier Typname plus
        /// einfacher Assemblyname.
        /// </summary>
        public static string GetComparisonKey(string assemblyQualifiedName)
        {
            var name = GetArityFreeName(assemblyQualifiedName);
            if (name.Length == 0)
            {
                return string.Empty;
            }

            var assembly = GetAssemblySimpleName(assemblyQualifiedName);
            return assembly.Length == 0 ? name : $"{name}, {assembly}";
        }

        /// <summary>
        /// Die Stelligkeit des aeussersten Typnamens, oder <c>null</c> bei einem nicht-generischen Typ.
        /// </summary>
        public static int? GetGenericArity(string assemblyQualifiedName)
        {
            var typeName = GetTypeName(assemblyQualifiedName);
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            var bracket = typeName.IndexOf('[');
            if (bracket >= 0)
            {
                typeName = typeName.Substring(0, bracket);
            }

            var tick = typeName.LastIndexOf('`');
            if (tick < 0 || tick == typeName.Length - 1)
            {
                return null;
            }

            var digits = 0;
            for (var i = tick + 1; i < typeName.Length; i++)
            {
                if (!char.IsDigit(typeName[i]))
                {
                    return null;
                }

                digits = (digits * 10) + (typeName[i] - '0');
            }

            return digits;
        }

        /// <summary>
        /// Sagt in einem Satz, worin sich zwei Namen desselben Typs unterscheiden - fuer Protokoll und Maske.
        /// </summary>
        /// <param name="expected">der Name, der gesucht wurde (der Laufzeit-Typ)</param>
        /// <param name="found">der Name, der im Eintrag steht</param>
        public static string DescribeDifference(string expected, string found)
        {
            var expectedArity = GetGenericArity(expected);
            var foundArity = GetGenericArity(found);
            if (expectedArity != foundArity)
            {
                return $"different generic arity (expected {FormatArity(expectedArity)}, the entry says {FormatArity(foundArity)})";
            }

            var expectedAssembly = GetAssemblySimpleName(expected);
            var foundAssembly = GetAssemblySimpleName(found);
            if (!string.Equals(expectedAssembly, foundAssembly, StringComparison.OrdinalIgnoreCase))
            {
                return $"different assembly (expected '{expectedAssembly}', the entry says '{foundAssembly}')";
            }

            return "a different assembly version, culture or public key token";
        }

        /// <summary>
        /// Stellt eine Stelligkeit so dar, wie sie im Typnamen steht (<c>`46</c>) - oder sagt, dass keine da ist.
        /// </summary>
        public static string FormatArity(int? arity) => arity == null ? "no generic arity" : $"`{arity}";
    }
}
