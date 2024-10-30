using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Formatting;
using ITVComponents.Formatting.Extensions;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.HelperModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.HelperModels.Comparers;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.WebPlugins;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.WebPlugins.Formatting
{
    public class DbPluginFormatter<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> :IStringFormatProvider 
        where TTenant : Tenant 
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>

    {
        private Dictionary<string, object> formatPrototype = new Dictionary<string, object>();

        private Dictionary<string, WebPluginConstant> metadata = new Dictionary<string, WebPluginConstant>();

        //private int? tenantId;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Initializes a new instance of the DbPluginFormatter class
        /// </summary>
        /// <param name="context">the database containing formatting-hints</param>
        public DbPluginFormatter(IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> context, IWebPluginsSelector plugInSelector)
        {
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
                    IsGlobal=false,
                    Name = n.Name
                }).AsEnumerable().Union(context.WebPluginConstants.Where(n => n.TenantId == null).Select(n => new WebPluginConstant
                {
                    Value = n.Value,
                    IsGlobal = true,
                    Name=n.Name
                }).AsEnumerable(), new WebPluginConstantComparer()).ForEach(n =>
                {
                    if (!n.IsGlobal && !string.IsNullOrEmpty(encryptedPassword))
                    {
                        n.DecryptKey = encryptedPassword;
                    }

                    formatPrototype.Add(n.Name, n.Value);
                    metadata.Add(n.Name, n);
                });
            }
            else if (!string.IsNullOrEmpty(plugInSelector.ExplicitPluginPermissionScope))
            {
                var tenant = context.Tenants.First(n => n.TenantName == plugInSelector.ExplicitPluginPermissionScope);
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
                }).AsEnumerable().Union(context.WebPluginConstants.Where(n => n.TenantId == null).Select(n => new WebPluginConstant
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

                    formatPrototype.Add(n.Name, n.Value);
                    metadata.Add(n.Name, n);
                });

            }
            else
            {
                context.WebPluginConstants.Where(n => n.TenantId == null)
                    .ForEach(n =>
                    {
                        formatPrototype.Add(n.Name, n.Value);
                        metadata.Add(n.Name, new WebPluginConstant{Name = n.Name, DecryptKey = null, IsGlobal = true, Value = n.Value});
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
