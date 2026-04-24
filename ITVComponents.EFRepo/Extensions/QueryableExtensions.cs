using ITVComponents.EFRepo.Expressions;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.EFRepo.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Helpers;

namespace ITVComponents.EFRepo.Extensions
{
    public static class QueryableExtensions
    {
        public static IQueryableWrapper<T> QueryAndSort<T>(this IQueryable<T> source,FilterBase filter, Sort[] sorts, Func<string, string[]> redirectColumn) where T : class, new()
        {
            var filtered = source.Where(ExpressionBuilder.BuildExpression<T>(filter, redirectColumn));
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
