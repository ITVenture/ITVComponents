using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Decisions.Entities;
using ITVComponents.Decisions.Entities.Results;
using ITVComponents.EFRepo.DynamicData;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Json;
using ITVComponents.WebCoreToolkit.Configuration;
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
using ITVComponents.WebCoreToolkit.Security;
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
        /// Caches the concrete DbContext-Type for a given context-friendlyName, so that resolving the
        /// EntityWriteTracker for a connection does not require instantiating a context on every call.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Type> contextTypeCache = new();

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
                        IDisposable owner = null;
                        if (retVal is IScopedDataSource scoped)
                        {
                            owner = scoped.Scope;
                            retVal = scoped.Source;
                        }

                        if (retVal is DbContext dbc)
                        {
                            return new WrappedDbContext(dbc, services, owner);
                        }
                        else if (retVal is DynamicDataAdapter dynda)
                        {
                            return new WrappedDynamicDataAdapter(dynda, owner);
                        }
                        else
                        {
                            owner?.Dispose();
                            throw new InvalidOperationException("Unexpected returned value");
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the <see cref="IEntityWriteTracker"/> that is registered for the DbContext behind the given connection.
        /// The concrete context-type is determined once per connection and cached afterwards, so the underlying
        /// context-factory is only invoked on the first lookup.
        /// </summary>
        /// <param name="services">the serviceProvider containing services for the current request</param>
        /// <param name="contextName">the friendlyName of the required context</param>
        /// <param name="area">the area for which to resolve the context</param>
        /// <returns>the write-tracker for the context, or null when none is registered</returns>
        public static IEntityWriteTracker TrackerForContext(this IServiceProvider services, string contextName, string area)
        {
            if (!contextTypeCache.TryGetValue(contextName, out var contextType))
            {
                var factory = GetFactoryForContext(services, contextName);
                if (factory != null && factory(services, contextName, area) is DbContext dbc)
                {
                    contextType = dbc.GetType();
                    contextTypeCache[contextName] = contextType;
                }
            }

            if (contextType != null)
            {
                var svcType = typeof(IEntityWriteTracker<>).MakeGenericType(contextType);
                if (services.GetService(svcType) is IEntityWriteTracker tracker)
                {
                    return tracker;
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
            var factory = GetFactoryForContext(services, contextName);
            if (factory != null)
            {
                var retVal = factory(services, contextName, area);
                IDisposable owner = null;
                if (retVal is IScopedDataSource scoped)
                {
                    owner = scoped.Scope;
                    retVal = scoped.Source;
                }

                IWrappedFkSource ret;
                if (retVal is DbContext dbc)
                {
                    ret = new WrappedDbContext(dbc, services, owner);
                }
                else if (retVal is DynamicDataAdapter dynda)
                {
                    ret = new WrappedDynamicDataAdapter(dynda, owner);
                }
                else if (retVal is IForeignKeyProvider fkp)
                {
                    ret = new WrappedCustomFkSource(fkp, owner);
                }
                else
                {
                    owner?.Dispose();
                    throw new InvalidOperationException("Unexpected returned value");
                }

                if (retVal is IForeignKeyProviderWithOptions fkpc)
                {
                    var scopeOptions = services.GetService<IScopedSettingsProvider>();
                    var globalOptions = services.GetService<IGlobalSettingsProvider>();
                    var contextSettingsName = $"{contextName}ForeignKeySettings";
                    var contextSettingsRaw = scopeOptions?.GetJsonSetting(contextSettingsName) ??
                                             globalOptions?.GetJsonSetting(contextSettingsName);
                    if (!string.IsNullOrEmpty(contextSettingsRaw))
                    {
                        fkpc.DefaultFkOptions = JsonHelper.FromJsonString<ForeignKeyOptions>(contextSettingsRaw, SerializationTypingMode.StaticTyping);
                    }
                }

                return ret;
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
            try
            {
                foreach (var t in data)
                {
                    yield return DiagnoseResult(services, t, argumentsFor(t), knownQueries, ref typeAnalysis, postProcess);
                }
            }
            finally
            {
                // The per-record DiagnoseResult caches its diagnostics-data-source in knownQueries and re-uses it
                // across the whole loop (also shared with DiagnosticResultAttribute via DiagnoseQueryOptions).
                // Dispose them once the loop is done — releases any per-operation scope (scoped plugin + context).
                foreach (var item in knownQueries.Values)
                {
                    item.Context?.Dispose();
                }
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
            IContextUserProvider userProvider = services.GetService<IContextUserProvider>();
            string area = null;
            if (userProvider?.RouteData != null)
            {
                var routeData = userProvider.RouteData;
                if (routeData.ContainsKey("area"))
                {
                    area = (string)routeData["area"];
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

        private static Func<IServiceProvider, string, string, object> GetFactoryForContext(IServiceProvider services, string contextName)
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

            return factory;
        }
    }
}
