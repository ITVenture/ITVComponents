using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Threading;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat.Interceptors
{
    /// <summary>
    /// Flat-strategy save-changes interceptor that materializes <c>EmployeeRole</c> assignments onto the
    /// underlying user: when an EmployeeRole (Employee↔DirectRole-mapping) is added and the employee is already
    /// linked to a TenantUser, the corresponding <c>UserRole</c> is created; when removed, the UserRole is
    /// dropped unless another EmployeeRole of the same employee still maps to that role. Permission inheritance
    /// of PermissionSet-mappings is handled by the existing security interceptor (via RoleRole), so this only
    /// concerns the direct user↔role link.
    /// <para>
    /// Pending employees (no TenantUser yet) are skipped here; their roles are materialized at invitation
    /// acceptance. Note: a role both directly assigned and employee-derived is treated as employee-owned, so
    /// removing the last EmployeeRole for it also removes the UserRole.
    /// </para>
    /// </summary>
    public class EmployeeRoleMaterializationInterceptor : SaveChangesInterceptor
    {
        private readonly List<(int employeeId, int mappingId)> added = new();
        private readonly List<(int employeeId, int mappingId)> removed = new();

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
            => AsyncHelpers.RunSync(async () => await SavingChangesAsync(eventData, result).ConfigureAwait(false));

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context != null)
            {
                foreach (var entry in eventData.Context.ChangeTracker.Entries<EmployeeRole>().ToList())
                {
                    if (entry.State == EntityState.Added)
                    {
                        added.Add((entry.Entity.EmployeeId, entry.Entity.EmployeeRoleMappingId));
                    }
                    else if (entry.State == EntityState.Deleted)
                    {
                        removed.Add((entry.Entity.EmployeeId, entry.Entity.EmployeeRoleMappingId));
                    }
                }
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
            => AsyncHelpers.RunSync(async () => await SavedChangesAsync(eventData, result).ConfigureAwait(false));

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            var ctx = eventData.Context;
            if (ctx != null && (added.Count != 0 || removed.Count != 0))
            {
                bool changed;
                try
                {
                    changed = Reconcile(ctx);
                }
                finally
                {
                    added.Clear();
                    removed.Clear();
                }

                if (changed)
                {
                    await ctx.SaveChangesAsync(cancellationToken);
                }
            }

            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        private bool Reconcile(DbContext ctx)
        {
            var changed = false;
            var employeeIds = added.Select(a => a.employeeId).Concat(removed.Select(r => r.employeeId)).Distinct().ToList();
            var employees = ctx.Set<Employee>().IgnoreQueryFilters()
                .Where(e => employeeIds.Contains(e.EmployeeId))
                .ToDictionary(e => e.EmployeeId, e => e.TenantUserId);

            foreach (var (employeeId, mappingId) in added)
            {
                if (!employees.TryGetValue(employeeId, out var tenantUserId) || tenantUserId == null)
                {
                    continue;
                }

                var roleId = ctx.Set<EmployeeRoleMapping>().IgnoreQueryFilters()
                    .Where(m => m.EmployeeRoleMappingId == mappingId).Select(m => (int?)m.RoleId).FirstOrDefault();
                if (roleId == null)
                {
                    continue;
                }

                var exists = ctx.Set<UserRole>().Any(ur => ur.TenantUserId == tenantUserId && ur.RoleId == roleId);
                if (!exists)
                {
                    ctx.Set<UserRole>().Add(new UserRole { TenantUserId = tenantUserId, RoleId = roleId });
                    changed = true;
                }
            }

            foreach (var (employeeId, mappingId) in removed)
            {
                if (!employees.TryGetValue(employeeId, out var tenantUserId) || tenantUserId == null)
                {
                    continue;
                }

                var roleId = ctx.Set<EmployeeRoleMapping>().IgnoreQueryFilters()
                    .Where(m => m.EmployeeRoleMappingId == mappingId).Select(m => (int?)m.RoleId).FirstOrDefault();
                if (roleId == null)
                {
                    continue;
                }

                var stillAssigned = ctx.Set<EmployeeRole>().IgnoreQueryFilters()
                    .Any(er => er.EmployeeId == employeeId && er.RoleMapping.RoleId == roleId);
                if (!stillAssigned)
                {
                    var stale = ctx.Set<UserRole>().Where(ur => ur.TenantUserId == tenantUserId && ur.RoleId == roleId).ToList();
                    if (stale.Count != 0)
                    {
                        ctx.Set<UserRole>().RemoveRange(stale);
                        changed = true;
                    }
                }
            }

            return changed;
        }
    }
}
