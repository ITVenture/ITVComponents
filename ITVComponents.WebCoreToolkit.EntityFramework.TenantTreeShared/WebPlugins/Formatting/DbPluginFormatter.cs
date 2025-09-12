using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DIServices;
using ITVComponents.Extensions;
using ITVComponents.Formatting;
using ITVComponents.Formatting.Extensions;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.HelperModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.HelperModels.Comparers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.WebPlugins;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.WebPlugins.Formatting
{
    public class DbPluginFormatter<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig> : StringFormatProvider, IDeferredInit
        where TTenant : HierarchyTenant 
        where TWebPlugin : HierarchyWebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : HierarchyWebPluginConstant<TTenant>
        where TWebPluginGenericParameter : HierarchyWebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : HierarchyTenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TTrustConfig : HierarchyTenantContextSecurityTrustConfig<TTrustConfig>, new()

    {
        private readonly IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig> context;
        private readonly IWebPluginsSelector plugInSelector;
        private readonly IObjectProvider objectCache;
        private Dictionary<string, object> formatPrototype = new Dictionary<string, object>();

        private Dictionary<string, WebPluginConstant> metadata;


        /// <summary>
        /// Initializes a new instance of the DbPluginFormatter class
        /// </summary>
        /// <param name="context">the database containing formatting-hints</param>
        public DbPluginFormatter(
            IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig> context, IWebPluginsSelector plugInSelector) : this(context, plugInSelector, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DbPluginFormatter class
        /// </summary>
        /// <param name="context">the database containing formatting-hints</param>
        public DbPluginFormatter(IHierarchyTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig> context, IWebPluginsSelector plugInSelector, IObjectProvider objectCache)
        {
            this.context = context;
            this.plugInSelector = plugInSelector;
            this.objectCache = objectCache;
        }

        protected override string FormatStringInternal(string rawString, Dictionary<string, object> customStringFormatArguments)
        {
            return customStringFormatArguments.FormatText(rawString, EncryptSupport, TextFormat.DefaultFormatPolicyWithPrimitives);
        }


        protected override void FillDictionary(IDictionary<string, object> values)
        {
            formatPrototype.ForEach(values.Add);
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

        public bool Initialized { get; private set; }
        public bool ForceImmediateInitialization => true;
        public void Initialize()
        {
            var fx = (string k) =>
            {
                var dic = new Dictionary<string, WebPluginConstant>();
                if (context.FilterAvailable && !context.ShowAllTenants)
                {
                    using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(context, context,
                        new TTrustConfig { HideGlobals = false, IncludeParentTree = true, ShowAllTenants = false });
                    Dictionary<string, string> tenantPass = new Dictionary<string, string>();
                    (from rprot in (from c in context.UpwardsTenantTreeView
                                    join m in context.WebPluginConstants on c.ParentTenantId equals m.TenantId
                                    where c.ParentLevel == 1 || m.Inheritable
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
                            dic.Add(lop.Name, lop);
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
                            dic.Add(lop.Name, lop);
                        });
                }
                else
                {
                    context.WebPluginConstants.Where(n => n.TenantId == null)
                        .ForEach(n =>
                        {
                            dic.Add(n.Name, new WebPluginConstant { Value = n.Value, Name = n.Name, IsGlobal = true });
                        });
                }

                return dic;
            };

            if (objectCache == null)
            {
                metadata = fx(null);
            }
            else
            {
                metadata = objectCache.GetBufferedValue($"pifConstants#{UniqueName}", fx, null);
            }

            metadata.ForEach(n => formatPrototype.Add(n.Value.Name, n.Value.Value));
            Initialized = true;
        }
    }
}
