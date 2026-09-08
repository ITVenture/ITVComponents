using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Configuration.Impl
{
    internal class HierarchySettingsImpl<TSettings>:IHierarchySettings<TSettings> where TSettings:class, new()
    {
        private readonly IScopedSettings<TSettings> scoped;
        private readonly IGlobalSettings<TSettings> global;
        private readonly ILogger<HierarchySettingsImpl<TSettings>> logger;
        private TSettings valueOrDefault;
        private TSettings value;
        private HierarchyScope scope = HierarchyScope.None;

        public HierarchySettingsImpl(IScopedSettings<TSettings> scoped, IGlobalSettings<TSettings> global, ILogger<HierarchySettingsImpl<TSettings>> logger)
        {
            this.scoped = scoped;
            this.global = global;
            this.logger = logger;
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

                // Value hands out a fresh default when nothing was found, which is indistinguishable from a
                // setting that IS configured and simply says false everywhere. A feature that stays switched
                // off then looks like a decision instead of a missing row, and the only way to tell them apart
                // is to ask the database by hand. So the outcome gets written down once, here, where both
                // providers have already had their turn.
                if (retVal == null)
                {
                    logger?.LogDebug(
                        "Settings '{SettingsKey}' ({SettingsType}) were found on neither the scoped ({ScopedProvider}) nor the global ({GlobalProvider}) provider. Callers asking for Value will get a default instance.",
                        SettingsKeyOf(), typeof(TSettings).FullName,
                        scoped?.GetType().FullName ?? "<not registered>",
                        global?.GetType().FullName ?? "<not registered>");
                }
                else
                {
                    logger?.LogDebug("Settings '{SettingsKey}' resolved from {Scope}.", SettingsKeyOf(), scope);
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
        /// The key these settings are stored under - the type name unless a <see cref="SettingNameAttribute"/>
        /// says otherwise. Only used for log messages; the providers derive it the same way for themselves.
        /// </summary>
        private static string SettingsKeyOf()
        {
            var att = (SettingNameAttribute)Attribute.GetCustomAttribute(typeof(TSettings), typeof(SettingNameAttribute), true);
            return att?.SettingsKeyName ?? typeof(TSettings).Name;
        }

        /// <summary>
        /// Gets a value indicating whether this setting was loaded from global or from scope
        /// </summary>
        public HierarchyScope SettingScope => scope;
    }
}
