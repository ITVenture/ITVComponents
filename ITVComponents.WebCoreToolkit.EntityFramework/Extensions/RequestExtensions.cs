using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess;
using ITVComponents.EFRepo.DynamicData;
using ITVComponents.EFRepo.Expressions;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core.Native;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Extensions
{
    public static class RequestExtensions
    {
        /// <summary>
        /// Creates a default query based on the provided parameters on the current view
        /// </summary>
        /// <typeparam name="T">the type for which to get the default-query</typeparam>
        /// <param name="basicRequest">the basic-request that identifies the current view and the current data-request</param>
        /// <param name="useProperty">provides a callback that enables the caller to decide whether the specified parameter can be used for the target query</param>
        /// <param name="redirectProps">provides a callback that allows the caller to redirect properties to a different property or a property-path</param>
        /// <param name="unpackString">provides a callback to unpack the query-string when a linq-query was provided in the referrer query</param>
        /// <param name="nativeConfigName">the native configuration name that is used to compile linq queries</param>
        /// <returns>an expression that provides the basic-query for the given entity-type</returns>
        public static Expression<Func<T, bool>> ViewFilter<T>(this HttpRequest basicRequest,
            Func<string, string> redirectProps = null, Func<Type, string, bool> useProperty = null, Func<string,string> unpackString = null, string nativeConfigName = null)
        {
            var filter = basicRequest.BuildViewFilter<T>(unpackString, nativeConfigName);
            if (filter != null)
            {
                return ExpressionBuilder.BuildExpression<T>(filter, redirectProps, useProperty);
            }

            return null;
        }

        /// <summary>
        /// Creates a default query based on the provided parameters on the current view
        /// </summary>
        /// <typeparam name="T">the type for which to get the default-query</typeparam>
        /// <param name="basicRequest">the basic-request that identifies the current view and the current data-request</param>
        /// <param name="unpackString">provides a callback to unpack the query-string when a linq-query was provided in the referrer query</param>
        /// <param name="nativeConfigName">the native configuration name that is used to compile linq queries</param>
        /// <returns>an expression that provides the basic-query for the given entity-type</returns>
        public static FilterBase BuildViewFilter<T>(this HttpRequest basicRequest, Func<string,string> unpackString = null, string nativeConfigName = null)
        {
            nativeConfigName ??= "RFXQ";
            if (unpackString == null)
            {
                var svc = basicRequest.HttpContext.RequestServices.GetService<ISecurityRepository>();
                var scopeProvider = basicRequest.HttpContext.RequestServices.GetService<IPermissionScope>();
                if (svc != null && scopeProvider != null)
                {
                    unpackString = s =>
                    {
                        var raw = WebEncoders.Base64UrlDecode(s);
                        var decrypted = svc.Decrypt(raw, scopeProvider.PermissionPrefix);
                        return Encoding.Default.GetString(decrypted);
                    };
                }
                else
                {
                    if (PasswordSecurity.Initialized)
                    {
                        unpackString = s =>
                        {
                            var raw = WebEncoders.Base64UrlDecode(s);
                            var decrypted = raw.Decrypt();
                            return Encoding.Default.GetString(decrypted);
                        };
                    }
                    else
                    {
                        unpackString = s => s;
                    }
                }
            }
            
            var refQuery = basicRequest.GetRefererQuery();
            if (refQuery != null)
            {
                var reqs = refQuery.Keys.Where(n => n.StartsWith("RFQ_")).ToArray();
                var reqx = refQuery.Keys.FirstOrDefault(n => n == "RFXQ");
                if (!string.IsNullOrEmpty(reqx))
                {
                    string s = unpackString(refQuery["RFXQ"]);
                    return new LinqFilter<T>(s, nativeConfigName);
                }

                if (reqs.Length > 0)
                {
                    var filter = new CompositeFilter { Operator = BoolOperator.And };
                    //List<FilterBase> fils = new();
                    foreach (var rq in reqs)
                    {
                        var field = rq.Substring(4);
                        string[] value = refQuery[rq].ToArray();
                        var op = CompareOperator.Equal;
                        var opK = $"ROP_{field}";
                        string value2 = null;
                        var v2K = $"RFQ2_{field}";
                        if (refQuery.ContainsKey(opK))
                        {
                            op = Enum.Parse<CompareOperator>(refQuery[opK]);
                        }

                        if (refQuery.ContainsKey(v2K) && value.Length == 1)
                        {
                            value2 = refQuery[v2K];
                        }

                        var target = filter;
                        if (value.Length > 1)
                        {
                            var sub = new CompositeFilter{Operator = BoolOperator.Or};
                            filter.AddFilter(sub);
                            target = sub;
                        }

                        for (int i = 0; i < value.Length; i++)
                        {
                            CompareFilter fil = new CompareFilter
                            {
                                Value = value[i],
                                Operator = op,
                                Value2 = value2,
                                PropertyName = field
                            };

                            target.AddFilter(fil);
                        }
                    }

                    return filter;
                }
            }

            return null;
        }
    }
}
