using ITVComponents.EFRepo.Expressions;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Handlers.Model;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Extensions
{
    public static class EnumerableExtensions
    {
        public static DataResult ToDataResult(this IEnumerable data)
        {
            var retVal = new DataResult();
            var src = data.Cast<object>().ToArray();
            retVal.Data = src;
            retVal.Total = src.Length;
            return retVal;
        }

        public static DataResult ToDataResult<T>(this IEnumerable<T> raw, SearchForm request,
            Func<T, object> modelSelect, Func<string, string[]> redirectColumnName = null, Func<string, CustomFilter<T>> customFilterCallback = null) where T : class
        {
            var filter = request.Filter;
            
            var preFiltered = raw.AsQueryable();
            if (filter != null)
            {
                preFiltered = preFiltered.Where(ExpressionBuilder.BuildExpression<T>(filter, redirectColumnName));
            }

            if (request.Sorts != null && request.Sorts.Length != 0)
            {
                foreach (var sort in request.Sorts)
                {
                    var cols = redirectColumnName?.Invoke(sort.MemberName) ?? new[] { sort.MemberName };
                    foreach (var c in cols)
                    {
                        var ordx = ExpressionBuilder.BuildPropertyAccessExpression<T>(c);
                        if (ordx != null)
                        {
                            if (sort.Direction == SortDirection.Ascending)
                            {

                                preFiltered =
                                    preFiltered.OrderBy(ordx);
                            }
                            else
                            {
                                preFiltered =
                                    preFiltered.OrderByDescending(ordx);
                            }
                        }
                    }
                }
            }

            var retVal = new DataResult()
            {
                Total = preFiltered.Count()
            };

            if (request.Page != 0 && request.PageSize != 0)
            {
                preFiltered = preFiltered.Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize);
            }

            retVal.Data = preFiltered.ToList().Select(modelSelect);
            return retVal;
        }
    }
}
