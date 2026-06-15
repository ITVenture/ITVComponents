using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Threading;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree.Interceptors
{
    /// <summary>
    /// Hierarchy-strategy counterpart of the flat employee-role materialization interceptor. Same behaviour,
    /// bound to the Hierarchy onboarding entities and the tree identity <c>UserRole</c>.
    /// </summary>
    public class HierarchyEmployeeRoleMaterializationInterceptor : SaveChangesInterceptor
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
                foreach (var entry in eventData.Context.ChangeTracker.Entries<HierarchyEmployeeRole>().ToList())
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
            var employees = ctx.Set<HierarchyEmployee>().IgnoreQueryFilters()
                .Where(e => employeeIds.Contains(e.EmployeeId))
                .ToDictionary(e => e.EmployeeId, e => e.TenantUserId);

            foreach (var (employeeId, mappingId) in added)
            {
                if (!employees.TryGetValue(employeeId, out var tenantUserId) || tenantUserId == null)
                {
                    continue;
                }

                var roleId = ctx.Set<HierarchyEmployeeRoleMapping>().IgnoreQueryFilters()
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

                var roleId = ctx.Set<HierarchyEmployeeRoleMapping>().IgnoreQueryFilters()
                    .Where(m => m.EmployeeRoleMappingId == mappingId).Select(m => (int?)m.RoleId).FirstOrDefault();
                if (roleId == null)
                {
                    continue;
                }

                var stillAssigned = ctx.Set<HierarchyEmployeeRole>().IgnoreQueryFilters()
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
