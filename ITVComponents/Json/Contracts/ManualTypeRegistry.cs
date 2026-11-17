using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace ITVComponents.Json.Contracts
{
    /// <summary>
    /// Vergibt <b>stabile Kurznamen</b> fuer Typen, die ueber
    /// <see cref="SerializationTypingMode.AssistedPolymorphism"/> serialisiert werden - und entscheidet,
    /// ob beim Lesen ueberhaupt etwas anderes als ein registrierter Typ aufgeloest werden darf.
    /// </summary>
    /// <remarks>
    /// Ohne Registrierung schreibt <see cref="ManualSerializationData.FromValue"/> den
    /// <c>AssemblyQualifiedName</c> - das ist das bisherige Verhalten und fuer die Prozesskommunikation
    /// auch das richtige: dort reisen beliebige Methodenparameter, die sich nicht vorab anmelden lassen,
    /// und beide Seiten laufen aus demselben Build.
    /// <para>
    /// Fuer <b>langlebig abgelegte</b> Daten taugt der AssemblyQualifiedName dagegen schlecht. Er
    /// enthaelt Assembly und Version, also genau die Dinge, die ein Refactoring aendert: eine umbenannte
    /// Klasse laesst sich nach Wochen nicht mehr aufloesen. Und er erlaubt beim Lesen das Laden eines
    /// <i>beliebigen</i> Typs - was fuer eine Datenbank-Spalte eine ganz andere Angriffsflaeche ist als
    /// fuer einen prozessinternen Kanal. Ein registrierter Kurzname loest beides: er ueberlebt den Umzug
    /// der Klasse, und er ist eine Positivliste.
    /// </para></remarks>
    public static class ManualTypeRegistry
    {
        private static readonly ConcurrentDictionary<string, Type> byAlias =
            new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<Type, string> byType =
            new ConcurrentDictionary<Type, string>();

        private static readonly AsyncLocal<bool> restricted = new AsyncLocal<bool>();

        /// <summary>
        /// Types that may be read under the restriction without a short name - written, as before, with their
        /// AssemblyQualifiedName (see <see cref="Allow"/>).
        /// </summary>
        private static readonly ConcurrentDictionary<Type, byte> allowed = new ConcurrentDictionary<Type, byte>();

        /// <summary>Das Anhaengsel, mit dem eine Sammlung ueber ihrem Elementtyp benannt wird.</summary>
        private const string ArraySuffix = "[]";

        static ManualTypeRegistry()
        {
            // Die Grundtypen sind ab Werk angemeldet. Ohne sie waere die Beschraenkung auf registrierte
            // Typen unbenutzbar - eine schlichte Zeichenketten-Variable liesse sich nicht mehr lesen. Die
            // Namen sind bewusst kurz: sie stehen in jedem abgelegten Datensatz.
            Register(typeof(string), "string");
            Register(typeof(bool), "bool");
            Register(typeof(byte), "byte");
            Register(typeof(sbyte), "sbyte");
            Register(typeof(short), "short");
            Register(typeof(ushort), "ushort");
            Register(typeof(int), "int");
            Register(typeof(uint), "uint");
            Register(typeof(long), "long");
            Register(typeof(ulong), "ulong");
            Register(typeof(float), "float");
            Register(typeof(double), "double");
            Register(typeof(decimal), "decimal");
            Register(typeof(char), "char");
            Register(typeof(DateTime), "datetime");
            Register(typeof(DateTimeOffset), "datetimeoffset");
            Register(typeof(TimeSpan), "timespan");
            Register(typeof(Guid), "guid");
        }

        /// <summary>
        /// Gilt gerade die Beschraenkung auf registrierte Typen? Siehe
        /// <see cref="RestrictToRegisteredTypes"/>.
        /// </summary>
        public static bool Restricted => restricted.Value;

        /// <summary>
        /// Meldet einen Typ unter einem stabilen Kurznamen an. Ab dann wird dieser Name geschrieben (statt
        /// des AssemblyQualifiedName) und beim Lesen wieder auf den Typ abgebildet.
        /// </summary>
        /// <remarks>
        /// Der Name gehoert zum <b>Datenformat</b> - er darf sich nicht mehr aendern, sobald etwas damit
        /// abgelegt wurde. Der Typ dahinter darf umziehen, umbenannt werden, die Assembly wechseln.
        /// Mehrfach-Registrierung desselben Paares ist folgenlos; ein zweiter Typ unter demselben Namen
        /// (oder ein zweiter Name fuer denselben Typ) ist ein Programmierfehler und wirft.
        /// </remarks>
        public static void Register(Type type, string alias)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new ArgumentException("A type alias must not be empty.", nameof(alias));
            }

            Type existing = byAlias.GetOrAdd(alias, type);
            if (existing != type)
            {
                throw new InvalidOperationException(
                    $"The type alias '{alias}' is already taken by '{existing.FullName}' and cannot be " +
                    $"reused for '{type.FullName}' - it is part of the stored format.");
            }

            string existingAlias = byType.GetOrAdd(type, alias);
            if (!string.Equals(existingAlias, alias, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' is already registered as '{existingAlias}' and cannot also be " +
                    $"registered as '{alias}' - reading back would be ambiguous.");
            }
        }

        /// <summary>Meldet einen Typ unter einem stabilen Kurznamen an.</summary>
        public static void Register<T>(string alias) => Register(typeof(T), alias);

        /// <summary>
        /// Allows a type to be read under the restriction (<see cref="RestrictToRegisteredTypes"/>) WITHOUT giving it a
        /// short name: it keeps being written with its AssemblyQualifiedName, so the format on the wire does not change and
        /// peers of other versions still understand each other. Meant for the transport types of a protocol (e.g. the
        /// parameter wrappers of the inter-process communication).
        /// </summary>
        /// <remarks>
        /// Under the restriction the name is matched against the allowed and the registered types only - by type name and
        /// assembly name, ignoring version, culture and key. Nothing is loaded by name.
        /// </remarks>
        public static void Allow(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            allowed.TryAdd(type, 0);
        }

        /// <summary>Liefert den registrierten Kurznamen eines Typs, oder false.</summary>
        public static bool TryGetAlias(Type type, out string alias)
        {
            alias = null;
            return type != null && byType.TryGetValue(type, out alias);
        }

        /// <summary>Loest einen registrierten Kurznamen auf, oder liefert false.</summary>
        public static bool TryResolve(string alias, out Type type)
        {
            type = null;
            return !string.IsNullOrEmpty(alias) && byAlias.TryGetValue(alias, out type);
        }

        /// <summary>
        /// Die Typkennung, die fuer einen Laufzeittyp geschrieben wird: sein Kurzname, bei einer Sammlung
        /// registrierter Elemente der Elementname mit <c>[]</c>, sonst der
        /// <c>AssemblyQualifiedName</c> (unveraendertes Verhalten).
        /// </summary>
        public static string NameOf(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (byType.TryGetValue(type, out string alias))
            {
                return alias;
            }

            // Sammlungen bekommen ihren Namen vom Element - sonst muesste jede Auspraegung
            // (List<X>, X[], ...) einzeln angemeldet werden, und niemand denkt daran.
            Type element = ElementTypeOf(type);
            if (element != null && byType.TryGetValue(element, out string elementAlias))
            {
                return elementAlias + ArraySuffix;
            }

            return type.AssemblyQualifiedName;
        }

        /// <summary>
        /// Loest eine Typkennung auf: erst als Kurzname (auch <c>name[]</c> fuer eine Sammlung), dann -
        /// sofern nicht beschraenkt - als <c>AssemblyQualifiedName</c>.
        /// </summary>
        /// <param name="refused">
        /// true, wenn nur die Beschraenkung (<see cref="RestrictToRegisteredTypes"/>) die Aufloesung
        /// verhindert hat - fuer eine unterscheidbare Meldung
        /// </param>
        public static Type Resolve(string typeName, out bool refused)
        {
            refused = false;
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            if (byAlias.TryGetValue(typeName, out Type registered))
            {
                return registered;
            }

            if (typeName.EndsWith(ArraySuffix, StringComparison.Ordinal))
            {
                string elementName = typeName.Substring(0, typeName.Length - ArraySuffix.Length);
                if (byAlias.TryGetValue(elementName, out Type element))
                {
                    // Bewusst als Array: eine Sammlung kommt einheitlich als Array zurueck, egal ob sie
                    // als List<T> oder T[] abgelegt wurde. Beides ist aufzaehlbar, und die Alternative
                    // waere, die genaue Auspraegung mit ins Format zu schreiben.
                    return element.MakeArrayType();
                }
            }

            if (restricted.Value)
            {
                var known = FindKnown(typeName);
                if (known != null)
                {
                    return known;
                }

                refused = true;
                return null;
            }

            return Type.GetType(typeName);
        }

        /// <summary>
        /// Finds an allowed or registered type by its written name (AssemblyQualifiedName or FullName), without loading
        /// anything. An array of such a type is found as well.
        /// </summary>
        private static Type FindKnown(string typeName)
        {
            var key = Simplify(typeName);
            if (key == null)
            {
                return null;
            }

            foreach (var candidate in allowed.Keys.Concat(byType.Keys))
            {
                if (key == Simplify(candidate.AssemblyQualifiedName) || key == candidate.FullName)
                {
                    return candidate;
                }
            }

            var comma = key.IndexOf(',');
            var namePart = comma < 0 ? key : key.Substring(0, comma);
            if (namePart.EndsWith(ArraySuffix, StringComparison.Ordinal))
            {
                var element = FindKnown(namePart.Substring(0, namePart.Length - ArraySuffix.Length) + (comma < 0 ? string.Empty : key.Substring(comma)));
                return element?.MakeArrayType();
            }

            return null;
        }

        /// <summary>
        /// "Namespace.Type, Assembly, Version=..., Culture=..., PublicKeyToken=..." -> "Namespace.Type, Assembly". Names of
        /// generic types are not simplified (they nest further assembly names) and only match exactly.
        /// </summary>
        private static string Simplify(string assemblyQualifiedName)
        {
            if (string.IsNullOrWhiteSpace(assemblyQualifiedName))
            {
                return null;
            }

            if (assemblyQualifiedName.Contains('['))
            {
                return assemblyQualifiedName.Trim();
            }

            var parts = assemblyQualifiedName.Split(',');
            return parts.Length >= 2 ? $"{parts[0].Trim()}, {parts[1].Trim()}" : parts[0].Trim();
        }

        /// <summary>Der Elementtyp einer Sammlung (Array oder generische Ein-Parameter-Sammlung), sonst null.</summary>
        private static Type ElementTypeOf(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (type.IsGenericType)
            {
                Type[] args = type.GetGenericArguments();
                if (args.Length == 1 && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
                {
                    return args[0];
                }
            }

            return null;
        }

        /// <summary>
        /// Beschraenkt das Auflösen von Typen fuer den aktuellen Ausfuehrungsfluss auf <b>registrierte</b>
        /// Kurznamen - ein AssemblyQualifiedName im Datenstrom wird dann NICHT geladen. Beim
        /// <see cref="IDisposable.Dispose"/> gilt wieder der vorige Zustand (verschachtelbar).
        /// </summary>
        /// <remarks>
        /// Gedacht fuer alles, was aus einer Ablage gelesen wird, die nicht dieselbe Vertrauensstellung
        /// hat wie der eigene Prozess - Datenbank, Datei, Netz. Der Standard bleibt bewusst unbeschraenkt,
        /// damit die Prozesskommunikation unveraendert weiterlaeuft.
        /// </remarks>
        public static IDisposable RestrictToRegisteredTypes()
        {
            bool previous = restricted.Value;
            restricted.Value = true;
            return new Restore(previous);
        }

        private sealed class Restore : IDisposable
        {
            private readonly bool previous;
            private bool done;

            public Restore(bool previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (!done)
                {
                    restricted.Value = previous;
                    done = true;
                }
            }
        }
    }
}
