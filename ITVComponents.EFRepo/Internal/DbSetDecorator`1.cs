using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ITVComponents.EFRepo.Internal
{
    internal class DbSetDecorator<T>:IDbSet where T:class
    {
        private DbSet<T> decorated;

        public DbSetDecorator(DbSet<T> decorated)
        {
            this.decorated = decorated;
        }
        public EntityEntry Add(object entity)
        {
            return decorated.Add((T) entity);
        }

        public void AddRange(IEnumerable entities)
        {
            decorated.AddRange(entities.Cast<T>());
        }

        public void AddRange(params object[] entities)
        {
            decorated.AddRange(entities.Cast<T>().ToArray());
        }

        public IQueryable AsQueryable()
        {
            return decorated.AsQueryable();
        }

        public EntityEntry Attach(object entity)
        {
            return decorated.Attach((T) entity);
        }

        public void AttachRange(IEnumerable entities)
        {
            decorated.AttachRange(entities.Cast<T>());
        }

        public void AttachRange(params object[] entities)
        {
            decorated.AttachRange(entities.Cast<T>().ToArray());
        }

        public object Find(params object[] keyValues)
        {
            return decorated.Find(keyValues);
        }

        public EntityEntry Remove(object entity)
        {
            return decorated.Remove((T)entity);
        }

        public void RemoveRange(params object[] entities)
        {
            decorated.RemoveRange(entities.Cast<T>().ToArray());
        }

        public void RemoveRange(IEnumerable entities)
        {
            decorated.RemoveRange(entities.Cast<T>());
        }

        public EntityEntry Update(object entity)
        {
            return decorated.Update((T) entity);
        }

        public void UpdateRange(params object[] entities)
        {
            decorated.UpdateRange(entities.Cast<T>().ToArray());
        }

        public void UpdateRange(IEnumerable entities)
        {
            decorated.UpdateRange(entities.Cast<T>());
        }
    }
}
