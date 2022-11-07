using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.Formatting;
using ITVComponents.Plugins;
using ITVComponents.Plugins.PluginServices;
using ITVComponents.TypeConversion;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Net.PlugInServices;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Data;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using Kendo.Mvc.Extensions;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ModuleConfigHandlers
{
    public class PluginConfigHandler
    {
        private readonly IBaseTenantContext context;
        private readonly IPermissionScope permissionScope;
        private readonly ISecurityRepository security;
        private readonly IInjectablePlugin<WebPluginAnalyzer> localLoader;

        public PluginConfigHandler(IBaseTenantContext context, IPermissionScope permissionScope, ISecurityRepository security, IInjectablePlugin<WebPluginAnalyzer> localLoader = null)
        {
            this.context = context;
            this.permissionScope = permissionScope;
            this.security = security;
            this.localLoader = localLoader;
        }

        public void SetParameters(Type type, string pluginName,
            PluginParameterInfo[] arguments, Dictionary<string, object> values)
        {
            var j = from a in arguments.SelectMany(n => n.Inputs)
                join v in values on a.Name equals v.Key
                select new { Parameter = a, Value = v.Value };
            var p = context.WebPlugins.FirstOrDefault(n =>
                n.UniqueName == pluginName && n.Tenant != null &&
                n.Tenant.TenantName == permissionScope.PermissionPrefix);
            Dictionary<string, object> formatHint = new Dictionary<string, object>();
            foreach (var item in j)
            {
                var vl = item.Value;
                if (vl is string s && item.Parameter.EncryptValue)
                {
                    var tmp = security.Encrypt(s, permissionScope.PermissionPrefix);
                    vl = $"[{item.Parameter.Name}:decrypt]";
                    SetVar(item.Parameter.Name, tmp);
                }

                formatHint[item.Parameter.Name] = vl;
            }


            var paramString = string.Join(",",
                from arg in arguments select formatHint.FormatText(arg.ValueOutputFormat));
            var mustEnable = (bool)TypeConverter.Convert(values[$"{pluginName}_EnableModule"], typeof(bool));
            var plug = context.WebPlugins.FirstOrDefault(n =>
                n.UniqueName == pluginName && n.Tenant != null &&
                n.Tenant.TenantName == permissionScope.PermissionPrefix);
            var currentEnabled = plug != null;
            if (mustEnable && !currentEnabled)
            {
                plug = new WebPlugin
                {
                    TenantId = context.CurrentTenantId,
                    UniqueName = pluginName
                };

                context.WebPlugins.Add(plug);
            }

            var save = false;
            if (mustEnable)
            {
                var description = localLoader.Instance.DescribeType(type);
                if (type.IsGenericTypeDefinition)
                {
                    throw new InvalidOperationException("Generic types are not supported with this component.");
                }

                var targetConstructor = description.Constructors.FirstOrDefault(n =>
                    n.Parameters.Length == arguments.Length &&
                    (from t in n.Parameters join a in arguments on t.ParameterName equals a.ParameterName select a)
                    .Count() == arguments.Length);
                if (targetConstructor == null)
                {
                    throw new InvalidOperationException("No constructor found that matches the given settings");
                }

                var typeIdentifier = targetConstructor.Sample.Substring(0, targetConstructor.Sample.IndexOf(">") + 1);
                plug.Constructor = $"{typeIdentifier}{paramString}";
                save = true;
            }
            else if (currentEnabled)
            {
                context.WebPlugins.Remove(plug);
                save = true;
            }

            if (save)
            {
                context.SaveChanges();
            }
        }

        public ModuleParameterTemplate[] GetParameters(Type type, PluginParameterInfo[] arguments)
        {
            arguments ??= Array.Empty<PluginParameterInfo>(); 
            var description = localLoader.Instance.DescribeType(type);
            if (type.IsGenericTypeDefinition)
            {
                throw new InvalidOperationException("Generic types are not supported with this component.");
            }

            var targetConstructor = description.Constructors.FirstOrDefault(n =>
                n.Parameters.Length == arguments.Length &&
                (from t in n.Parameters join a in arguments on t.ParameterName equals a.ParameterName select a)
                .Count() == arguments.Length);
            if (targetConstructor == null)
            {
                throw new InvalidOperationException("No constructor found that matches the given settings");
            }

            
            return (from t in arguments.SelectMany(n => n.Inputs??Array.Empty<ModuleParameterTemplate>())
                select new ModuleParameterTemplate()
                {
                    Name = t.Name,
                    DisplayName = t.DisplayName,
                    EditorType = t.EditorType,
                    CustomConfig = t.CustomConfig
                }).ToArray();
        }

        public object GetConfig(string pluginName,
            PluginParameterInfo[] arguments = null)
        {
            Dictionary<string, object> retVal = new Dictionary<string, object>
            {
                { $"{pluginName}_EnableModule", false }
            };
            arguments ??= Array.Empty<PluginParameterInfo>();
            var p = context.WebPlugins.FirstOrDefault(n =>
                n.UniqueName == pluginName && n.Tenant != null &&
                n.Tenant.TenantName == permissionScope.PermissionPrefix);
            
            if (p != null)
            {
                var result = PluginConstructorParser.ParsePluginString(p.Constructor, null);
                if (result.Parameters.Length == arguments.Length)
                {
                    retVal[$"{pluginName}_EnableModule"]= true;
                    for (int i = 0; i < result.Parameters.Length; i++)
                    {
                        var arg = arguments[i];
                        var param = result.Parameters[i];
                        if (arg.Inputs.Any() && !string.IsNullOrEmpty(arg.ValueInputFormat))
                        {
                            var rx = new Regex(arg.ValueInputFormat, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant);
                            var m = rx.Match(param.ToString());
                            foreach (var inp in arg.Inputs)
                            {
                                if (m.Groups.ContainsKey(inp.Name))
                                {
                                    if (!inp.EncryptValue)
                                    {
                                        retVal[inp.Name] = m.Groups[inp.Name].Value;
                                    }
                                    else
                                    {
                                        retVal[inp.Name] = new string(' ', m.Groups[inp.Name].Value.Length);
                                    }
                                }
                                else
                                {
                                    retVal[inp.Name] = null;
                                }
                            }
                        }
                    }
                }
            }

            return retVal;
        }

        private void SetVar(string parameterName, string value)
        {
            var constantEntity = context.WebPluginConstants.FirstOrDefault(n =>
                n.Name == parameterName && n.TenantId == context.CurrentTenantId);
            if (constantEntity == null)
            {
                constantEntity = new WebPluginConstant
                {
                    Name = parameterName,
                    TenantId = context.CurrentTenantId
                };
                context.WebPluginConstants.Add(constantEntity);
            }

            constantEntity.Value = value;
            context.SaveChanges();
        }
    }
}
