using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Models;
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

        public string Extract(DbContext db, int tenantId)
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
                    DisplayNameJson = m.DisplayNameJson
                })
                .ToList();

            return entries.Count == 0 ? null : JsonSerializer.Serialize(entries);
        }

        public void Apply(DbContext db, int tenantId, string payload)
        {
            if (db is not IHierarchySecurityContextWithOnboarding ctx)
            {
                return;
            }

            var entries = JsonSerializer.Deserialize<List<EmployeeRoleMappingTemplateEntry>>(payload);
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            var changed = false;
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

                var existing = ctx.EmployeeRoleMappings.IgnoreQueryFilters()
                    .FirstOrDefault(m => m.TenantId == tenantId && m.RoleId == role.RoleId);
                if (existing == null)
                {
                    ctx.EmployeeRoleMappings.Add(new HierarchyEmployeeRoleMapping
                    {
                        TenantId = tenantId,
                        RoleId = role.RoleId,
                        Kind = entry.Kind,
                        DisplayNameJson = entry.DisplayNameJson
                    });
                }
                else
                {
                    existing.Kind = entry.Kind;
                    existing.DisplayNameJson = entry.DisplayNameJson;
                }

                changed = true;
            }

            if (changed)
            {
                db.SaveChanges();
            }
        }
    }
}
