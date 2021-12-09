using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security
{
    public abstract class PermissionScopeBase:IPermissionScope
    {
        private bool scopeIsFixed = false;

        private bool scopeIsExplicit = false;

        private string fixScope = null;

        public string PermissionPrefix
        {
            get
            {
                return GetPermissionPrefix();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the PermissionScope was set explicitly
        /// </summary>
        public bool IsScopeExplicit
        {
            get
            {
                var tmp = PermissionPrefix;
                return scopeIsExplicit || scopeIsFixed;
            }
            protected set => scopeIsExplicit = value;
        }

        /// <summary>
        /// Sets the permissionScope to a new value
        /// </summary>
        /// <param name="newScope">the new scope to apply for the current user</param>
        public void ChangeScope(string newScope)
        {
            if(scopeIsExplicit || scopeIsFixed)
            {
                throw new InvalidOperationException("Scope explicitly set!");
            }

            SetPermissionScopePrefix(newScope);
        }

        /// <summary>
        /// sets the scope to a fixed value when it is used outside a http-context
        /// </summary>
        /// <param name="fixedScope"></param>
        internal void SetFixedScope(string fixedScope)
        {
            scopeIsFixed = !string.IsNullOrEmpty(fixedScope);
            fixScope = fixedScope;
        }

        /// <summary>
        /// Enables a derived class to provide a permissionScopePrefix for the case that it is not being bypassed internally
        /// </summary>
        /// <returns>a string representing the current permission scope</returns>
        protected abstract string GetPermissionScopePrefix();

        /// <summary>
        /// Enables a derived class to set the value for the current scope in case that it can be changed by a caller
        /// </summary>
        /// <param name="newScope">the new scope value</param>
        protected abstract void SetPermissionScopePrefix(string newScope);

        private string GetPermissionPrefix()
        {
            if (!scopeIsFixed)
            {
                return GetPermissionScopePrefix();
            }

            return fixScope;
        }
    }
}
