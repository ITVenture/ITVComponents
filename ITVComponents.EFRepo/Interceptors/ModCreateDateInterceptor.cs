using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ITVComponents.EFRepo.Interceptors
{
    public class ModCreateInterceptor:ISaveChangesInterceptor
    {
        private readonly string userName;
        private readonly bool useUtc;

        public ModCreateInterceptor(string userName, bool useUtc)
        {
            this.userName = userName;
            this.useUtc = useUtc;
        }

        public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            var l = eventData.Context.ChangeTracker.Entries().ToList();
            foreach (var entry in l)
            {
                foreach (var m in entry.Members.Where(m =>
                                 Attribute.IsDefined(m.Metadata.PropertyInfo, typeof(ModMarkerAttribute)))
                             .Select(f => new
                             {
                                 f.Metadata.PropertyInfo,
                                 Attribute = (ModMarkerAttribute)Attribute.GetCustomAttribute(f.Metadata.PropertyInfo,
                                     typeof(ModMarkerAttribute), true)
                             }))
                {
                    DateTime dt = useUtc ? DateTime.UtcNow : DateTime.Now;
                    if (entry.State == EntityState.Added)
                    {
                        if (m.Attribute is CreatedAttribute && m.PropertyInfo.PropertyType == typeof(DateTime))
                        {
                            m.PropertyInfo.SetValue(entry.Entity, dt);
                        }
                        else if (m.Attribute is CreatorAttribute && m.PropertyInfo.PropertyType == typeof(string))
                        {
                            m.PropertyInfo.SetValue(entry.Entity, userName);
                        }
                    }

                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    {
                        if (m.Attribute is ModifiedAttribute && m.PropertyInfo.PropertyType == typeof(DateTime))
                        {
                            m.PropertyInfo.SetValue(entry.Entity, dt);
                        }
                        else if (m.Attribute is ModifierAttribute && m.PropertyInfo.PropertyType == typeof(string))
                        {
                            m.PropertyInfo.SetValue(entry.Entity, userName);
                        }
                    }
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
