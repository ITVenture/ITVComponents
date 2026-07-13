using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.TemplateHandling;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.TemplateHandling
{
    /// <summary>
    /// Hierarchy-strategy counterpart of the employee-role-mapping template part handler. Same behaviour,
    /// bound to <c>IHierarchySecurityContextWithOnboarding</c> and the Hierarchy mapping entity.
    /// </summary>
    public class HierarchyEmployeeRoleMappingTemplatePartHandler : ITenantTemplatePartHandler
    {
        public string PartKey => EmployeeRoleMappingTemplateEntry.PartKey;

        public TemplateExtensionPayload Extract(DbContext db, int tenantId)
        {
            if (db is not IHierarchySecurityContextWithOnboarding ctx)
            {
                return null;
            }

            var entries = ctx.EmployeeRoleMappings.IgnoreQueryFilters().AsNoTracking()
                .Where(m => m.TenantId == tenantId)
                .Select(m => new EmployeeRoleMappingTemplateEntry
                {
                    RoleName = m.Role.RoleName,
                    Kind = m.Kind,
                    DisplayNameJson = m.DisplayNameJson,
                    VisibilityFeature = m.VisibilityFeature
                })
                .ToList();

            return entries.Count == 0 ? null : new EmployeeRoleMappingTemplatePayload { Mappings = entries };
        }

        public void Apply(DbContext db, int tenantId, TemplateExtensionPayload payload, TemplateApplyMode mode)
        {
            if (db is not IHierarchySecurityContextWithOnboarding ctx)
            {
                return;
            }

            if (payload is not EmployeeRoleMappingTemplatePayload p || p.Mappings == null || p.Mappings.Count == 0)
            {
                return;
            }

            var entries = p.Mappings;

            var changed = false;
            var appliedRoleIds = new List<int>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.RoleName))
                {
                    continue;
                }

                var role = ctx.SecurityRoles.IgnoreQueryFilters()
                    .FirstOrDefault(r => r.TenantId == tenantId && r.RoleName == entry.RoleName);
                if (role == null)
                {
                    continue;
                }

                appliedRoleIds.Add(role.RoleId);
                var existing = ctx.EmployeeRoleMappings.IgnoreQueryFilters()
                    .FirstOrDefault(m => m.TenantId == tenantId && m.RoleId == role.RoleId);
                if (existing == null)
                {
                    ctx.EmployeeRoleMappings.Add(new HierarchyEmployeeRoleMapping
                    {
                        TenantId = tenantId,
                        RoleId = role.RoleId,
                        Kind = entry.Kind,
                        DisplayNameJson = entry.DisplayNameJson,
                        VisibilityFeature = entry.VisibilityFeature
                    });
                }
                else
                {
                    existing.Kind = entry.Kind;
                    existing.DisplayNameJson = entry.DisplayNameJson;
                    existing.VisibilityFeature = entry.VisibilityFeature;
                }

                changed = true;
            }

            // Forced also prunes tenant mappings not present in the payload; Additive leaves them in place.
            if (mode == TemplateApplyMode.Forced)
            {
                var stale = ctx.EmployeeRoleMappings.IgnoreQueryFilters()
                    .Where(m => m.TenantId == tenantId && !appliedRoleIds.Contains(m.RoleId))
                    .ToList();
                if (stale.Count > 0)
                {
                    ctx.EmployeeRoleMappings.RemoveRange(stale);
                    changed = true;
                }
            }

            if (changed)
            {
                db.SaveChanges();
            }
        }
    }
}
