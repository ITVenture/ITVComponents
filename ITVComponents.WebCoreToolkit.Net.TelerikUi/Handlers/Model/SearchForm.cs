using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Net.Handlers;
using Kendo.Mvc;
using Kendo.Mvc.Infrastructure;
using Kendo.Mvc.UI;
using Kendo.Mvc.UI.Fluent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Handlers.Model
{
    public class SearchForm
    {
        public static ValueTask<SearchForm?> BindAsync(HttpContext httpContext, ParameterInfo parameter)
        {
            var newDic = Tools.TranslateForm(httpContext.Request.Form, (k, v) =>
            {
                switch (k)
                {
                    case "filter":
                    {
                        return ToEfFilters(FilterDescriptorFactory.Create(v));
                    }
                    case "sort":
                    {
                        return ToEfSorts(DataSourceDescriptorSerializer.Deserialize<SortDescriptor>(v));
                    }
                    case "aggregates":
                    {
                        return null; //DataSourceDescriptorSerializer.Deserialize<AggregateDescriptor>(v);
                    }
                    case "group":
                    {
                        return null; //DataSourceDescriptorSerializer.Deserialize<GroupDescriptor>(v);
                    }
                    case "page":
                    {
                        return int.Parse(v);
                    }
                    case "pageSize":
                    {
                        return int.Parse(v);
                    }
                    case "groupPaging":
                    {
                        return bool.Parse(v);
                    }
                    case "includeSubGroupCount":
                    {
                        return bool.Parse(v);
                    }
                    case "skip":
                    {
                        return int.Parse(v);
                    }
                    case "take":
                    {
                        return int.Parse(v);
                    }
                }

                return null;
            }, true);
            return ValueTask.FromResult<SearchForm?>(new SearchForm { SearchDictionary = newDic });
        }

        private static Sort[] ToEfSorts(IList<SortDescriptor> inSort)
        {
            return (from t in inSort
                select new Sort
                {
                    Direction = t.SortDirection == ListSortDirection.Ascending
                        ? SortDirection.Ascending
                        : SortDirection.Descending,
                    MemberName = t.Member
                }).ToArray();
        }

        private static FilterBase ToEfFilters(IList<IFilterDescriptor> filter)
        {
            var l = new List<FilterBase>();
            l.AddRange(ToEfFilterList(filter));
            if (l.Count > 1)
            {
                return new CompositeFilter
                {
                    Children = l.ToArray(),
                    Operator = BoolOperator.And
                };
            }

            if (l.Count == 1)
            {
                return l.First();
            }

            return null;
        }

        private static FilterBase[] ToEfFilterList(IEnumerable<IFilterDescriptor> filter)
        {
            var l = new List<FilterBase>();
            foreach (var tmp in filter)
            {
                if (tmp is CompositeFilterDescriptor cfd)
                {
                    var c =  ToEfFilterList(cfd.FilterDescriptors);
                    if (c.Length > 1)
                    {
                        l.Add(new CompositeFilter
                        {
                            Children = c,
                            Operator = cfd.LogicalOperator == FilterCompositionLogicalOperator.And
                                ? BoolOperator.And
                                : BoolOperator.Or
                        });
                    }
                    else if (c.Length == 1)
                    {
                        l.Add(c.First());
                    }
                }
                else if (tmp is FilterDescriptor sfd)
                {
                    l.Add(new CompareFilter
                    {
                        Value = sfd.Value,
                        Operator = TranslateOp(sfd.Operator),
                        PropertyName = sfd.Member
                    });
                }
                else
                {
                    throw new ArgumentException($"Unexpected Filter-Type: {tmp.GetType()}", nameof(filter));
                }
            }

            return l.ToArray();
        }

        private static CompareOperator TranslateOp(FilterOperator op)
        {
            switch (op)
            {
                case FilterOperator.IsLessThan:
                    return CompareOperator.LessThan;
                case FilterOperator.IsLessThanOrEqualTo:
                    return CompareOperator.LessThanOrEqual;
                case FilterOperator.IsEqualTo:
                    return CompareOperator.Equal;
                case FilterOperator.IsNotEqualTo:
                    return CompareOperator.NotEqual;
                case FilterOperator.IsGreaterThanOrEqualTo:
                    return CompareOperator.GreaterThanOrEqual;
                case FilterOperator.IsGreaterThan:
                    return CompareOperator.GreaterThan;
                case FilterOperator.StartsWith:
                    return CompareOperator.StartsWith;
                case FilterOperator.EndsWith:
                    return CompareOperator.EndsWith;
                case FilterOperator.Contains:
                    return CompareOperator.Contains;
                case FilterOperator.DoesNotContain:
                    return CompareOperator.ContainsNot;
                case FilterOperator.IsNull:
                    return CompareOperator.IsNull;
                case FilterOperator.IsNotNull:
                    return CompareOperator.IsNotNull;
                case FilterOperator.IsNullOrEmpty:
                case FilterOperator.IsEmpty:
                    return CompareOperator.IsEmpty;
                case FilterOperator.IsNotNullOrEmpty:
                case FilterOperator.IsNotEmpty:
                    return CompareOperator.IsNotEmpty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, null);
            }
        }

        public Dictionary<string, object> SearchDictionary { get; set; }

    }
}

/*
   DataSourceRequestModelBinder.TryGetValue<int>(modelMetadata, valueProvider, modelName, DataSourceRequestUrlParameters.Take, (Action<int>) (take => request.Take = take));
 */
