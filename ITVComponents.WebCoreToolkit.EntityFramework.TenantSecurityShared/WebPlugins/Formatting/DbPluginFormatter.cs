using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Formatting;
using ITVComponents.Plugins.Initialization;
using ITVComponents.Security;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.WebPlugins.Formatting
{
    public class DbPluginFormatter:IStringFormatProvider
    {
        private Dictionary<string, object> formatPrototype = new Dictionary<string, object>();

        private Dictionary<string, bool> publicPrototypeIndicators = new Dictionary<string, bool>();

        private string encryptedPassword;

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Initializes a new instance of the DbPluginFormatter class
        /// </summary>
        /// <param name="context">the database containing formatting-hints</param>
        public DbPluginFormatter(IBaseTenantContext context)
        {
            context.WebPluginConstants.ForEach(n =>
            {
                formatPrototype.Add(n.Name, n.Value);
                publicPrototypeIndicators.Add(n.Name, n.TenantId == null);
            });

            int? tenantId;
            if ((tenantId  = context.CurrentTenantId) != null)
            {
                var t = context.Tenants.First(n => n.TenantId == tenantId);
                if (!string.IsNullOrEmpty(t.TenantPassword))
                {
                    encryptedPassword = t.TenantPassword.Encrypt();
                }
            }
        }

        /// <summary>
        /// Processes a raw-string and uses it as format-string of the configured const-collection
        /// </summary>
        /// <param name="rawString">the raw-string that was read from a plugin-configuration string</param>
        /// <returns>the format-result of the raw-string</returns>
        public string ProcessLiteral(string rawString)
        {
            return formatPrototype.FormatText(rawString, EncryptSupport);
        }

        private object EncryptSupport(string constName, string formatterName, string argumentName)
        {
            switch (formatterName)
            {
                case "decrypt":
                    if (!string.IsNullOrEmpty(encryptedPassword))
                    {
                        bool isPublic = false;
                        if (formatPrototype.ContainsKey(constName))
                        {
                            isPublic = publicPrototypeIndicators[constName];
                        }

                        switch (argumentName)
                        {
                            case "password":
                                return !isPublic ? encryptedPassword.Decrypt() : null;
                            case "useRawKey":
                                return !isPublic;
                            default:
                                return null;
                        }
                    }

                    break;
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
