using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Extensions;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.Security.PermissionFlagging
{
    public class PermissionDeterminationDictionary
    {
        private readonly ITrustfulComponent trustfulComponent;
        private readonly Func<string, IReadOnlyDictionary<string, string[]>> getPermissions;
        private readonly IContextUserProvider userProvider;
        private Dictionary<string, bool> values = new Dictionary<string, bool>();
        private Dictionary<string, IReadOnlyDictionary<string, string[]>> permissionBuffer = new Dictionary<string, IReadOnlyDictionary<string, string[]>>();

        private IReadOnlyDictionary<string, string[]> Permissions(string permissionCategory) =>
            permissionBuffer.GetOrInsert(permissionCategory, c => getPermissions?.Invoke(c));
        public PermissionDeterminationDictionary(Func<string, IReadOnlyDictionary<string, string[]>> getPermissions, IContextUserProvider userProvider)
        {
            this.getPermissions = getPermissions;
            this.userProvider = userProvider;
        }


        public PermissionDeterminationDictionary(Func<string, IReadOnlyDictionary<string, string[]>> getPermissions,
            IContextUserProvider userProvider, ITrustfulComponent trustfulComponent) : this(getPermissions,
            userProvider)
        {
            this.trustfulComponent = trustfulComponent;
        }

        public bool this[string category, string key]
        {
            get
            {
                var compKey = $"{category}#{key}";
                var trustByC = trustfulComponent != null && trustfulComponent.IsComponentSecureFor(compKey);
                var t = trustByC || values.TryGetValue(compKey, out var v) && v;
                return t;
            }
            set
            {
                var compKey = $"{category}#{key}";
                if (value != this[category, key])
                {
                    if (value && !CanDeactivateFilter(category, key))
                    {
                        values[compKey] = false;
                    }
                    else
                    {
                        values[compKey] = value;
                    }
                }
            }
        }

        public bool this[(string category, string key)[] keys]
        {
            get
            {
                var trustByC = keys.Any(key =>
                    trustfulComponent != null && trustfulComponent.IsComponentSecureFor($"{key.category}#{key.key}"));
                var t = trustByC || keys.Any(key => values.TryGetValue($"{key.category}#{key.key}", out var v) && v);
                return t;
            }
        }

        public string[] PermissionsOnTopics(string category, string[] topicKeys, out bool block)
        {
            var retVal = Array.Empty<string>();
            block = true;
            if (Permissions != null)
            {
                retVal = (from t in topicKeys
                    join p in Permissions(category) on t equals p.Key
                    select p.Value).SelectMany(n => n).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                block = retVal.Length == 0;
            }

            return retVal;
        }

        private bool CanDeactivateFilter(string category, string key)
        {
            var value = false;
            var prm = Permissions(category);
            if (prm != null && userProvider != null)
            {
                if (prm.TryGetValue(key, out var directPerms))
                {
                    value = userProvider.Services.VerifyUserPermissions(directPerms);
                }
                else if (prm.TryGetValue("Default", out var defPerms))
                {
                    value = userProvider.Services.VerifyUserPermissions(defPerms);
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
