using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.DIServices;
using ITVComponents.Formatting;
using ITVComponents.Formatting.Extensions;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.HelperModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.HelperModels.Comparers;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.WebPlugins;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.WebPlugins.Formatting
{
    public class DbPluginFormatter<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : StringFormatProvider, IDeferredInit
        where TTenant : Tenant 
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>

    {
        private readonly IToolkitContextFactory contextFactory;
        private readonly IWebPluginsSelector plugInSelector;
        private readonly IObjectProvider objectProvider;
        private Dictionary<string, object> formatPrototype = new Dictionary<string, object>();

        private Dictionary<string, WebPluginConstant> metadata;

        //private int? tenantId;

        /// <summary>
        /// Initializes a new instance of the DbPluginFormatter class
        /// </summary>
        /// <param name="context">the database containing formatting-hints</param>
        public DbPluginFormatter(
            IToolkitContextFactory contextFactory,
            IWebPluginsSelector plugInSelector) : this(contextFactory, plugInSelector, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the DbPluginFormatter class
        /// </summary>
        /// <param name="contextFactory">factory yielding a fresh per-operation context containing formatting-hints</param>
        public DbPluginFormatter(IToolkitContextFactory contextFactory, IWebPluginsSelector plugInSelector, IObjectProvider objectProvider)
        {
            this.contextFactory = contextFactory;
            this.plugInSelector = plugInSelector;
            this.objectProvider = objectProvider;
        }

        /// <summary>
        /// Leases a fresh per-operation context for the duration of a single operation.
        /// </summary>
        private IContextLease<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> LeaseDb()
            => contextFactory.Lease<IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();

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
            if (metadata.TryGetValue(constName, out var meta))
            {
                switch (formatterName)
                {
                    case "decrypt":
                        if (!meta.IsGlobal && !string.IsNullOrEmpty(meta.DecryptKey))
                        {
                            switch (argumentName)
                            {
                                case "password":
                                    return meta.DecryptKey.Decrypt();
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
            using var lease = LeaseDb();
            var context = lease.Context;
            var fx = (string k) =>
            {
                var dic = new Dictionary<string, WebPluginConstant>();
                int? tenantId = null;
                if (context.FilterAvailable && !context.ShowAllTenants)
                {
                    string encryptedPassword = null;
                    if ((tenantId = context.CurrentTenantId) != null)
                    {
                        var t = context.Tenants.First(n => n.TenantId == tenantId);
                        if (!string.IsNullOrEmpty(t.TenantPassword))
                        {
                            encryptedPassword = t.TenantPassword.Encrypt();
                        }
                    }

                    context.WebPluginConstants.Where(n => n.TenantId != null).Select(n => new WebPluginConstant
                    {
                        Value = n.Value,
                        IsGlobal = false,
                        Name = n.Name
                    }).AsEnumerable().Union(context.WebPluginConstants.Where(n => n.TenantId == null).Select(n =>
                        new WebPluginConstant
                        {
                            Value = n.Value,
                            IsGlobal = true,
                            Name = n.Name
                        }).AsEnumerable(), new WebPluginConstantComparer()).ForEach(n =>
                        {
                            if (!n.IsGlobal && !string.IsNullOrEmpty(encryptedPassword))
                            {
                                n.DecryptKey = encryptedPassword;
                            }

                            dic.Add(n.Name, n);
                        });
                }
                else if (!string.IsNullOrEmpty(plugInSelector.ExplicitPluginPermissionScope))
                {
                    var tenant =
                        context.Tenants.First(n => n.TenantName == plugInSelector.ExplicitPluginPermissionScope);
                    string encryptedPassword = null;
                    if (!string.IsNullOrEmpty(tenant.TenantPassword))
                    {
                        encryptedPassword = tenant.TenantPassword.Encrypt();
                    }

                    tenantId = tenant.TenantId;
                    context.WebPluginConstants.Where(n => n.TenantId == tenantId).Select(n => new WebPluginConstant
                    {
                        Value = n.Value,
                        IsGlobal = false,
                        Name = n.Name
                    }).AsEnumerable().Union(context.WebPluginConstants.Where(n => n.TenantId == null).Select(n =>
                        new WebPluginConstant
                        {
                            Value = n.Value,
                            IsGlobal = true,
                            Name = n.Name
                        }).AsEnumerable(), new WebPluginConstantComparer()).ForEach(n =>
                        {
                            if (!n.IsGlobal && !string.IsNullOrEmpty(encryptedPassword))
                            {
                                n.DecryptKey = encryptedPassword;
                            }

                            dic.Add(n.Name, n);
                        });

                }
                else
                {
                    context.WebPluginConstants.Where(n => n.TenantId == null)
                        .ForEach(n =>
                        {
                            dic.Add(n.Name,
                                new WebPluginConstant
                                { Name = n.Name, DecryptKey = null, IsGlobal = true, Value = n.Value });
                        });
                }

                return dic;
            };

            if (objectProvider == null)
            {
                metadata = fx(null);
            }
            else
            {
                metadata = objectProvider.GetBufferedValue($"pifConstants#{UniqueName}", fx, null);
            }

            metadata.ForEach(n => formatPrototype.Add(n.Value.Name, n.Value.Value));
            Initialized = true;
        }
    }
}
