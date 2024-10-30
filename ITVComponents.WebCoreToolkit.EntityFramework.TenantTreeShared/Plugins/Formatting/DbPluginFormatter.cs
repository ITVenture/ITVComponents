using System;
using System.Collections.Generic;
using System.Linq;
using Castle.DynamicProxy;
using ITVComponents.DataAccess;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Extensions;
using ITVComponents.Formatting;
using ITVComponents.Formatting.Extensions;
using ITVComponents.Helpers;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.HelperModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.HelperModels.Comparers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.HelperModels;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Plugins.Formatting
{
    public class DbPluginFormatter<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> :IStringFormatProvider 
        where TTenant : HierarchyTenant 
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>

    {
        private Dictionary<string, object> formatPrototype = new Dictionary<string, object>();

        private Dictionary<string, WebPluginConstant> metadata = new Dictionary<string, WebPluginConstant>();

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Initializes a new instance of the DbPluginFormatter class
        /// </summary>
        /// <param name="context">the database containing formatting-hints</param>
        public DbPluginFormatter(IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> context, IWebPluginsSelector plugInSelector)
        {
            if (context.FilterAvailable && !context.ShowAllTenants)
            {
                Dictionary<string, string> tenantPass = new Dictionary<string, string>();
                (from rprot in (from c in context.UpwardsTenantTreeView
                    join m in context.WebPluginConstants on c.ParentTenantId equals m.TenantId
                    select new { c.ParentTenantId, c.ParentLevel, m.Name, m.WebPluginConstantId }
                    into gprot
                    group gprot by gprot.Name
                    into g1
                    select new { Name = g1.Key, Level = g1.Min(m => m.ParentLevel), All = g1.ToArray() })
                    .Select(item => item.All.First(n => n.ParentLevel == item.Level))
                    join prc in context.WebPluginConstants on rprot.WebPluginConstantId equals prc.WebPluginConstantId
                    join tn in context.Tenants on prc.TenantId equals tn.TenantId
                            select new WebPluginConstant{Value = prc.Value, IsGlobal = false,Name = prc.Name, DecryptKey = tenantPass.GetOrInsert(tn.TenantName, n=> tn.TenantPassword.Encrypt())})
                    .AsEnumerable()
                    .Union(context.WebPluginConstants.Where(n => n.TenantId == null).Select(cst => new WebPluginConstant{Value = cst.Value, IsGlobal = true, Name = cst.Name}).AsEnumerable(), new WebPluginConstantComparer())
                    .ForEach(lop =>
                    {
                        formatPrototype.Add(lop.Name, lop.Value);
                        metadata.Add(lop.Name, lop);
                    });
            }
            else if (!string.IsNullOrEmpty(plugInSelector.ExplicitPluginPermissionScope))
            {
                var tenantPass = new Dictionary<string, string>();
                (from rprot in (from c in context.UpwardsTenantTreeView.Where(n => n.OutermostLeafTenantName == plugInSelector.ExplicitPluginPermissionScope)
                                join m in context.WebPluginConstants on c.ParentTenantId equals m.TenantId
                                select new { c.ParentTenantId, c.ParentLevel, m.Name, m.WebPluginConstantId }
                                into gprot
                                group gprot by gprot.Name
                                into g1
                                select new { Name = g1.Key, Level = g1.Min(m => m.ParentLevel), All = g1.ToArray() })
                            .Select(item => item.All.First(n => n.ParentLevel == item.Level))
                        join prc in context.WebPluginConstants on rprot.WebPluginConstantId equals prc.WebPluginConstantId
                        join tn in context.Tenants on prc.TenantId equals tn.TenantId
                        select new WebPluginConstant { Value = prc.Value, IsGlobal = false, Name = prc.Name, DecryptKey = tenantPass.GetOrInsert(tn.TenantName, n => tn.TenantPassword.Encrypt()) })
                    .AsEnumerable()
                    .Union(context.WebPluginConstants.Where(n => n.TenantId == null).Select(cst => new WebPluginConstant { Value = cst.Value, IsGlobal = true, Name = cst.Name }).AsEnumerable(), new WebPluginConstantComparer())
                    .ForEach(lop =>
                    {
                        formatPrototype.Add(lop.Name, lop.Value);
                        metadata.Add(lop.Name, lop);
                    });
            }
            else
            {
                context.WebPluginConstants.Where(n => n.TenantId == null)
                    .ForEach(n =>
                    {
                        formatPrototype.Add(n.Name, n.Value);
                        metadata.Add(n.Name, new WebPluginConstant { Value = n.Value, Name = n.Name, IsGlobal = true });
                    });
            }
        }

        /// <summary>
        /// Processes a raw-string and uses it as format-string of the configured const-collection
        /// </summary>
        /// <param name="rawString">the raw-string that was read from a plugin-configuration string</param>
        /// <returns>the format-result of the raw-string</returns>
        public string ProcessLiteral(string rawString, Dictionary<string,object> customStringFormatArguments)
        {
            customStringFormatArguments ??= new();
            customStringFormatArguments = formatPrototype.ExtendDictionary(customStringFormatArguments);
            return customStringFormatArguments.FormatText(rawString, EncryptSupport, TextFormat.DefaultFormatPolicyWithPrimitives);
        }

        private object EncryptSupport(string constName, string formatterName, string argumentName)
        {
            if (metadata.TryGetValue(constName, out var cst))
            {
                switch (formatterName)
                {
                    case "decrypt":
                        if (!cst.IsGlobal && !string.IsNullOrEmpty(cst.DecryptKey))
                        {
                            switch (argumentName)
                            {
                                case "password":
                                    return cst.DecryptKey.Decrypt();
                                default:
                                    return null;
                            }
                        }

                        break;
                }
            }

            return null;
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
