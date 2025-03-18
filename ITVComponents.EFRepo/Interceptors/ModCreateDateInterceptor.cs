using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.EFRepo.DIIntegration;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ITVComponents.EFRepo.Interceptors
{
    public class ModCreateInterceptor:ISaveChangesInterceptor
    {
        private readonly IServiceProvider services;
        private readonly ICurrentUserProvider localUserProvider;
        private readonly bool useUtc;

        public ModCreateInterceptor(IServiceProvider services, bool useUtc)
        {
            this.services = services;
            this.useUtc = useUtc;
        }

        public ModCreateInterceptor(ICurrentUserProvider userProvider, bool useUtc)
        {
            localUserProvider = userProvider;
            this.useUtc = useUtc;
        }

        public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context != null)
            {
                var contextType = eventData.Context.GetType();
                var userProviderType = typeof(ICurrentUserProvider<>).MakeGenericType(contextType);
                var userProvider = localUserProvider ??
                                   (ICurrentUserProvider)services.GetService(
                                       userProviderType);
                if (userProvider != null)
                {
                    var l = eventData.Context.ChangeTracker.Entries().ToList();
                    var userName = userProvider.GetUserName(eventData.Context);
                    if (!string.IsNullOrEmpty(userName))
                    {
                        foreach (var entry in l)
                        {
                            foreach (var m in entry.Members.Where(m =>
                                             m.Metadata.PropertyInfo != null && Attribute.IsDefined(
                                                 m.Metadata.PropertyInfo,
                                                 typeof(ModMarkerAttribute)))
                                         .Select(f => new
                                         {
                                             f.Metadata.PropertyInfo,
                                             Attribute = (ModMarkerAttribute)Attribute.GetCustomAttribute(
                                                 f.Metadata.PropertyInfo,
                                                 typeof(ModMarkerAttribute), true)
                                         }))
                            {
                                DateTime dt = useUtc ? DateTime.UtcNow : DateTime.Now;
                                if (entry.State == EntityState.Added)
                                {
                                    if (m.Attribute is CreatedAttribute &&
                                        m.PropertyInfo.PropertyType == typeof(DateTime))
                                    {
                                        m.PropertyInfo.SetValue(entry.Entity, dt);
                                    }
                                    else if (m.Attribute is CreatorAttribute &&
                                             m.PropertyInfo.PropertyType == typeof(string))
                                    {
                                        m.PropertyInfo.SetValue(entry.Entity, userName);
                                    }
                                }

                                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                                {
                                    if (m.Attribute is ModifiedAttribute &&
                                        m.PropertyInfo.PropertyType == typeof(DateTime))
                                    {
                                        m.PropertyInfo.SetValue(entry.Entity, dt);
                                    }
                                    else if (m.Attribute is ModifierAttribute &&
                                             m.PropertyInfo.PropertyType == typeof(string))
                                    {
                                        m.PropertyInfo.SetValue(entry.Entity, userName);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        LogEnvironment.LogEvent("Current Context is not bound to a specific user. No action is performed.", LogSeverity.Report);
                    }
                }
                else
                {
                    LogEnvironment.LogEvent($"No Service of Type {userProviderType} was found.", LogSeverity.Error);
                }
            }

            return result;
        }

        public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            return result;
        }

        public void SaveChangesFailed(DbContextErrorEventData eventData)
        {
        }

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return ValueTask.FromResult(SavingChanges(eventData, result));
        }

        public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return ValueTask.FromResult(result);
        }

        public Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.CompletedTask;
        }
    }
}
