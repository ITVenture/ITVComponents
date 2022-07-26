using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Dynamitey.DynamicObjects;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Extensions;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Handlers;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Model;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Helpers
{
    internal static class ResponseFormatterBuilder
    {
        public static void BuildCallbacks(ActivationSettings settings, out Func<HttpContext, HealthReport, Task> appInfo,
            out Func<HttpContext, HealthReport, Task> readyInfo, out Func<HttpContext, HealthReport, Task> liveInfo)
        {
            BuildModelTypes(settings, out var appInfoType, out var readyInfoType, out var liveInfoType);
            var t = typeof(HealthHandler<,,>).MakeGenericType(appInfoType, readyInfoType, liveInfoType);
            appInfo = t.GetMethod("AppInfo", BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Static, new []{typeof(HttpContext), typeof(HealthReport)})
                .CreateDelegate<Func<HttpContext, HealthReport, Task>>();
            readyInfo = t.GetMethod("Ready", BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Static, new[] { typeof(HttpContext), typeof(HealthReport) })
                .CreateDelegate<Func<HttpContext, HealthReport, Task>>();
            liveInfo = t.GetMethod("Live", BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Static, new[] { typeof(HttpContext), typeof(HealthReport) })
                .CreateDelegate<Func<HttpContext, HealthReport, Task>>();
        }

        public static ExposeHealthEndPointsCallback BuildHealthExposer(ActivationSettings settings)
        {
            BuildModelTypes(settings, out var appInfoType, out var readyInfoType, out var liveInfoType);
            var meth = typeof(RouteExtensions).GetMethod("ExposeHealthEndPoints`3",
                BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Static);
            meth = meth.MakeGenericMethod(appInfoType, readyInfoType, liveInfoType);
            return meth.CreateDelegate<ExposeHealthEndPointsCallback>();
        }

        private static void BuildModelTypes(ActivationSettings settings, out Type appInfoType, out Type readyInfoType, out Type liveInfoType)
        {
            appInfoType = typeof(AppInfoModel);
            readyInfoType = typeof(ReadynessModel);
            liveInfoType = typeof(LivenessModel);
            if (!string.IsNullOrEmpty(settings.AppInfoModel) || !string.IsNullOrEmpty(settings.ReadyInfoModel) || !string.IsNullOrEmpty(settings.LiveInfoModel))
            {
                using (var repl = ExpressionParser.BeginRepl(new Dictionary<string, object>(),
                           p => DefaultCallbacks.PrepareDefaultCallbacks(p.Scope, p.ReplSession)))
                {
                    if (!string.IsNullOrEmpty(settings.AppInfoModel))
                    {
                        appInfoType = (Type)ExpressionParser.Parse(settings.AppInfoModel, repl);
                    }

                    if (!string.IsNullOrEmpty(settings.ReadyInfoModel))
                    {
                        readyInfoType = (Type)ExpressionParser.Parse(settings.ReadyInfoModel, repl);
                    }

                    if (!string.IsNullOrEmpty(settings.LiveInfoModel))
                    {
                        liveInfoType = (Type)ExpressionParser.Parse(settings.LiveInfoModel, repl);
                    }
                }
            }
        }
    }
}
