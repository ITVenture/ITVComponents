using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Config;

namespace ITVComponents.AgentServices
{
    /// <summary>
    /// Der Katalog des Agenten: bietet die Bereichs-Plugins seiner Factory als Geräte an, beschreibt ihre
    /// Einstellungen und legt sie lokal ab.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wird am Agenten als Plugin unter <see cref="AgentCatalog.DefaultObjectName"/> (oder dem Namen aus dem
    /// Template der ClientApp) geladen und über den Dienst-Verteiler bereitgestellt:
    /// <c>[ITVComponents.AgentServices.dll]&lt;ITVComponents.AgentServices.PluginAgentObjectCatalog&gt;$factory</c>
    /// — dafür muss <see cref="PluginFactory.AllowFactoryParameter"/> gesetzt sein.
    /// </para>
    /// <para>
    /// <b>Er schaltet zugleich <c>$DeviceSettings</c> frei:</b> solange er lebt, löst die Factory diesen
    /// Konstruktor-Parameter für jedes Gerät aus dessen lokaler Ablage auf.
    /// </para>
    /// <para>
    /// <b>Kein Aufruf baut ein Gerät.</b> Aufzählen und Beschreiben laufen über den Typ
    /// (<see cref="PluginFactory.GetScopedPluginTypes"/>); Auswahlwerte kommen von eigenen
    /// <see cref="IDeviceChoiceProvider"/>n.
    /// </para>
    /// </remarks>
    public sealed class PluginAgentObjectCatalog : IAgentObjectCatalog, IPlugin
    {
        private readonly PluginFactory factory;

        private readonly IDeviceSettingsStore store;

        /// <summary>Initialisiert den Katalog mit der Standard-Ablage (<see cref="NativeDeviceSettingsStore"/>).</summary>
        /// <param name="factory">die Factory, deren Bereichs-Plugins die Geräte sind</param>
        public PluginAgentObjectCatalog(PluginFactory factory) : this(factory, new NativeDeviceSettingsStore())
        {
        }

        /// <summary>Initialisiert den Katalog.</summary>
        /// <param name="factory">die Factory, deren Bereichs-Plugins die Geräte sind</param>
        /// <param name="store">die Ablage der Einstellungen</param>
        public PluginAgentObjectCatalog(PluginFactory factory, IDeviceSettingsStore store)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            factory.UnknownConstructorParameter += ProvideDeviceSettings;
        }

        /// <summary>Gets or sets the UniqueName of this Plugin.</summary>
        public string UniqueName { get; set; }

        /// <summary>Informs a calling class of a Disposal of this Instance.</summary>
        public event EventHandler Disposed;

        /// <inheritdoc />
        public Task<AgentObjectInfo[]> GetObjectsAsync(string contractTypeName)
        {
            if (string.IsNullOrEmpty(contractTypeName))
            {
                return Task.FromResult(Array.Empty<AgentObjectInfo>());
            }

            var retVal = factory.GetScopedPluginTypes(null)
                .Where(d => Fulfills(d.Type, contractTypeName))
                .Select(d => new AgentObjectInfo
                {
                    Name = d.Item.Name,
                    DisplayName = string.IsNullOrEmpty(d.Item.DisplayName) ? null : d.Item.DisplayName,
                    HasSettings = DeviceSettingsReflection.SettingsTypeOf(d.Type) != null
                })
                .ToArray();
            if (retVal.Length == 0)
            {
                LogEnvironment.LogDebugEvent($"No device on this agent fulfills '{contractTypeName}'.", LogSeverity.Report);
            }

            return Task.FromResult(retVal);
        }

        /// <inheritdoc />
        public Task<SettingDescriptor[]> DescribeSettingsAsync(string objectName)
        {
            var device = Device(objectName);
            if (device == null)
            {
                LogEnvironment.LogEvent($"Settings were requested for '{objectName}', which is no device on this agent.",
                    LogSeverity.Warning);
                return Task.FromResult(Array.Empty<SettingDescriptor>());
            }

            return Task.FromResult(DeviceSettingsReflection.Describe(DeviceSettingsReflection.SettingsTypeOf(device)));
        }

