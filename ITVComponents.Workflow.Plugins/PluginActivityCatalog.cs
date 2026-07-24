using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Workflow.Activities;

namespace ITVComponents.Workflow.Plugins
{
    /// <summary>
    /// In-Process-Implementierung des <see cref="IWorkflowActivityCatalog"/>: ermittelt die verfuegbaren
    /// Aktivitaets-Typen aus den Scoped-Plugin-Definitionen der <see cref="IDynamicLoader"/> der Factory
    /// und liest ihre Parameter per Reflection aus den Klassen-Attributen - OHNE die Aktivitaeten zu
    /// instanzieren.
    /// </summary>
    public sealed class PluginActivityCatalog : IWorkflowActivityCatalog
    {
        private readonly PluginFactory factory;

        /// <summary>Initialisiert den Katalog mit der Factory, deren Loader die Aktivitaeten kennen.</summary>
        public PluginActivityCatalog(PluginFactory factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <inheritdoc/>
        public IReadOnlyList<ActivityTypeInfo> GetActivityTypes()
        {
            var result = new List<ActivityTypeInfo>();
            foreach ((PluginConfigurationItem item, Type type) in Activities())
            {
                WorkflowActivityAttribute meta = type.GetCustomAttribute<WorkflowActivityAttribute>(true);
                result.Add(new ActivityTypeInfo
                {
                    ActivityRef = item.Name,
                    DisplayName = string.IsNullOrWhiteSpace(meta?.DisplayName) ? item.Name : meta.DisplayName,
                    Description = meta?.Description
                });
            }

            return result;
        }

        /// <inheritdoc/>
        public IReadOnlyList<ActivityParameter> GetParameters(string activityRef)
        {
            Type type = TypeFor(activityRef);
            if (type == null)
            {
                return new List<ActivityParameter>();
            }

            return type.GetCustomAttributes<ActivityParameterAttribute>(true)
                .Select(ToParameter)
                .OrderBy(p => p.Order)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <inheritdoc/>
        public IReadOnlyList<ActivityParameterValue> GetValidValues(string activityRef, string parameterName)
        {
            Type type = TypeFor(activityRef);
            ActivityParameterAttribute attr = type?.GetCustomAttributes<ActivityParameterAttribute>(true)
                .FirstOrDefault(a => a.Name == parameterName);
            if (attr == null)
            {
                return new List<ActivityParameterValue>();
            }

            if (attr.Kind != ActivityParameterKind.CallbackList)
            {
                // Statische Picklist (oder nichts).
                return StaticValues(attr);
            }

            if (attr.ValuesProvider == null || !typeof(IValuesProvider).IsAssignableFrom(attr.ValuesProvider))
            {
                LogEnvironment.LogEvent(
                    $"Parameter '{parameterName}' of activity '{activityRef}' is a CallbackList but declares no " +
                    "valid IValuesProvider type.", LogSeverity.Error);
                return new List<ActivityParameterValue>();
            }

            IPluginFactory scope = null;
            try
            {
                // Eigener Scope: der Provider wird darin konstruiert (volle Konstruktor-Injection ueber die
                // Factory) und beim Schliessen wieder freigegeben.
                scope = factory.NewScope(new Dictionary<string, object>(), null, false);
                string constructor = $"[{attr.ValuesProvider.Assembly.Location}]<{attr.ValuesProvider.FullName}>";
                IValuesProvider provider = scope.LoadPlugin<IValuesProvider>(
                    $"valuesprovider:{activityRef}:{parameterName}", constructor);
                return provider.GetValues(parameterName)?.ToList() ?? new List<ActivityParameterValue>();
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Value provider '{attr.ValuesProvider?.FullName}' for parameter '{parameterName}' of " +
                    $"activity '{activityRef}' failed: {ex.OutlineException()}", LogSeverity.Error);
                return new List<ActivityParameterValue>();
            }
            finally
            {
                scope?.Dispose();
            }
        }

        private Type TypeFor(string activityRef)
        {
            return Activities()
                .Where(a => a.Item.Name == activityRef)
                .Select(a => a.Type)
                .FirstOrDefault();
        }

        /// <summary>
        /// Alle Scoped-Plugins der Loader, die sich als <see cref="IActivityPlugin"/> aufloesen lassen -
        /// samt ihres CLR-Typs (reflection-only, ohne Instanz).
        /// </summary>
        private IEnumerable<(PluginConfigurationItem Item, Type Type)> Activities()
        {
            foreach (IDynamicLoader loader in factory.GetPlugins<IDynamicLoader>())
            {
                IEnumerable<PluginConfigurationItem> names;
                try
                {
                    names = loader.GetScopedPluginNames() ?? Enumerable.Empty<PluginConfigurationItem>();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent(
                        $"Could not read scoped plugin names from a dynamic loader: {ex.OutlineException()}",
                        LogSeverity.Warning);
                    continue;
                }

                foreach (PluginConfigurationItem item in names)
                {
                    if (item?.ConstructionString != null
                        && factory.TryGetPluginType(item.ConstructionString, out Type type)
                        && typeof(IActivityPlugin).IsAssignableFrom(type))
                    {
                        yield return (item, type);
                    }
                }
            }
        }

        private static ActivityParameter ToParameter(ActivityParameterAttribute a)
        {
            return new ActivityParameter
            {
                Name = a.Name,
                Kind = a.Kind,
                Direction = a.Direction,
                Required = a.Required,
                Default = a.Default,
                Description = a.Description,
                Group = a.Group,
                Order = a.Order,
                // CallbackList-Werte kommen lazy ueber GetValidValues; statische nur bei Picklist inline.
                Values = a.Kind == ActivityParameterKind.Picklist ? StaticValues(a) : new List<ActivityParameterValue>()
            };
        }

        private static List<ActivityParameterValue> StaticValues(ActivityParameterAttribute a)
        {
            var result = new List<ActivityParameterValue>();
            if (a.Values != null)
            {
                for (int i = 0; i < a.Values.Length; i++)
                {
                    result.Add(new ActivityParameterValue
                    {
                        Value = a.Values[i],
                        Label = a.Labels != null && i < a.Labels.Length ? a.Labels[i] : a.Values[i]
                    });
                }
            }

            return result;
        }
    }
}
