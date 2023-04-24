using System;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.Configuration.Impl
{
    internal class ScopedSettingsImpl<TSettings>:IScopedSettings<TSettings> where TSettings:class,new()
    {
        /// <summary>
        /// the underlaying settings-provider
        /// </summary>
        private readonly IScopedSettingsProvider settingsProvider;

        private readonly ISecurityRepository securityRepo;
        private readonly IPermissionScope permissionScope;

        /// <summary>
        /// the name of the setting represented by this instance
        /// </summary>
        private readonly string typeName;

        /// <summary>
        /// the configured value
        /// </summary>
        private TSettings value;

        private TSettings valueOrDefault;

        /// <summary>
        /// Injector Constructor for this scoped settings
        /// </summary>
        /// <param name="settingsProvider"></param>
        public ScopedSettingsImpl(IScopedSettingsProvider settingsProvider, ISecurityRepository securityRepo, IPermissionScope permissionScope)
        {
            this.settingsProvider = settingsProvider;
            this.securityRepo = securityRepo;
            this.permissionScope = permissionScope;
            typeName = typeof(TSettings).Name;
            var att = (SettingNameAttribute)Attribute.GetCustomAttribute(typeof(TSettings), typeof(SettingNameAttribute), true);
            if (att != null)
            {
                typeName = att.SettingsKeyName;
            }
        }

        /// <summary>
        /// Gets the deserialized Settings-value. If it is not configured, an object is constructed, using the Default-Constructor.
        /// </summary>
        public TSettings Value => value ??= ValueOrDefault??new TSettings();

        /// <summary>
        /// Gets the deserialized Settings-value. If it is not configured, null is returned (-> default(TSettings)).
        /// </summary>
        public TSettings ValueOrDefault => valueOrDefault ??= GetSettingsValue(null);

        public TSettings GetValue(string explicitSettingName)
        {
            return GetValueOrDefault(explicitSettingName) ?? new TSettings();
        }

        public TSettings GetValueOrDefault(string explicitSettingName)
        {
            return GetSettingsValue(explicitSettingName);
        }

        /// <summary>
        /// Sets the value of this settings item
        /// </summary>
        /// <param name="newValue">the value to write for the setting represented by this object</param>
        public void Update(TSettings newValue)
        {
            value = null;
            valueOrDefault = null;
            var tmp = JsonHelper.ToJson(newValue);
            settingsProvider.UpdateJsonSetting(typeName, permissionScope.PermissionPrefix, tmp);
        }

        /// <summary>
        /// Sets the value of this settings item and encrypts string values that are prefixed with "encrypt:"
        /// </summary>
        /// <param name="newValue">the value to write for the setting represented by this object</param>
        /// <param name="useTenantEncryption">indicates whether to use tenant-driven encryption for writing the settings</param>
        public void Update(TSettings newValue, bool useTenantEncryption)
        {
            value = null;
            valueOrDefault = null;
            var currentScope = permissionScope.PermissionPrefix;
            var tmp = securityRepo.EncryptJsonObject(newValue, currentScope);
            settingsProvider.UpdateJsonSetting(typeName, currentScope, tmp);
        }

        public void Update(string explicitSettingName, TSettings newValue)
        {
            var tmp = JsonHelper.ToJson(newValue);
            settingsProvider.UpdateJsonSetting(explicitSettingName, permissionScope.PermissionPrefix, tmp);
        }

        public void Update(string explicitSettingName, TSettings newValue, bool useTenantEncryption)
        {
            var currentScope = permissionScope.PermissionPrefix;
            var tmp = securityRepo.EncryptJsonObject(newValue, currentScope);
            settingsProvider.UpdateJsonSetting(explicitSettingName, currentScope, tmp);
        }

        /// <summary>
        /// Reads the settings-value from the underlaying provider
        /// </summary>
        /// <returns>the configured settings-instance or its default-value</returns>
        private TSettings GetSettingsValue(string? explicitSettingName)
        {
            var tmp = settingsProvider.GetJsonSetting(explicitSettingName??typeName);
            if (!string.IsNullOrEmpty(tmp))
            {
                var retVal = JsonHelper.FromJsonString<TSettings>(tmp);
                return retVal;
            }

            return null;
        }
    }
}
