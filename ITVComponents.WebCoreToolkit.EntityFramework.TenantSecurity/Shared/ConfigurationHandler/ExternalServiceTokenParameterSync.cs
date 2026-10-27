using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ITVComponents.Logging;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ConfigurationHandler
{
    /// <summary>
    /// Brings the token parameters of an external service on the target system in line with an imported configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parameters have no DbSet of their own (see <see cref="ExternalOAuthServiceParameter"/>), so the data-apply can
    /// not address them as an entity. They travel as one detail of the service, and this helper - offered to the apply as
    /// the variable <see cref="VariableName"/> - updates the collection of the service.
    /// </para>
    /// <para>
    /// <b>A secret value on the target is never overwritten</b>: the configuration does not carry it. A parameter that is
    /// new on the target is created empty and has to be set there. A plain parameter that the source has marked secret is
    /// encrypted on the target as it is - with the system key, because only global services are exported.
    /// </para>
    /// </remarks>
    public sealed class ExternalServiceTokenParameterSync
    {
        /// <summary>The name under which the data-apply offers this helper to the value expressions.</summary>
        public const string VariableName = "TokenParameterSync";

        /// <summary>The value expression of the detail that carries the parameters.</summary>
        public const string ValueExpression = VariableName + ".Apply(Entity.TokenParameters, NewValueRaw)";

        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

        /// <summary>The canonical text of a parameter list - for the comparison and as the value of the detail.</summary>
        public static string Describe(IEnumerable<ExternalOAuthServiceParameterTemplateMarkup> parameters)
            => JsonSerializer.Serialize(
                (parameters ?? Array.Empty<ExternalOAuthServiceParameterTemplateMarkup>())
                .OrderBy(n => n.ParameterName, StringComparer.OrdinalIgnoreCase)
                .Select(n => new ExternalOAuthServiceParameterTemplateMarkup
                {
                    ParameterName = n.ParameterName,
                    IsSecret = n.IsSecret,
                    ParameterValue = n.IsSecret ? null : n.ParameterValue
                }).ToArray(), Json);

        /// <summary>Applies the imported parameters to the collection of a service.</summary>
        /// <param name="target">the TokenParameters of the service on the target system</param>
        /// <param name="json">the parameters as produced by <see cref="Describe"/></param>
        public void Apply(IList<ExternalOAuthServiceParameter> target, string json)
        {
            var imported = string.IsNullOrWhiteSpace(json)
                ? Array.Empty<ExternalOAuthServiceParameterTemplateMarkup>()
                : JsonSerializer.Deserialize<ExternalOAuthServiceParameterTemplateMarkup[]>(json)
                  ?? Array.Empty<ExternalOAuthServiceParameterTemplateMarkup>();

            foreach (var gone in target.Where(t => imported.All(i => !string.Equals(i.ParameterName, t.ParameterName, StringComparison.OrdinalIgnoreCase))).ToArray())
            {
                // required relationship: removing the parameter from the collection deletes the orphaned row
                target.Remove(gone);
            }

            foreach (var parameter in imported)
            {
                var existing = target.FirstOrDefault(t => string.Equals(t.ParameterName, parameter.ParameterName, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    target.Add(new ExternalOAuthServiceParameter
                    {
                        ParameterName = parameter.ParameterName,
                        ParameterValue = parameter.IsSecret ? null : parameter.ParameterValue,
                        IsSecret = parameter.IsSecret
                    });
                    if (parameter.IsSecret)
                    {
                        LogEnvironment.LogEvent($"The secret token parameter '{parameter.ParameterName}' was created without a value by the configuration import; set it on this system.", LogSeverity.Warning);
                    }

                    continue;
                }

                existing.ParameterName = parameter.ParameterName;
                if (parameter.IsSecret)
                {
                    if (!existing.IsSecret && !string.IsNullOrEmpty(existing.ParameterValue))
                    {
                        existing.ParameterValue = existing.ParameterValue.Encrypt();
                    }

                    existing.IsSecret = true;
                }
                else
                {
                    existing.ParameterValue = parameter.ParameterValue;
                    existing.IsSecret = false;
                }
            }
        }
    }
}
