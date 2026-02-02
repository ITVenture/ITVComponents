using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.Security.PermissionFlagging
{
    public class PermissionDeterminationDictionary
    {
        private readonly ITrustfulComponent trustfulComponent;
        private readonly Func<IReadOnlyDictionary<string, string[]>> getPermissions;
        private readonly IContextUserProvider userProvider;
        private IReadOnlyDictionary<string, string[]> permissions;
        private Dictionary<string, bool> values = new Dictionary<string, bool>();

        private IReadOnlyDictionary<string, string[]> Permissions => permissions ??= getPermissions?.Invoke();
        public PermissionDeterminationDictionary(Func<IReadOnlyDictionary<string, string[]>> getPermissions, IContextUserProvider userProvider)
        {
            this.getPermissions = getPermissions;
            this.userProvider = userProvider;
        }


        public PermissionDeterminationDictionary(Func<IReadOnlyDictionary<string, string[]>> getPermissions,
            IContextUserProvider userProvider, ITrustfulComponent trustfulComponent) : this(getPermissions,
            userProvider)
        {
            this.trustfulComponent = trustfulComponent;
        }

        public bool this[string key]
        {
            get
            {
                var trustByC = trustfulComponent != null && trustfulComponent.IsComponentSecureFor(key);
                var t = trustByC || values.TryGetValue(key, out var v) && v;
                return t;
            }
            set
            {
                if (value != this[key])
                {
                    if (value && !CanDeactivateFilter(key))
                    {
                        values[key] = false;
                    }
                    else
                    {
                        values[key] = value;
                    }
                }
            }
        }

        public bool this[string[] keys]
        {
            get
            {
                var trustByC = keys.Any(key =>
                    trustfulComponent != null && trustfulComponent.IsComponentSecureFor(key));
                var t = trustByC || keys.Any(key => values.TryGetValue(key, out var v) && v);
                return t;
            }
        }

        public string[] PermissionsOnTopics(string[] topicKeys, out bool block)
        {
            var retVal = Array.Empty<string>();
            block = true;
            if (Permissions != null)
            {
                retVal = (from t in topicKeys
                    join p in Permissions on t equals p.Key
                    select p.Value).SelectMany(n => n).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                block = retVal.Length == 0;
            }

            return retVal;
        }

        private bool CanDeactivateFilter(string key)
        {
            var value = false;
            if (Permissions != null && userProvider != null)
            {
                if (Permissions.ContainsKey(key))
                {
                    value = userProvider.Services.VerifyUserPermissions(permissions[key]);
                }
                else if (permissions.ContainsKey("Default"))
                {
                    value = userProvider.Services.VerifyUserPermissions(permissions["Default"]);
                }

                return value;
            }

            return true;
        }

        public void UnsafeAllow(string key, bool value)
        {
            values[key] = value;
        }
    }
}
