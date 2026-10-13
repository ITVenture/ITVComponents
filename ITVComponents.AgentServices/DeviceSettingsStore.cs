using System;
using System.Text.Json.Nodes;
using ITVComponents.Settings.Native;

namespace ITVComponents.AgentServices
{
    /// <summary>
    /// Wo der Agent die Einstellungen seiner Geräte ablegt.
    /// </summary>
    public interface IDeviceSettingsStore
    {
        /// <summary>Die abgelegten Einstellungen eines Geräts, oder <c>null</c>, wenn es keine gibt.</summary>
        /// <param name="objectName">der Name des Geräts</param>
        /// <param name="settingsType">der Einstellungstyp des Geräts</param>
        object Read(string objectName, Type settingsType);

        /// <summary>Legt die Einstellungen eines Geräts ab und ersetzt, was dort stand.</summary>
        /// <param name="objectName">der Name des Geräts</param>
        /// <param name="settings">die Einstellungen</param>
        void Write(string objectName, object settings);
    }

    /// <summary>
    /// Legt die Einstellungen in den nativen Einstellungen des Agenten ab, je Gerät unter
    /// <c>ITVenture:AgentServices:Devices:&lt;Name&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Gelesen</b> wird über <see cref="NativeSettings"/> — also aus allen Quellen des Agenten. Ein Gerät lässt
    /// sich damit auch von Hand in seiner <c>appsettings.json</c> vorbelegen.
    /// </para>
    /// <para>
    /// <b>Geschrieben</b> wird mit <see cref="JsonUserSettings"/> in die Benutzerdatei
    /// (<see cref="NativeSettings.UserSettingsFile"/>): das Programmverzeichnis ist nach einer Installation nicht
    /// beschreibbar. Die Datei wird für jedes Schreiben neu geladen, damit nichts überschrieben wird, was ein
    /// anderer seit dem letzten Mal dort abgelegt hat, und danach wird die Konfiguration sofort neu gelesen.
    /// </para>
    /// <para>
    /// Die Einstellungen liegen nur hier und nicht in der Web-Anwendung: nach der Einrichtung ist der Agent eine
    /// Blackbox. Wird der Rechner neu aufgesetzt, wird neu eingerichtet.
    /// </para>
    /// </remarks>
    public class NativeDeviceSettingsStore : IDeviceSettingsStore
    {
        /// <summary>Der Abschnitt, unter dem die Geräte liegen.</summary>
        public const string SectionPath = "ITVenture:AgentServices:Devices";

        private static readonly object WriteLock = new();

        private readonly string userSettingsFile;

        /// <summary>Initialisiert die Ablage; geschrieben wird in <see cref="NativeSettings.UserSettingsFile"/>.</summary>
        public NativeDeviceSettingsStore() : this(null)
        {
        }

        /// <summary>Initialisiert die Ablage mit einer bestimmten Benutzerdatei.</summary>
        /// <param name="userSettingsFile">die Datei, in die geschrieben wird; leer = <see cref="NativeSettings.UserSettingsFile"/></param>
        /// <remarks>Gelesen wird trotzdem über <see cref="NativeSettings"/> — die Datei muss dort angehängt sein.</remarks>
        public NativeDeviceSettingsStore(string userSettingsFile)
        {
            this.userSettingsFile = string.IsNullOrEmpty(userSettingsFile) ? null : userSettingsFile;
        }

        /// <inheritdoc />
        public object Read(string objectName, Type settingsType)
        {
            var path = PathOf(objectName);
            return NativeSettings.SectionExists(path) ? NativeSettings.GetSection(path, settingsType) : null;
        }

        /// <inheritdoc />
        public void Write(string objectName, object settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PathOf(objectName);
            var file = userSettingsFile ?? NativeSettings.UserSettingsFile
                ?? throw new InvalidOperationException("No user settings file is registered; call NativeSettings.AddUserSettings at the start of the agent.");
            lock (WriteLock)
            {
                var root = JsonUserSettings.LoadFrom(file);
                var devices = root;
                foreach (var part in SectionPath.Split(':'))
                {
                    devices = devices.GetSubSection(part);
                }

                devices.SetSubSection(objectName, JsonNode.Parse(DeviceSettingsReflection.Write(settings)));
                root.Save();
                NativeSettings.Reload();
            }
        }

        private static string PathOf(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                throw new ArgumentNullException(nameof(objectName));
            }

            // Ein Doppelpunkt waere in der Konfiguration ein weiterer Abschnitt - das Geraet laege woanders,
            // als es gesucht wird.
            if (objectName.Contains(':'))
            {
                throw new ArgumentException($"The device name '{objectName}' must not contain ':'.", nameof(objectName));
            }

            return $"{SectionPath}:{objectName}";
        }
    }
}
