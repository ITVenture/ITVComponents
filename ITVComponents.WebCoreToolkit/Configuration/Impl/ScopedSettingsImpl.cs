using System;
using ITVComponents.Json;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<ScopedSettingsImpl<TSettings>> logger;

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
        /// <param name="settingsProvider">the provider that reads the raw setting for the current scope</param>
        /// <param name="securityRepo">the security repository used for tenant-driven encryption on write</param>
        /// <param name="permissionScope">the scope a written setting is attributed to</param>
        /// <param name="logger">the logger that records whether a setting was found and usable</param>
        public ScopedSettingsImpl(IScopedSettingsProvider settingsProvider, ISecurityRepository securityRepo, IPermissionScope permissionScope, ILogger<ScopedSettingsImpl<TSettings>> logger)
        {
            this.settingsProvider = settingsProvider;
            this.securityRepo = securityRepo;
            this.permissionScope = permissionScope;
            this.logger = logger;
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
            var tmp = JsonHelper.ToJson(newValue, SerializationTypingMode.StaticTyping, null);
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
            var tmp = JsonHelper.ToJson(newValue, SerializationTypingMode.StaticTyping, null);
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
            // Three outcomes that all end up as the same null for the caller: nothing stored under the key,
            // something stored that deserializes to nothing, and a value that came back fine. Only the last is
            // ordinary, so say which one it was - a settings-driven feature that stays switched off gives no
            // other clue, and the caller cannot tell "not configured" from "configured to false".
            var key = explicitSettingName ?? typeName;
            var tmp = settingsProvider.GetJsonSetting(key);
            if (!string.IsNullOrEmpty(tmp))
            {
                var retVal = JsonHelper.FromJsonString<TSettings>(tmp, SerializationTypingMode.StaticTyping);
                if (retVal == null)
                {
                    logger?.LogWarning(
                        "Scoped setting '{SettingsKey}' was found ({Length} characters) but deserialized to nothing. The stored value does not match {SettingsType}.",
                        key, tmp.Length, typeof(TSettings).FullName);
                }
                else
                {
                    logger?.LogDebug("Scoped setting '{SettingsKey}' resolved from the scoped provider ({Length} characters).", key, tmp.Length);
                }

                return retVal;
            }

            // FullName, not Name: the flat and the tree-capable settings provider are both called
            // TenantSettingsProvider and both take eleven type arguments, so the short name says nothing about
            // which of the two answered - and they answer very differently.
            logger?.LogDebug("Scoped setting '{SettingsKey}' is not configured on the scoped provider ({Provider}).",
                key, settingsProvider?.GetType().FullName ?? "<none>");
            return null;
        }
    }
}
