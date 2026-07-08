using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Json;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Helpers
{
    /// <summary>
    /// The template resolved for onboarding: the parsed <see cref="TenantTemplateMarkup"/> to apply and, when the
    /// template was picked through a <c>TenantType</c> (the <see cref="TenantSetupOptions.BasicTenantType"/> path), that
    /// type's id so the onboarded tenant can be tagged with it.
    /// </summary>
    public sealed record ResolvedOnboardingTemplate(TenantTemplateMarkup Markup, int? TenantTypeId);

    /// <summary>
    /// Shared resolution of the tenant template to apply during onboarding (used by both the Blazor and the MVC
    /// onboarding flows), in this precedence:
    /// <list type="number">
    /// <item>an explicit <c>templateNameOverride</c> (e.g. carried by an invitation), by template name;</item>
    /// <item><see cref="TenantSetupOptions.BasicTenantType"/> → the <c>TenantType</c>'s attached <c>TenantTemplate</c>;</item>
    /// <item><see cref="TenantSetupOptions.BasicTenantTemplate"/> (legacy), by template name.</item>
    /// </list>
    /// </summary>
    public static class OnboardingTemplateResolver
    {
        /// <summary>
        /// Resolves the template markup + originating tenant-type id, or <c>null</c> (with a logged warning) when
        /// nothing is configured, the type/template is not found, or the resolved type carries no template.
        /// </summary>
        public static async Task<ResolvedOnboardingTemplate> ResolveAsync(
            IQueryable<TenantType> tenantTypes,
            IQueryable<TenantTemplate> tenantTemplates,
            TenantSetupOptions cfg,
            string templateNameOverride,
            ILogger logger,
            CancellationToken ct)
        {
            TenantTemplate tmpl = null;
            int? tenantTypeId = null;

            if (!string.IsNullOrEmpty(templateNameOverride))
            {
                tmpl = await tenantTemplates.FirstOrDefaultAsync(n => n.Name == templateNameOverride, ct);
                if (tmpl == null)
                {
                    logger.LogWarning("Tenant template {Template} not found; skipping.", templateNameOverride);
                }
            }
            else if (!string.IsNullOrEmpty(cfg?.BasicTenantType))
            {
                var tt = await tenantTypes.Include(t => t.TenantTemplate)
                    .FirstOrDefaultAsync(t => t.TenantTypeName == cfg.BasicTenantType, ct);
                if (tt == null)
                {
                    logger.LogWarning("Tenant type {Type} not found; skipping template.", cfg.BasicTenantType);
                }
                else if (tt.TenantTemplate == null)
                {
                    logger.LogWarning("Tenant type {Type} has no template attached; skipping.", cfg.BasicTenantType);
                }
                else
                {
                    tmpl = tt.TenantTemplate;
                    tenantTypeId = tt.TenantTypeId;
                }
            }
            else if (!string.IsNullOrEmpty(cfg?.BasicTenantTemplate))
            {
                tmpl = await tenantTemplates.FirstOrDefaultAsync(n => n.Name == cfg.BasicTenantTemplate, ct);
                if (tmpl == null)
                {
                    logger.LogWarning("Tenant template {Template} not found; skipping.", cfg.BasicTenantTemplate);
                }
            }

            if (tmpl == null)
            {
                return null;
            }

            var markup = JsonHelper.FromJsonString<TenantTemplateMarkup>(tmpl.Markup, SerializationTypingMode.NativePolymorphism);
            return new ResolvedOnboardingTemplate(markup, tenantTypeId);
        }
    }
}