        /// <inheritdoc />
        public async Task<SettingChoice[]> GetChoicesAsync(string objectName, string settingName, string draftJson)
        {
            var device = Device(objectName);
            var field = DeviceSettingsReflection.Describe(DeviceSettingsReflection.SettingsTypeOf(device))
                .FirstOrDefault(f => string.Equals(f.Name, settingName, StringComparison.OrdinalIgnoreCase));
            if (field == null)
            {
                LogEnvironment.LogEvent($"Choices were requested for '{settingName}' of '{objectName}', which is no setting of a device on this agent.",
                    LogSeverity.Warning);
                return Array.Empty<SettingChoice>();
            }

            if (string.IsNullOrEmpty(field.ChoiceSource))
            {
                return field.Choices?.ToArray() ?? Array.Empty<SettingChoice>();
            }

            if (string.Equals(field.ChoiceSource, objectName, StringComparison.Ordinal))
            {
                // Das Geraet als seine eigene Auswahl-Quelle hiesse: eine Liste aufklappen baut das Geraet.
                LogEnvironment.LogEvent($"Device '{objectName}' names itself as the choice source of '{settingName}'. A device is never built to answer a choice request; configure a separate IDeviceChoiceProvider.",
                    LogSeverity.Error);
                return Array.Empty<SettingChoice>();
            }

            IPluginFactory scope = null;
            try
            {
                scope = factory.NewScope(new Dictionary<string, object>(), null, false);
                if (scope[field.ChoiceSource, true] is IDeviceChoiceProvider provider)
                {
                    var choices = await provider.GetChoicesAsync(field.Name, Draft(draftJson));
                    return choices?.ToArray() ?? Array.Empty<SettingChoice>();
                }

                LogEnvironment.LogEvent($"Choice source '{field.ChoiceSource}' for '{settingName}' of '{objectName}' could not be resolved as an IDeviceChoiceProvider.",
                    LogSeverity.Error);
                return Array.Empty<SettingChoice>();
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Choice source '{field.ChoiceSource}' for '{settingName}' of '{objectName}' failed: {ex.OutlineException()}",
                    LogSeverity.Error);
                return Array.Empty<SettingChoice>();
            }
            finally
            {
                scope?.Dispose();
            }
        }

        /// <inheritdoc />
        public Task<string> GetSettingsAsync(string objectName)
        {
            var device = Device(objectName);
            if (device == null)
            {
                LogEnvironment.LogEvent($"Settings were requested for '{objectName}', which is no device on this agent.",
                    LogSeverity.Warning);
                return Task.FromResult<string>(null);
            }

            var settingsType = DeviceSettingsReflection.SettingsTypeOf(device);
            if (settingsType == null)
            {
                LogEnvironment.LogDebugEvent($"Settings were requested for '{objectName}', which has no settings.", LogSeverity.Report);
                return Task.FromResult<string>(null);
            }

            var settings = store.Read(objectName, settingsType);
            return Task.FromResult(settings == null ? null : DeviceSettingsReflection.Write(settings));
        }

        /// <inheritdoc />
        public Task<ApplySettingsResult> ApplySettingsAsync(string objectName, string settingsJson)
        {
            var device = Device(objectName);
            if (device == null)
            {
                LogEnvironment.LogEvent($"Settings were sent for '{objectName}', which is no device on this agent.",
                    LogSeverity.Warning);
                return Task.FromResult(new ApplySettingsResult { Message = $"'{objectName}' is no device on this agent." });
            }

            var settingsType = DeviceSettingsReflection.SettingsTypeOf(device);
            if (settingsType == null)
            {
                LogEnvironment.LogEvent($"Settings were sent for '{objectName}', which has no settings.", LogSeverity.Warning);
                return Task.FromResult(new ApplySettingsResult { Message = $"'{objectName}' has no settings." });
            }

            var errors = new List<SettingError>();
            var settings = DeviceSettingsReflection.Validate(settingsType, settingsJson, errors);
            if (errors.Count != 0)
            {
                return Task.FromResult(new ApplySettingsResult { Errors = errors.ToArray() });
            }

            try
            {
                store.Write(objectName, settings);
                LogEnvironment.LogEvent($"Settings of device '{objectName}' were updated.", LogSeverity.Report);
                return Task.FromResult(new ApplySettingsResult { Success = true });
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Could not store the settings of device '{objectName}': {ex.OutlineException()}",
                    LogSeverity.Error);
                return Task.FromResult(new ApplySettingsResult { Message = $"The settings of '{objectName}' could not be stored on the agent." });
            }
        }

        /// <summary>
        /// Gibt den Katalog frei und nimmt <c>$DeviceSettings</c> wieder zurück. Die Factory gehört ihm nicht.
        /// </summary>
        public void Dispose()
        {
            factory.UnknownConstructorParameter -= ProvideDeviceSettings;
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Löst <c>$DeviceSettings</c> für das Gerät auf, das gerade gebaut wird.
        /// </summary>
        /// <remarks>
        /// Das Gerät ist der <b>Anforderer</b> (<see cref="UnknownConstructorParameterEventArgs.PluginType"/>),
        /// sein Name der Schlüssel der Ablage. Nicht lesbare Werte = Ausnahme: ein Gerät mit falschen Einstellungen
        /// zu bauen (der falsche Drucker, das falsche Terminal) ist schlimmer als keines.
        /// </remarks>
        private void ProvideDeviceSettings(object sender, UnknownConstructorParameterEventArgs e)
        {
            if (e.Handled || !string.Equals(e.RequestedName, AgentCatalog.DeviceSettingsParameterName, StringComparison.Ordinal))
            {
                return;
            }

            var device = e.PluginType;
            var settingsType = DeviceSettingsReflection.SettingsTypeOf(device?.PluginType);
            if (settingsType == null)
            {
                LogEnvironment.LogEvent($"'{device?.UniqueName}' requests ${AgentCatalog.DeviceSettingsParameterName}, but its type ({device?.PluginType}) does not declare IHasDeviceSettings<T>.",
                    LogSeverity.Error);
                return;
            }

            object settings;
            try
            {
                settings = store.Read(device.UniqueName, settingsType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"The stored settings of device '{device.UniqueName}' are not readable as {settingsType.Name}.", ex);
            }

            if (settings == null)
            {
                LogEnvironment.LogEvent($"Device '{device.UniqueName}' is not configured; it is built with its default settings.",
                    LogSeverity.Warning);
                settings = Activator.CreateInstance(settingsType);
            }

            e.Value = settings;
            e.Handled = true;
        }

        private Type Device(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            return factory.GetScopedPluginTypes(null)
                .Where(d => d.Item.Name == objectName)
                .Select(d => d.Type)
                .FirstOrDefault();
        }

        /// <summary>
        /// Ob ein Typ den Vertrag erfüllt — über den NAMEN, damit der Vertrag am Agenten nicht vorab geladen
        /// sein muss.
        /// </summary>
        private static bool Fulfills(Type type, string contractTypeName)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                if (t.FullName == contractTypeName)
                {
                    return true;
                }
            }

            return type.GetInterfaces().Any(i => i.FullName == contractTypeName);
        }

        private static IReadOnlyDictionary<string, string> Draft(string draftJson)
        {
            var retVal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(draftJson))
            {
                return retVal;
            }

            try
            {
                if (JsonNode.Parse(draftJson) is JsonObject values)
                {
                    foreach (var (key, value) in values)
                    {
                        retVal[key] = value is JsonValue v && v.TryGetValue(out string text) ? text : value?.ToJsonString();
                    }
                }
            }
            catch (JsonException ex)
            {
                LogEnvironment.LogEvent($"The draft values of a choice request are not valid JSON and are ignored: {ex.Message}",
                    LogSeverity.Warning);
            }

            return retVal;
        }
    }
}
