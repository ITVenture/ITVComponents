using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.AspExtensions.Attributes;

namespace ITVComponents.WebCoreToolkit.AspExtensions
{
    /// <summary>
    /// Findet das ViewModel, das sich per <see cref="ViewModelForEntityAttribute"/> zu einem Entitaetstyp bekennt.
    /// </summary>
    /// <remarks>
    /// Gesucht wird in den geladenen Assemblies, die die Assembly der Vorgabe referenzieren - ein Erweiterungspaket (etwa die
    /// Baum-Ansichten) ist geladen, sobald sein WebPart aktiv ist. Das Ergebnis wird je (Vorgabe, Entitaet) gepuffert.
    /// </remarks>
    internal static class EntityBoundTypeArguments
    {
        private static readonly ConcurrentDictionary<(Type DefaultType, Type EntityType), Type> Resolved = new();

        /// <summary>Das passende ViewModel, oder null, wenn sich keines (eindeutig) bekennt - dann gilt die Vorgabe.</summary>
        public static Type Find(Type defaultType, Type entityType)
            => Resolved.GetOrAdd((defaultType, entityType), key => Search(key.DefaultType, key.EntityType));

        private static Type Search(Type defaultType, Type entityType)
        {
            var candidates = new List<(Type Type, int Distance)>();
            var anchor = defaultType.Assembly.GetName();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || (assembly != defaultType.Assembly
                                           && !assembly.GetReferencedAssemblies().Any(r => AssemblyName.ReferenceMatchesDefinition(r, anchor))))
                {
                    continue;
                }

                foreach (var type in TypesOf(assembly))
                {
                    if (type.IsAbstract || type.IsGenericTypeDefinition || type == defaultType || !defaultType.IsAssignableFrom(type))
                    {
                        continue;
                    }

                    foreach (var attribute in type.GetCustomAttributes<ViewModelForEntityAttribute>(false))
                    {
                        var distance = Distance(entityType, attribute.EntityType);
                        if (distance >= 0)
                        {
                            candidates.Add((type, distance));
                        }
                    }
                }
            }

            if (candidates.Count == 0)
            {
                LogEnvironment.LogDebugEvent($"No view model for entity {entityType.FullName} derives from {defaultType.FullName}; the default is used.", LogSeverity.Report);
                return null;
            }

            var nearest = candidates.Min(c => c.Distance);
            var best = candidates.Where(c => c.Distance == nearest).Select(c => c.Type).Distinct().ToArray();
            if (best.Length != 1)
            {
                LogEnvironment.LogEvent($"Several view models claim entity {entityType.FullName} ({string.Join(", ", best.Select(b => b.FullName))}); {defaultType.FullName} is used instead. Remove one of the [ViewModelForEntity] declarations or configure ViewConfig:CustomViewGenericArgs.", LogSeverity.Error);
                return null;
            }

            LogEnvironment.LogDebugEvent($"View model {best[0].FullName} is used for entity {entityType.FullName}.", LogSeverity.Report);
            return best[0];
        }

        /// <summary>Wie weit die Entitaet vom erklaerten Typ abgeleitet ist (0 = genau er), -1 = gar nicht.</summary>
        private static int Distance(Type entityType, Type declared)
        {
            var distance = 0;
            for (var t = entityType; t != null; t = t.BaseType, distance++)
            {
                if (t == declared || (declared.IsGenericTypeDefinition && t.IsGenericType && t.GetGenericTypeDefinition() == declared))
                {
                    return distance;
                }
            }

            return -1;
        }

        private static IEnumerable<Type> TypesOf(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                LogEnvironment.LogEvent($"Some types of {assembly.FullName} could not be loaded while looking for entity-bound view models; the others are searched: {ex.OutlineException()}", LogSeverity.Warning);
                return ex.Types.Where(t => t != null);
            }
        }
    }
}
