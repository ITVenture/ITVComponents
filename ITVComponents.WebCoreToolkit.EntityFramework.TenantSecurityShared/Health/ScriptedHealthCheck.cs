﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Scripting.CScript;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.AssetLevelImpersonation;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health
{
    public class ScriptedHealthCheck:IHealthCheck
    {
        private readonly IBaseTenantContext db;
        private readonly IWebPluginHelper pluginsAccess;
        private readonly IServiceProvider services;

        public ScriptedHealthCheck(IBaseTenantContext db, IWebPluginHelper pluginsAccess, IServiceProvider services)
        {
            this.db = db;
            this.pluginsAccess = pluginsAccess;
            this.services = services;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var check = await db.HealthScripts.FirstOrDefaultAsync(n =>
                n.HealthScriptName == context.Registration.Name);
            if (check != null)
            {
                lock (db)
                {
                    FullSecurityAccessHelper helper = null;
                    if (db.FilterAvailable)
                    {
                        helper = new FullSecurityAccessHelper(db, true, false);
                    }

                    try
                    {

                        var vars = new Scope();
                        using (var ctx = ExpressionParser.BeginRepl(vars,
                                   i => DefaultCallbacks.PrepareDefaultCallbacks(i.Scope, i.ReplSession)))
                        {
                            var script = ScriptFile<IHealthScript>.FromText(check.Script);
                            var healthScript = script.Execute(ctx);

                            if (!string.IsNullOrEmpty(healthScript.TargetTenant))
                            {
                                try
                                {
                                    var factory = pluginsAccess.GetFactory(healthScript.TargetTenant);
                                    healthScript.LogEnvironment = typeof(LogEnvironment);
                                    healthScript.LogSeverity = typeof(LogSeverity);
                                    healthScript.HealthStatus = typeof(HealthStatus);
                                    healthScript.HealthScriptResult = typeof(HealthScriptResult);
                                    healthScript.Results = new Dictionary<string, HealthScriptResult>();
                                    healthScript.GetPlugin = name => factory[name, true];
                                    healthScript.Services = new ServicesDecorator(services);
                                    healthScript.OverAllResult = HealthStatus.Healthy;
                                    healthScript.OverAllDescription = "OK";
                                    healthScript.Callback = f =>
                                        (Action<IPlugin, HealthScriptResult>)f.CreateDelegate(
                                            typeof(Action<IPlugin, HealthScriptResult>));
                                    healthScript.TestPlugin = (name, testDetailFunc) =>
                                    {
                                        HealthScriptResult result = new HealthScriptResult
                                        {
                                            TestName = $"{check.HealthScriptName}_{name}",
                                            Status = HealthStatus.Healthy,
                                            StatusText = "OK"
                                        };
                                        try
                                        {
                                            if (healthScript.OverAllResult == HealthStatus.Healthy)
                                            {
                                                var plug = factory[name, true];
                                                testDetailFunc(plug, result);
                                            }
                                            else
                                            {
                                                result.Status = HealthStatus.Degraded;
                                                result.StatusText = "Skipped";
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            result.Status = HealthStatus.Unhealthy;
                                            result.StatusText = ex.Message;
                                            if (healthScript.OverAllResult == HealthStatus.Healthy)
                                            {
                                                healthScript.OverAllResult = HealthStatus.Degraded;
                                                healthScript.OverAllDescription = "Some tests failed.";
                                            }
                                        }
                                        finally
                                        {
                                            healthScript.Results.Add(result.TestName, result);
                                        }
                                    };

                                    try
                                    {
                                        healthScript.RunTests();
                                    }
                                    catch (Exception ex)
                                    {
                                        return new HealthCheckResult(HealthStatus.Unhealthy,
                                            $"Test {context.Registration.Name} failing", ex);
                                    }
                                }
                                finally
                                {
                                    pluginsAccess.ResetFactory();
                                }

                                var tmp = new Dictionary<string, object>();
                                foreach (var t in healthScript.Results)
                                {
                                    tmp.Add(t.Key, t.Value);
                                }

                                var result = new HealthCheckResult(healthScript.OverAllResult,
                                    healthScript.OverAllDescription, data: new ReadOnlyDictionary<string, object>(tmp));
                                return result;

                            }
                        }
                    }
                    finally
                    {
                        helper?.Dispose();
                    }
                }
            }

            return new HealthCheckResult(HealthStatus.Healthy, "Unchecked");
        }
    }
}