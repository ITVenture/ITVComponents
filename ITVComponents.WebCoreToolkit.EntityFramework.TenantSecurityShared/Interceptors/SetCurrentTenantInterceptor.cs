using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Antlr4.Runtime.Atn;
using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.DataAnnotation;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.DIIntegration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.BinderModels;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Interceptors
{
    public class SetCurrentTenantInterceptor:SaveChangesInterceptor
    {
        private readonly IServiceProvider services;
        private readonly SetTenantType tenantType;

        public SetCurrentTenantInterceptor(IServiceProvider services, SetTenantType tenantType)
        {
            this.services = services;
            this.tenantType = tenantType;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return ValueTask.FromResult(SavingChanges(eventData, result));
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context != null)
            {
                var scopeProvider = services.GetService<IPermissionScope>();
                if (scopeProvider != null)
                {
                    if (!string.IsNullOrEmpty(scopeProvider.PermissionPrefix))
                    {
                        var l = eventData.Context.ChangeTracker.Entries().ToList();
                        var currentScopeName = scopeProvider.PermissionPrefix;
                        var scopeAsInt = -1;
                        if (tenantType == SetTenantType.Tenant)
                        {
                            scopeAsInt = eventData.Context.Set<Tenant>().First(n => n.TenantName == currentScopeName)
                                .TenantId;
                        }
                        else if (tenantType == SetTenantType.BinderTenant)
                        {
                            scopeAsInt = eventData.Context.Set<BinderTenant>()
                                .First(n => n.TenantName == currentScopeName)
                                .TenantId;
                        }

                        var scopeAsString = currentScopeName;
                        Guid ScopeAsGuid() => Guid.Parse(currentScopeName);
                        foreach (var entry in l)
                        {
                            foreach (var m in entry.Members.Where(m =>
                                             m.Metadata.PropertyInfo != null && Attribute.IsDefined(
                                                 m.Metadata.PropertyInfo,
                                                 typeof(AssignTenantAttribute)))
                                         .Select(f => new
                                         {
                                             f.Metadata.PropertyInfo
                                         }))
                            {
                                if (entry.State == EntityState.Added)
                                {
                                    if (m.PropertyInfo.PropertyType == typeof(int) &&
                                        m.PropertyInfo.GetValue(entry.Entity) is 0 ||
                                        m.PropertyInfo.PropertyType == typeof(int?) &&
                                        m.PropertyInfo.GetValue(entry.Entity) is null)
                                    {
                                        m.PropertyInfo.SetValue(entry.Entity, scopeAsInt);
                                    }
                                    else if (m.PropertyInfo.PropertyType == typeof(int?) &&
                                             m.PropertyInfo.GetValue(entry.Entity) is <= 0)
                                    {
                                        m.PropertyInfo.SetValue(entry.Entity, null);
                                    }
                                    else if (m.PropertyInfo.PropertyType == typeof(string) &&
                                             string.IsNullOrEmpty((string)m.PropertyInfo.GetValue(entry.Entity)))
                                    {
                                        m.PropertyInfo.SetValue(entry.Entity, scopeAsString);
                                    }
                                    else if (m.PropertyInfo.PropertyType == typeof(string) &&
                                             m.PropertyInfo.GetValue(entry.Entity) is "null")
                                    {
                                        m.PropertyInfo.SetValue(entry.Entity, null);
                                    }
                                    else if (m.PropertyInfo.PropertyType == typeof(Guid) &&
                                             m.PropertyInfo.GetValue(entry.Entity) is Guid g && g == Guid.Empty ||
                                             m.PropertyInfo.PropertyType == typeof(Guid?) &&
                                             m.PropertyInfo.GetValue(entry.Entity) is null)
                                    {
                                        m.PropertyInfo.SetValue(entry.Entity, ScopeAsGuid());
                                    }
                                    else if (m.PropertyInfo.PropertyType == typeof(Guid?) &&
                                             m.PropertyInfo.GetValue(entry.Entity) is Guid gnu && gnu == Guid.Empty)
                                    {
                                        m.PropertyInfo.SetValue(entry.Entity, null);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        LogEnvironment.LogEvent("Current Context is not bound to a specific tenant. No action is performed.", LogSeverity.Report);
                    }
                }
                else
                {
                    LogEnvironment.LogEvent($"No Service of Type {typeof(IPermissionScope)} was found.", LogSeverity.Error);
                }
            }

            return base.SavingChanges(eventData, result);
        }
    }
}
