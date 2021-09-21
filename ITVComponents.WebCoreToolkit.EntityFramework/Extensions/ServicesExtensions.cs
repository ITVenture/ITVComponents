using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Decisions.Entities;
using ITVComponents.Decisions.Entities.Results;
using ITVComponents.EFRepo.DynamicData;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.DataSources;
using ITVComponents.WebCoreToolkit.EntityFramework.DataSources.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.DiagnosticsQueries;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.Diagnostics;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.ForeignKeys;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Extensions
{
    public static class ServicesExtensions
    {
        /// <summary>
        /// Gets the EntityContext for a DiagnosticsQuery and the DiagnosticsQuery object that represents the requested query
        /// </summary>
        /// <param name="services">the services for the current request</param>
        /// <param name="queryName">the name of the requested query</param>
        /// <param name="queryObject">the query-object that was found in the Diagnostics-DB</param>
        /// <returns>the Db-Context that is used to execute the requested query</returns>
        public static IWrappedDataSource ContextForDiagnosticsQuery(this IServiceProvider services, string queryName,
            string area, out DiagnosticsQueryDefinition queryObject)
        {
            var store = services.GetService<IDiagnosticsStore>();
            queryObject = store.GetQuery(queryName);
            if (queryObject != null)
            {
                if (services.VerifyUserPermissions(new[] { queryObject.Permission }))
                {
                    var options = services.GetService<IOptions<DiagnosticsSourceOptions>>().Value;
                    Func<IServiceProvider, string, string, object> factory = null;
                    var connection = queryObject.DbContext;
                    if (options.Factories.ContainsKey(connection))
                    {
                        factory = options.Factories[connection];
                    }
                    else if (options.Factories.ContainsKey("*"))
                    {
                        factory = options.Factories["*"];
                    }

                    if (factory != null)
                    {
                        var retVal = factory(services, connection, area);
                        if (retVal is DbContext dbc)
                        {
                            return new WrappedDbContext(dbc);
                        }
                        else if (retVal is DynamicDataAdapter dynda)
                        {
                            return new WrappedDynamicDataAdapter(dynda);
                        }
                        else
                        {
                            throw new InvalidOperationException("Unexpected returned value");
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the EntityContext for a ForeignKey-Query by the friendlyName of the DBContext
        /// </summary>
        /// <param name="services">the serviceProvider containing services for the current request</param>
        /// <param name="contextName">the friendlyname of the required context</param>
        /// <returns>a dbcontext object when it could be created</returns>
        public static IWrappedFkSource ContextForFkQuery(this IServiceProvider services, string contextName,
            string area)
        {
            var options = services.GetService<IOptions<ForeignKeySourceOptions>>().Value;
            Func<IServiceProvider, string, string, object> factory = null;
            if (options.Factories.ContainsKey(contextName))
            {
                factory = options.Factories[contextName];
            }
            else if (options.Factories.ContainsKey("*"))
            {
                factory = options.Factories["*"];
            }

            if (factory != null)
            {
                var retVal = factory(services, contextName, area);
                if (retVal is DbContext dbc)
                {
                    return new WrappedDbContext(dbc);
                }
                else if (retVal is DynamicDataAdapter dynda)
                {
                    return new WrappedDynamicDataAdapter(dynda);
                }
                else if (retVal is IForeignKeyProvider fkp)
                {
                    return new WrappedCustomFkSource(fkp);
                }
                else
                {
                    throw new InvalidOperationException("Unexpected returned value");
                }
            }

            return null;
        }

        /// <summary>
        /// Translates the given data to its viewModel and applies Diagnostics queries if available
        /// </summary>
        /// <typeparam name="TOrigin">the original data that was selected from a database</typeparam>
        /// <typeparam name="TViewModel">the target viewmodel on which to translate the data</typeparam>
        /// <param name="services">the services for the current request</param>
        /// <param name="data">the selected data</param>
        /// <param name="argumentsFor">provides arguments for a specific record</param>
        /// <returns></returns>
        public static IEnumerable<TViewModel> DiagnoseResults<TOrigin, TViewModel>(this IServiceProvider services,
            IEnumerable<TOrigin> data, Func<TOrigin, IDictionary<string, object>> argumentsFor,
            Action<TOrigin, TViewModel> postProcess)
            where TViewModel : class, new()
            where TOrigin : class
        {
            Dictionary<string, DiagnoseQueryHelper.DiagQueryItem> knownQueries =
                new Dictionary<string, DiagnoseQueryHelper.DiagQueryItem>();
            DiagnoseQueryHelper.DiagEntityAnlyseItem[] typeAnalysis = null;
            var first = true;
            foreach (var t in data)
            {
                yield return DiagnoseResult(services, t, argumentsFor(t), knownQueries, ref typeAnalysis, postProcess);
            }
        }

        /// <summary>
        /// Translates the given data to its viewModel and applies Diagnostics queries if available
        /// </summary>
        /// <typeparam name="TOrigin">the original data that was selected from a database</typeparam>
        /// <typeparam name="TViewModel">the target viewmodel on which to translate the data</typeparam>
        /// <param name="services">the services for the current request</param>
        /// <param name="originalItem">the selected data</param>
        /// <param name="queryArguments">provides arguments for a specific record</param>
        /// <returns></returns>
        public static TViewModel DiagnoseResult<TOrigin, TViewModel>(this IServiceProvider services,
            TOrigin originalItem, IDictionary<string, object> queryArguments,
            Dictionary<string, DiagnoseQueryHelper.DiagQueryItem> knownQueries,
            ref DiagnoseQueryHelper.DiagEntityAnlyseItem[] typeAnalysis, Action<TOrigin, TViewModel> postProcess = null)
            where TViewModel : class, new()
            where TOrigin : class

        {
            typeAnalysis ??= DiagnoseQueryHelper.AnalyseViewModel<TViewModel>();
            IHttpContextAccessor httpContext = services.GetService<IHttpContextAccessor>();
            string area = null;
            if (httpContext?.HttpContext != null)
            {
                var routeData = httpContext.HttpContext.GetRouteData();
                if (routeData.Values.ContainsKey("area"))
                {
                    area = (string)routeData.Values["area"];
                }
            }

            var ret = originalItem.ToViewModel<TOrigin, TViewModel>(t =>
            {
                if (t == typeof(IServiceProvider))
                {
                    return services;
                }

                if (t == typeof(DiagnoseQueryOptions))
                {
                    return new DiagnoseQueryOptions
                    {
                        Area = area,
                        KnownQueries = knownQueries,
                        Arguments = queryArguments
                    };
                }

                return null;
            }, postProcess);
/*if (typeAnalysis.Length != 0)
{
                foreach (var p in typeAnalysis)
                {
                    IWrappedDataSource context = null;
                    DiagnosticsQuery qr = null;
                    if (knownQueries.ContainsKey(p.Attribute.DiagnosticQueryName))
                    {
                        var item = knownQueries[p.Attribute.DiagnosticQueryName];
                        context = item.Context;
                        qr = item.Query;
                    }
                    else
                    {
                        context = services.ContextForDiagnosticsQuery(p.Attribute.DiagnosticQueryName, area, out qr);
                        knownQueries.Add(p.Attribute.DiagnosticQueryName, new DiagnoseQueryHelper.DiagQueryItem
                        {
                            Query = qr,
                            Context = context
                        });
                    }

                    if (context != null)
                    {
                        if (p.Property.PropertyType == typeof(SimpleTriStateResult))
                        {
                            p.Property.SetValue(ret, context.RunDiagnosticsQuery(qr, queryArguments).Cast<SimpleTriStateResult>().FirstOrDefault());
                        }
                        else
                        {
                            p.Property.SetValue(ret, context.RunDiagnosticsQuery(qr, queryArguments).Cast<object>().FirstOrDefault());
                        }
                    }
                }

                postProcess?.Invoke(originalItem, ret);

            }*/

            return ret;
        }
    }
}
