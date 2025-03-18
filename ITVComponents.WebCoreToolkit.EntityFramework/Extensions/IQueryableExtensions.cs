using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Extensions
{
    public static class QueryableExtensions
    {
        public static IQueryable<T> WithDefaultFilter<T>(this IQueryable<T> baseQuery, HttpRequest request,
            Func<string, string[]> redirectProps = null,
            Func<Type, string, bool> useProperty = null, Func<string, string> unpackString = null, string nativeConfigurationName = null)
        {
            var t = typeof(T);
            var ft = request.ViewFilter<T>(redirectProps, useProperty, unpackString, nativeConfigurationName);
            var retVal = baseQuery;
            if (ft != null)
            {
                retVal = retVal.Where(ft);
            }

            return retVal;
        }
    }
}
