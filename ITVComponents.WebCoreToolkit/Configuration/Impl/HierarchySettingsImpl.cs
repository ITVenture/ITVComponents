using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Configuration.Impl
{
    internal class HierarchySettingsImpl<TSettings>:IHierarchySettings<TSettings> where TSettings:class, new()
    {
        private readonly IScopedSettings<TSettings> scoped;
        private readonly IGlobalSettings<TSettings> global;
        private TSettings valueOrDefault;
        private TSettings value;
        private HierarchyScope scope = HierarchyScope.None;

        public HierarchySettingsImpl(IScopedSettings<TSettings> scoped, IGlobalSettings<TSettings> global)
        {
            this.scoped = scoped;
            this.global = global;
        }

        public TSettings Value => value ??= ValueOrDefault ?? new TSettings();
        public TSettings ValueOrDefault
        {
            get
            {
                var retVal = valueOrDefault;
                if (retVal == null)
                {
                    retVal = scoped?.ValueOrDefault;
                    if (retVal != null)
                    {
                        scope = HierarchyScope.Scoped;
                    }

                }

                if (retVal == null)
                {
                    retVal = global?.ValueOrDefault;
                    if (retVal != null)
                    {
                        scope = HierarchyScope.Global;
                    }
                }

                return valueOrDefault = retVal;
            }
        }

        public TSettings GetValue(string explicitSettingName)
        {
            return GetValueOrDefault(explicitSettingName) ?? new TSettings();
        }

        public TSettings GetValueOrDefault(string explicitSettingName)
        {
            return ResolveSetting(explicitSettingName, out _);
        }

        public HierarchyScope GetSettingScope(string explicitSettingName)
        {
            ResolveSetting(explicitSettingName, out var ret);
            return ret;
        }

        private TSettings ResolveSetting(string explicitSettingName, out HierarchyScope explicitScope)
        {
            TSettings retVal = default;
            explicitScope = HierarchyScope.None;
            retVal = scoped?.GetValueOrDefault(explicitSettingName);
            if (retVal != null)
            {
                explicitScope = HierarchyScope.Scoped;
            }

            if (retVal == null)
            {
                retVal = global?.GetValueOrDefault(explicitSettingName);
                if (retVal != null)
                {
                    explicitScope = HierarchyScope.Global;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Gets a value indicating whether this setting was loaded from global or from scope
        /// </summary>
        public HierarchyScope SettingScope => scope;
    }
}
