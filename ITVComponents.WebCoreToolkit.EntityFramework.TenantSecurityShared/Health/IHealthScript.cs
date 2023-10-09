using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;
using ITVComponents.Scripting.CScript.Core.Literals;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health
{
    public interface IHealthScript
    {
        Dictionary<string, HealthScriptResult> Results { get; set; }
        string TargetTenant{ get; set; }
        Func<string,object> GetPlugin { get; set; }

        Func<FunctionLiteral, Action<object, HealthScriptResult>> Callback { get; set; }
        Action<string, Action<object, HealthScriptResult>> TestPlugin { get; set; }
        IServices Services { get; set; }
        Type LogEnvironment { get; set; }
        Type LogSeverity { get; set; }
        Type HealthStatus { get; set; }
        Type HealthScriptResult { get; set; }
        HealthStatus OverAllResult { get; set; }
        string OverAllDescription { get; set; }
        void RunTests();
    }
}
