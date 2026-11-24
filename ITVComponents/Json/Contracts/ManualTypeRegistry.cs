using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using ITVComponents.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.Json.Contracts
{
    /// <summary>
    /// Vergibt <b>stabile Kurznamen</b> fuer Typen, die polymorph serialisiert werden
    /// (<see cref="SerializationTypingMode.EnvelopePolymorphism"/>)
    /// - und entscheidet, ob beim Lesen ueberhaupt etwas anderes als ein registrierter Typ aufgeloest werden darf.
    /// </summary>
    /// <remarks>
    /// Ohne Registrierung wird der Typname <b>ohne Version</b> geschrieben (<c>Namespace.Typ, Assembly</c>, auch fuer
    /// generische Argumente). Gelesen wird versionstolerant: die Assembly wird ueber ihren einfachen Namen gesucht,
    /// zuerst unter den bereits geladenen (in allen Load-Contexts), Version, Culture und Key spielen keine Rolle.
    /// <para>
    /// Fuer <b>langlebig abgelegte</b> Daten bleibt ein Kurzname die bessere Wahl: der Typname haengt an Klasse,
    /// Namespace und Assembly, also genau an dem, was ein Refactoring aendert. Und er erlaubt beim Lesen das Laden
    /// eines <i>beliebigen</i> Typs - was fuer eine Datenbank-Spalte eine ganz andere Angriffsflaeche ist als fuer einen
    /// prozessinternen Kanal. Ein registrierter Kurzname loest beides: er ueberlebt den Umzug der Klasse, und er ist
    /// eine Positivliste.
    /// </para>
    /// <para>
    /// Generische Typen bekommen einen Kurznamen, wenn ihre Definition und alle Argumente einen haben:
    /// <c>list&lt;sign-item&gt;</c>, <c>dict&lt;string,sign-item&gt;</c>. <c>List&lt;&gt;</c>, <c>HashSet&lt;&gt;</c> und
    /// <c>Dictionary&lt;,&gt;</c> sind ab Werk angemeldet. Arrays heissen <c>name[]</c>.
    /// </para></remarks>
    public static class ManualTypeRegistry
    {
        private static readonly ConcurrentDictionary<string, Type> byAlias =
            new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<Type, string> byType =
            new ConcurrentDictionary<Type, string>();

        private static readonly AsyncLocal<bool> restricted = new AsyncLocal<bool>();

        /// <summary>
        /// Types that may be read under the restriction without a short name - written with their type name (see
        /// <see cref="Allow"/>).
        /// </summary>
        private static readonly ConcurrentDictionary<Type, byte> allowed = new ConcurrentDictionary<Type, byte>();

        /// <summary>
        /// Successfully resolved type names, separately for the open and the restricted lookup (the restricted one only
        /// ever holds known types). Types from collectible load contexts are not cached so they can be unloaded.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Type> openCache =
            new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<string, Type> restrictedCache =
            new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);

        /// <summary>Das Anhaengsel, mit dem ein Array ueber seinem Elementtyp benannt wird.</summary>
        private const string ArraySuffix = "[]";

        static ManualTypeRegistry()
        {
            // Die Grundtypen sind ab Werk angemeldet. Ohne sie waere die Beschraenkung auf registrierte
            // Typen unbenutzbar - eine schlichte Zeichenketten-Variable liesse sich nicht mehr lesen. Die
            // Namen sind bewusst kurz: sie stehen in jedem abgelegten Datensatz.
            Register(typeof(object), "object");
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

            // Die gaengigen Sammlungen als generische Definitionen: eine Liste angemeldeter Elemente bekommt so einen
            // Kurznamen (list<x>) und kommt als genau diese Sammlung zurueck, nicht als Array.
            Register(typeof(List<>), "list");
            Register(typeof(HashSet<>), "set");
            Register(typeof(Dictionary<,>), "dict");
        }

        /// <summary>
        /// Gilt gerade die Beschraenkung auf registrierte Typen? Siehe
        /// <see cref="RestrictToRegisteredTypes"/>.
        /// </summary>
        public static bool Restricted => restricted.Value;

        /// <summary>
        /// Meldet einen Typ unter einem stabilen Kurznamen an. Ab dann wird dieser Name geschrieben (statt
        /// des Typnamens) und beim Lesen wieder auf den Typ abgebildet.
        /// </summary>
        /// <remarks>
        /// Der Name gehoert zum <b>Datenformat</b> - er darf sich nicht mehr aendern, sobald etwas damit
        /// abgelegt wurde. Der Typ dahinter darf umziehen, umbenannt werden, die Assembly wechseln.
        /// Mehrfach-Registrierung desselben Paares ist folgenlos; ein zweiter Typ unter demselben Namen
        /// (oder ein zweiter Name fuer denselben Typ) ist ein Programmierfehler und wirft.
        /// <para>
        /// Eine generische <b>Definition</b> (z.B. <c>typeof(Queue&lt;&gt;)</c>) darf ebenfalls angemeldet werden; ihre
        /// Auspraegungen heissen dann <c>name&lt;arg,…&gt;</c>, sofern alle Argumente einen Kurznamen haben. Die Zeichen
        /// <c>&lt;</c>, <c>&gt;</c>, <c>,</c>, <c>[</c> und <c>]</c> sind deshalb in einem Kurznamen nicht erlaubt.
        /// </para>
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

            if (alias.IndexOfAny(new[] { '<', '>', ',', '[', ']' }) >= 0)
            {
                throw new ArgumentException(
                    $"The type alias '{alias}' contains one of the reserved characters < > , [ ] - they build the names of generic types and arrays.",
                    nameof(alias));
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
        /// short name: it keeps being written with its type name. Meant for the transport types of a protocol (e.g. the
        /// parameter wrappers of the inter-process communication).
        /// </summary>
        /// <remarks>
        /// Under the restriction every part of a name (the type, each generic argument, an array's element) must be an
        /// allowed or registered type, matched by type name and assembly name, ignoring version, culture and key. Nothing
        /// is loaded by name.
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
        /// Die Typkennung, die fuer einen Laufzeittyp geschrieben wird: sein Kurzname (auch zusammengesetzt, z.B.
        /// <c>list&lt;x&gt;</c> oder <c>x[]</c>), sonst der Typname ohne Version (<c>Namespace.Typ, Assembly</c>).
        /// </summary>
        public static string NameOf(Type type)
        {
            if (type == null)
            {
                return null;
            }

            return TryShortName(type, out string shortName) ? shortName : QualifiedName(type);
        }

        /// <summary>
        /// Loest eine Typkennung auf: erst als Kurzname (auch <c>name[]</c> und <c>name&lt;…&gt;</c>), dann als Typname.
        /// Der Typname wird versionstolerant aufgeloest; unter der Beschraenkung nur, wenn jeder Teil erlaubt oder
        /// registriert ist.
        /// </summary>
        /// <param name="typeName">die geschriebene Typkennung</param>
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

            Type composed = ResolveShortName(typeName);
            if (composed != null)
            {
                return composed;
            }

            bool isRestricted = restricted.Value;
            var cache = isRestricted ? restrictedCache : openCache;
            if (cache.TryGetValue(typeName, out Type cached))
            {
                return cached;
            }

            Type retVal = LoadByName(typeName, isRestricted);
            if (retVal == null)
            {
                refused = isRestricted;
                return null;
            }

            if (!IsCollectible(retVal))
            {
                cache.TryAdd(typeName, retVal);
            }

            return retVal;
        }

        /// <summary>
        /// Beschraenkt das Auflösen von Typen fuer den aktuellen Ausfuehrungsfluss auf <b>registrierte</b>
        /// (oder erlaubte) Typen - ein beliebiger Typname im Datenstrom wird dann NICHT geladen. Beim
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

        /// <summary>The composed short name of a type, if the type and all its parts have one.</summary>
        private static bool TryShortName(Type type, out string name)
        {
            if (byType.TryGetValue(type, out name))
            {
                return true;
            }

            if (IsSingleDimensionArray(type))
            {
                if (TryShortName(type.GetElementType(), out string element))
                {
                    name = element + ArraySuffix;
                    return true;
                }

                return false;
            }

            if (type.IsConstructedGenericType && byType.TryGetValue(type.GetGenericTypeDefinition(), out string definition))
            {
                var args = new List<string>();
                foreach (Type arg in type.GetGenericArguments())
                {
                    if (!TryShortName(arg, out string argName))
                    {
                        return false;
                    }

                    args.Add(argName);
                }

                name = $"{definition}<{string.Join(",", args)}>";
                return true;
            }

            return false;
        }

        /// <summary>"Namespace.Type, Assembly" without version, culture and key - recursively for generic arguments.</summary>
        private static string QualifiedName(Type type)
        {
            return $"{TypeNamePart(type)}, {AssemblyOf(type).GetName().Name}";
        }

        private static string TypeNamePart(Type type)
        {
            if (type.IsArray)
            {
                string rank = IsSingleDimensionArray(type) ? ArraySuffix : $"[{new string(',', type.GetArrayRank() - 1)}]";
                return TypeNamePart(type.GetElementType()) + rank;
            }

            if (type.IsConstructedGenericType)
            {
                var args = type.GetGenericArguments().Select(a => $"[{QualifiedName(a)}]");
                return $"{type.GetGenericTypeDefinition().FullName}[{string.Join(",", args)}]";
            }

            return type.FullName ?? type.Name;
        }

        private static Assembly AssemblyOf(Type type)
        {
            return type.IsArray ? AssemblyOf(type.GetElementType()) : type.Assembly;
        }

        private static bool IsSingleDimensionArray(Type type)
        {
            return type.IsArray && type == type.GetElementType().MakeArrayType();
        }

        /// <summary>Resolves "alias[]" and "alias&lt;arg,…&gt;" from registered short names only, otherwise null.</summary>
        private static Type ResolveShortName(string name)
        {
            if (byAlias.TryGetValue(name, out Type registered))
            {
                return registered;
            }

            if (name.EndsWith(ArraySuffix, StringComparison.Ordinal))
            {
                return ResolveShortName(name.Substring(0, name.Length - ArraySuffix.Length))?.MakeArrayType();
            }

            int open = name.IndexOf('<');
            if (open <= 0 || !name.EndsWith(">", StringComparison.Ordinal))
            {
                return null;
            }

            if (!byAlias.TryGetValue(name.Substring(0, open), out Type definition) || !definition.IsGenericTypeDefinition)
            {
                return null;
            }

            var argNames = SplitTopLevel(name.Substring(open + 1, name.Length - open - 2));
            if (argNames.Count != definition.GetGenericArguments().Length)
            {
                LogEnvironment.LogEvent(
                    $"Type name '{name}' gives {argNames.Count} type argument(s), but '{definition.FullName}' takes {definition.GetGenericArguments().Length}.",
                    LogSeverity.Error);
                return null;
            }

            var args = new Type[argNames.Count];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = ResolveShortName(argNames[i]);
                if (args[i] == null)
                {
                    return null;
                }
            }

            try
            {
                return definition.MakeGenericType(args);
            }
            catch (ArgumentException ex)
            {
                LogEnvironment.LogEvent(
                    $"Type name '{name}' does not satisfy the constraints of '{definition.FullName}': {ex.OutlineException()}",
                    LogSeverity.Error);
                return null;
            }
        }

        /// <summary>Splits <c>a,b&lt;c,d&gt;,e</c> at the commas of the outermost level.</summary>
        private static List<string> SplitTopLevel(string list)
        {
            var retVal = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < list.Length; i++)
            {
                switch (list[i])
                {
                    case '<':
                        depth++;
                        break;
                    case '>':
                        depth--;
                        break;
                    case ',' when depth == 0:
                        retVal.Add(list.Substring(start, i - start).Trim());
                        start = i + 1;
                        break;
                }
            }

            retVal.Add(list.Substring(start).Trim());
            return retVal;
        }

        /// <summary>
        /// Loads a type by its type name, version-tolerant. Under the restriction, only already loaded assemblies are
        /// considered and every part of the name must be an allowed or registered type.
        /// </summary>
        private static Type LoadByName(string typeName, bool isRestricted)
        {
            try
            {
                return Type.GetType(
                    typeName,
                    assemblyName => FindAssembly(assemblyName, isRestricted),
                    (assembly, name, ignoreCase) => FindTypeComponent(assembly, name, ignoreCase, isRestricted),
                    throwOnError: false);
            }
            catch (Exception ex)
            {
                // Type.GetType still throws for some malformed names and for assemblies that exist but fail to load.
                LogEnvironment.LogEvent($"Type name '{typeName}' could not be loaded: {ex.OutlineException()}", LogSeverity.Error);
                return null;
            }
        }

        private static Assembly FindAssembly(AssemblyName assemblyName, bool isRestricted)
        {
            Assembly loaded = (from context in AssemblyLoadContext.All
                               from assembly in context.Assemblies
                               where !assembly.IsDynamic &&
                                     string.Equals(assembly.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase)
                               orderby context == AssemblyLoadContext.Default descending, assembly.GetName().Version descending
                               select assembly).FirstOrDefault();
            if (loaded != null || isRestricted)
            {
                return loaded;
            }

            try
            {
                // By simple name only: the default context then takes whatever version is there.
                return AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName.Name));
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Assembly '{assemblyName.Name}' could not be loaded: {ex.OutlineException()}", LogSeverity.Warning);
                return null;
            }
        }

        private static Type FindTypeComponent(Assembly assembly, string name, bool ignoreCase, bool isRestricted)
        {
            if (!isRestricted)
            {
                return assembly != null
                    ? assembly.GetType(name, false, ignoreCase)
                    : Type.GetType(name, false, ignoreCase);
            }

            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return allowed.Keys.Concat(byType.Keys).FirstOrDefault(t =>
                string.Equals(t.FullName, name, comparison) &&
                (assembly == null || t.Assembly == assembly ||
                 string.Equals(t.Assembly.GetName().Name, assembly.GetName().Name, StringComparison.OrdinalIgnoreCase)));
        }

        private static bool IsCollectible(Type type)
        {
            if (type.IsArray)
            {
                return IsCollectible(type.GetElementType());
            }

            return type.Assembly.IsCollectible ||
                   (type.IsConstructedGenericType && type.GetGenericArguments().Any(IsCollectible));
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
