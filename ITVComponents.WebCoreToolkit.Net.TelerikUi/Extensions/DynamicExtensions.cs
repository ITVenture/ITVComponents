using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DynamicData;
using ITVComponents.EFRepo.Expressions;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.DynamicData.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Helpers;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions
{
    public static class DynamicExtensions
    {
        public static DummyDataSourceResult ToDummyDataSourceResult<T>(this IEnumerable<T> raw, RequestModel<T> request,
            Func<T, object> modelSelect, Func<string,string> redirectColumnName = null, Func<string, CustomFilter<T>> customFilterCallback = null) where T : class
        {
            var filter = GetFilter(request);
            if (request.CustomFilters != null && request.CustomFilters.Length != 0 && customFilterCallback != null)
            {
                List<FilterBase> additionalFilters = new List<FilterBase>();
                if (filter != null)
                {
                    additionalFilters.Add(filter);
                }
                foreach (var s in request.CustomFilters)
                {
                    var tmp = customFilterCallback(s);
                    if (tmp != null)
                    {
                        additionalFilters.Add(tmp);
                    }
                }

                filter = new CompositeFilter
                {
                    Children = additionalFilters.ToArray(),
                    Operator = BoolOperator.And
                };
            }

            var preFiltered = raw.AsQueryable();
            if (filter != null)
            {
                preFiltered = preFiltered.Where(ExpressionBuilder.BuildExpression<T>(filter, redirectColumnName));
            }

            if (request.Sorts != null && request.Sorts.Length != 0)
            {
                foreach (var sort in request.Sorts)
                {
                    if (sort.SortOrder == SortOrder.Asc)
                    {
                        preFiltered =
                            preFiltered.OrderBy(ExpressionBuilder.BuildPropertyAccessExpression<T>(sort.ColumnName, redirectColumnName));
                    }
                    else
                    {
                        preFiltered =
                            preFiltered.OrderByDescending(ExpressionBuilder.BuildPropertyAccessExpression<T>(sort.ColumnName, redirectColumnName));
                    }
                }
            }

            var retVal = new DummyDataSourceResult
            {
                Total = preFiltered.Count()
            };

            if (request.Page != null && request.PageSize != null && request.PageSize != 0)
            {
                preFiltered = preFiltered.Skip((request.Page.Value - 1) * request.PageSize.Value)
                    .Take(request.PageSize.Value);
            }

            retVal.Data = preFiltered.ToList().Select(modelSelect);
            return retVal;
        }

        public static FilterBase GetFilter<T>(this RequestModel<T> request) where T:class
        {
            FilterBase retVal = null;
            if (request?.Filters != null)
            {
                var fi = request.Filters;
                retVal = TranslateFilter(fi);
            }

            return retVal;
        }

        private static FilterBase TranslateFilter(FilterModel fi)
        {
            if (fi.Children != null && fi.Children.Length != 0)
            {
                return new CompositeFilter
                {
                    Operator = Enum.Parse<BoolOperator>(fi.GroupOp),
                    Children = (from t in fi.Children select TranslateFilter(t)).ToArray()
                };

            }


            return new CompareFilter
            {
                PropertyName = fi.ColumnName,
                Operator = Enum.Parse<CompareOperator>(fi.BinaryOp),
                Value = fi.Value
            };
        }
    }
}
