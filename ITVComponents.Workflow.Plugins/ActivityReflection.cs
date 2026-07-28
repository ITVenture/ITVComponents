using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.Workflow.Activities;

namespace ITVComponents.Workflow.Plugins
{
    /// <summary>
    /// Gemeinsame, <b>instanzfreie</b> Reflection ueber die Aktivitaets-Attribute
    /// (<see cref="WorkflowActivityAttribute"/> / <see cref="ActivityParameterAttribute"/>). Von den
    /// Katalog-Implementierungen geteilt: der In-Process-Katalog (<see cref="PluginActivityCatalog"/>, ueber
    /// <c>IDynamicLoader</c>) und der WebPlugin-Katalog beschreiben Typen und Parameter identisch - nur WOHER
    /// die Typen kommen, unterscheidet sich. So bleibt die Attribut-Auswertung an genau einer Stelle.
    /// </summary>
    public static class ActivityReflection
    {
        /// <summary>Die Kurzbeschreibung eines Aktivitaets-Typs aus seinem <see cref="WorkflowActivityAttribute"/>.</summary>
        public static ActivityTypeInfo ToActivityTypeInfo(string activityRef, Type type)
        {
            WorkflowActivityAttribute meta = type?.GetCustomAttribute<WorkflowActivityAttribute>(true);
            return new ActivityTypeInfo
            {
                ActivityRef = activityRef,
                DisplayName = string.IsNullOrWhiteSpace(meta?.DisplayName) ? activityRef : meta.DisplayName,
                Description = meta?.Description
            };
        }

        /// <summary>Die deklarierten Parameter eines Aktivitaets-Typs (sortiert nach Order/Name), oder leer.</summary>
        public static ActivityParameter[] GetParameters(Type type)
        {
            if (type == null)
            {
                return Array.Empty<ActivityParameter>();
            }

            return type.GetCustomAttributes<ActivityParameterAttribute>(true)
                .Select(ToParameter)
                .OrderBy(p => p.Order)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>Das Parameter-Attribut mit dem angegebenen Namen am Typ, oder null.</summary>
        public static ActivityParameterAttribute FindParameter(Type type, string parameterName)
            => type?.GetCustomAttributes<ActivityParameterAttribute>(true)
                .FirstOrDefault(a => a.Name == parameterName);

        /// <summary>Bildet ein Parameter-Attribut auf das serialisierbare <see cref="ActivityParameter"/>-Modell ab.</summary>
        public static ActivityParameter ToParameter(ActivityParameterAttribute a)
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
                Values = a.Kind == ActivityParameterKind.Picklist
                    ? StaticValues(a)
                    : Array.Empty<ActivityParameterValue>()
            };
        }

        /// <summary>Die statisch deklarierten Auswahlwerte eines Parameters (Value + Label), oder leer.</summary>
        public static ActivityParameterValue[] StaticValues(ActivityParameterAttribute a)
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

            return result.ToArray();
        }
    }
}
