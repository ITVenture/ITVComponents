using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.WebCoreToolkit.Security.AssetLevelImpersonation;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health
{
    public class ScriptedHealthCheck:IHealthCheck
    {
        private readonly IBaseTenantContext db;
        private readonly IImpersonationControl impersonator;
        private readonly IWebPluginHelper pluginsAccess;

        public ScriptedHealthCheck(IBaseTenantContext db, IImpersonationControl impersonator, IWebPluginHelper pluginsAccess)
        {
            this.db = db;
            this.impersonator = impersonator;
            this.pluginsAccess = pluginsAccess;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            var check = await db.HealthScripts.FirstOrDefaultAsync(n => n.HealthScriptName == context.Registration.Name);
            if (check != null)
            {
                var vars = new Scope();
                using (var ctx = ExpressionParser.BeginRepl(vars,
                           i => DefaultCallbacks.PrepareDefaultCallbacks(i.Scope, i.ReplSession)))
                {
                    var script = ScriptFile<IHealthScript>.FromText(check.Script);
                    var healthScript = script.Execute(ctx);
                    if (!string.IsNullOrEmpty(healthScript.AssetKey))
                    {
                        using (impersonator.AsAssetAccessor(healthScript.AssetKey))
                        {
                            var factory = pluginsAccess.GetFactory();
                            healthScript.Results = new Dictionary<string, object>();
                            healthScript.GetPlugin = name => factory[name, true];

                        }

                    }
                }
            }

            return new HealthCheckResult(HealthStatus.Healthy, "Unchecked");
        }
    }
}
