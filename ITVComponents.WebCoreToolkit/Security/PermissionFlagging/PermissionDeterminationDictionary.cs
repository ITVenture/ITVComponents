using ITVComponents.WebCoreToolkit.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.PermissionFlagging
{
    public class PermissionDeterminationDictionary
    {
        private readonly Func<IReadOnlyDictionary<string, string>> getPermissions;
        private readonly IContextUserProvider userProvider;
        private IReadOnlyDictionary<string, string> permissions;
        private Dictionary<string, bool> values = new Dictionary<string, bool>();

        private IReadOnlyDictionary<string, string> Permissions => permissions ??= getPermissions?.Invoke();
        public PermissionDeterminationDictionary(Func<IReadOnlyDictionary<string, string>> getPermissions, IContextUserProvider userProvider)
        {
            this.getPermissions = getPermissions;
            this.userProvider = userProvider;
        }

        public bool this[string key]
        {
            get
            {
                var t = values.TryGetValue(key, out var v);
                if (t)
                {
                    return v;
                }

                return false;
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

        private bool CanDeactivateFilter(string key)
        {
            var value = false;
            if (Permissions != null && userProvider != null)
            {
                if (Permissions.ContainsKey(key))
                {
                    value = userProvider.Services.VerifyUserPermissions(new[] { permissions[key] });
                }
                else if (permissions.ContainsKey("Default"))
                {
                    value = userProvider.Services.VerifyUserPermissions(new[] { permissions["Default"] });
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
