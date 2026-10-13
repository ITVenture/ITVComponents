using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.Helpers;
using ITVComponents.Plugins;
using ITVComponents.Plugins.Config;
using ITVComponents.Plugins.Initialization;

namespace ITVComponents.AgentServices.Test
{
    /// <summary>Ein Vertrag, wie ihn ein Host für ein Gerät deklariert.</summary>
    public interface IReceiptPrinter
    {
        Task<string> PrintAsync(string text);
    }

    /// <summary>Ein zweiter Vertrag — darf in der Drucker-Auswahl nicht auftauchen.</summary>
    public interface ICashDrawer
    {
        Task OpenAsync();
    }

    public class PrinterSettings
    {
        [DeviceSetting("Drucker", Required = true, ChoiceSource = "printers")]
        public string PrinterName { get; set; } = string.Empty;

        [DeviceSetting("Papierbreite (Zeichen)", Required = true)]
        public int Columns { get; set; } = 42;

        [DeviceSetting("Logo drucken")]
        public bool PrintLogo { get; set; }

        /// <summary>Kein Feld: lässt sich von aussen nicht setzen.</summary>
        public string Secret { get; set; } = "untouched";
    }

    /// <summary>Ein Drucker, der seine Einstellungen im Konstruktor bekommt.</summary>
    public class TestPrinter : IReceiptPrinter, IHasDeviceSettings<PrinterSettings>, IPlugin
    {
        public TestPrinter(PrinterSettings settings)
        {
            Settings = settings;
        }

        public PrinterSettings Settings { get; }

        public string UniqueName { get; set; } = string.Empty;

        public event EventHandler? Disposed;

        public Task<string> PrintAsync(string text) => Task.FromResult($"{Settings.PrinterName}/{Settings.Columns}:{text}");

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Ein Drucker, dessen Konstruktor die Hardware öffnen würde — hier: wirft. Der Katalog darf ihn
    /// trotzdem aufzählen und beschreiben, weil er nie gebaut wird.
    /// </summary>
    public class ExplodingPrinter : IReceiptPrinter, IHasDeviceSettings<PrinterSettings>, IPlugin
    {
        public ExplodingPrinter(PrinterSettings settings)
        {
            throw new InvalidOperationException("The constructor ran - the catalog built the device.");
        }

        public string UniqueName { get; set; } = string.Empty;

        public event EventHandler? Disposed;

        public Task<string> PrintAsync(string text) => Task.FromResult(text);

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    public class TestDrawer : ICashDrawer, IPlugin
    {
        public string UniqueName { get; set; } = string.Empty;

        public event EventHandler? Disposed;

        public Task OpenAsync() => Task.CompletedTask;

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Liefert die „installierten Drucker".</summary>
    public class TestPrinterChoices : IDeviceChoiceProvider, IPlugin
    {
        public string UniqueName { get; set; } = string.Empty;

        public event EventHandler? Disposed;

        public Task<IReadOnlyList<SettingChoice>> GetChoicesAsync(string settingName,
            IReadOnlyDictionary<string, string?> draft)
            => Task.FromResult<IReadOnlyList<SettingChoice>>(new[]
            {
                new SettingChoice { Value = "EPSON TM-T20III", Label = "EPSON TM-T20III Receipt" },
                new SettingChoice { Value = "Star TSP100", Label = $"Star (draft: {draft.GetValueOrDefault("columns")})" }
            });

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stellt die Geräte als Bereichs-Plugins bereit, wie es SettingsDrivenLoader am Agenten täte.</summary>
    public class DeviceLoader : IDynamicLoader
    {
        public static readonly PluginConfigurationItem[] Items =
        {
            new() { Name = "epson", DisplayName = "Epson an der Theke", LoadType = PluginLoadType.Scope,
                ConstructionString = "[agenttest]<ITVComponents.AgentServices.Test.TestPrinter>$DeviceSettings" },
            new() { Name = "star", LoadType = PluginLoadType.Scope,
                ConstructionString = "[agenttest]<ITVComponents.AgentServices.Test.TestPrinter>$DeviceSettings" },
            new() { Name = "exploding", LoadType = PluginLoadType.Scope,
                ConstructionString = "[agenttest]<ITVComponents.AgentServices.Test.ExplodingPrinter>$DeviceSettings" },
            new() { Name = "drawer", LoadType = PluginLoadType.Scope,
                ConstructionString = "[agenttest]<ITVComponents.AgentServices.Test.TestDrawer>" },
            new() { Name = "printers", LoadType = PluginLoadType.Scope,
                ConstructionString = "[agenttest]<ITVComponents.AgentServices.Test.TestPrinterChoices>" },
            // Lokal abgeschaltet: darf im Katalog gar nicht erst erscheinen.
            new() { Name = "disabled", LoadType = PluginLoadType.Scope, Disabled = true,
                ConstructionString = "[agenttest]<ITVComponents.AgentServices.Test.TestPrinter>$DeviceSettings" }
        };

        public string UniqueName { get; set; } = string.Empty;

        public event EventHandler? Disposed;

        public void Dispose() => Disposed?.Invoke(this, EventArgs.Empty);

        public IEnumerable<string> LoadDynamicAssemblies(PluginLoadType currentLoadType, bool writeAccess = true)
            => Array.Empty<string>();

        public bool HasParamsFor(string uniqueName) => false;

        public void GetGenericParams(string uniqueName, List<GenericTypeArgument> genericTypeArguments,
            Dictionary<string, object> customVariables, StringFormatProvider formatter)
        {
        }

        public bool HasScopedPlugin(string pluginName) => Available().Any(n => n.Name == pluginName);

        public PluginConfigurationItem? GetScopedPlugin(string pluginName)
            => Available().FirstOrDefault(n => n.Name == pluginName);

        // Absichtlich MIT dem abgeschalteten Eintrag: die Factory muss ihn selbst herausfiltern.
        public IEnumerable<PluginConfigurationItem> GetScopedPluginNames() => Items;

        private static IEnumerable<PluginConfigurationItem> Available() => Items.Where(n => !n.Disabled);
    }

    /// <summary>Eine Ablage im Speicher.</summary>
    public class MemoryStore : IDeviceSettingsStore
    {
        public Dictionary<string, object> Values { get; } = new();

        public object? Read(string objectName, Type settingsType) => Values.TryGetValue(objectName, out var v) ? v : null;

        public void Write(string objectName, object settings) => Values[objectName] = settings;
    }
}
