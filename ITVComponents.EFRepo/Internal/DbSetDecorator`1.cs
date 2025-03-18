using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.EFRepo.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ITVComponents.EFRepo.Internal
{
    internal class DbSetDecorator<T>:IDbSet where T:class, new()
    {
        public PropertyInfo PropertyInfo { get; }
        private DbSet<T> decorated;

        public DbSetDecorator(PropertyInfo propertyInfo, DbSet<T> decorated)
        {
            PropertyInfo = propertyInfo;
            this.decorated = decorated;
        }

        public DbSetDecorator(DbSet<T> decorated)
        {
            this.decorated = decorated;
        }

        public Type EntityType => typeof(T);

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

        public IQueryable<T> QueryAndSort(FilterBase filter, Sort[] sorts, Func<string,string[]> redirectColumn = null)
        {
            return GetQueryDecorator(filter, sorts, redirectColumn);
        }

        IQueryableWrapper IDbSet.QueryAndSort(FilterBase filter, Sort[] sorts,
            Func<string, string[]> redirectColumn = null)
        {
            return GetQueryDecorator(filter, sorts, redirectColumn);
        }

        public object FindWithQuery(Dictionary<string, object> query, bool ignoreNotFound)
        {
            var filter = new CompositeFilter { Operator = BoolOperator.And };
            var l = new List<FilterBase>();
            foreach (var q in query)
            {
                l.Add(new CompareFilter
                {
                    Operator = CompareOperator.Equal, PropertyName = q.Key, Value = q.Value
                });
            }

            filter.Children = l.ToArray();
            var qr = ExpressionBuilder.BuildExpression<T>(filter);
            var tmp = decorated.Where(qr).ToArray();
            if (tmp.Length == 0)
            {
                if (!ignoreNotFound)
                {
                    throw new InvalidOperationException($"The Query ({filter}) does not deliver a result for Entity-Type {typeof(T)}.");
                }

                return null;
            }

            if (tmp.Length != 1)
            {
                throw new InvalidOperationException($"The Query ({filter}) is not unique for Entity-Type {typeof(T)}.");
            }

            return tmp[0];
        }

        public object GetIndex(object entity)
        {
            return decorated.EntityType.FindPrimaryKey().Properties.First().PropertyInfo.GetValue(entity);
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

        public object New()
        {
            return new T();
        }

        private QueryableDecorator<T> GetQueryDecorator(FilterBase filter, Sort[] sorts, Func<string, string[]> redirectColumn)
        {
            var filtered = decorated.Where(ExpressionBuilder.BuildExpression<T>(filter, redirectColumn));
            foreach (var sort in sorts)
            {
                var cols = redirectColumn?.Invoke(sort.MemberName) ?? new[] { sort.MemberName };
                foreach (var c in cols)
                {
                    if (sort.Direction == SortDirection.Ascending)
                    {
                        filtered = filtered.OrderBy(
                            ExpressionBuilder.BuildPropertyAccessExpression<T>(c));
                    }
                    else
                    {
                        filtered = filtered.OrderByDescending(
                            ExpressionBuilder.BuildPropertyAccessExpression<T>(c));
                    }
                }
            }

            return new QueryableDecorator<T>(filtered);
        }
    }
}
