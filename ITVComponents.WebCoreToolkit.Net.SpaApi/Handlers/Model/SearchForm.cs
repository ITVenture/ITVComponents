using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.WebCoreToolkit.Net.Handlers;
using ITVComponents.WebCoreToolkit.Net.SpaApi.Parsers;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.Handlers.Model
{
    public class SearchForm
    {
        public static ValueTask<SearchForm?> BindAsync(HttpContext httpContext, ParameterInfo parameter)
        {
            var newDic = Tools.TranslateQuery(httpContext.Request.Query, (k, v) =>
            {
                switch (k)
                {
                    case "filter":
                    {
                        var parser = new FilterParser();
                        return parser.GetFilter(v).FirstOrDefault()?.ToFilter();
                    }
                    case "sort":
                    {
                        string s = v;
                        return (from i in s.Split("~") select i.Split("-")).Select(arr =>
                        {
                            var retVal = new Sort { MemberName = arr[0] };
                            if (arr.Length > 1)
                            {
                                retVal.Direction =
                                    arr[1] == "desc" ? SortDirection.Descending : SortDirection.Ascending;
                            }
                            return retVal;
                        }).ToArray();
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

        public Dictionary<string, object> SearchDictionary { get; set; }

        public FilterBase Filter
        {
            get
            {
                if (SearchDictionary.TryGetValue("parsedfilter", out var raw) && raw is FilterBase fi)
                {
                    return fi;
                }

                return null;
            }
        }

        public Sort[] Sorts
        {
            get
            {
                if (SearchDictionary.TryGetValue("parsedsort", out var raw) && raw is Sort[] so)
                {
                    return so;
                }

                return null;
            }
        }

        public int Page
        {
            get
            {
                if (SearchDictionary.TryGetValue("parsedpage", out var raw) && raw is int page)
                {
                    return page;
                }

                return 0;
            }
        }

        public int PageSize
        {
            get
            {
                if (SearchDictionary.TryGetValue("parsedpageSize", out var raw) && raw is int ps)
                {
                    return ps;
                }

                return 0;
            }
        }
    }
}
