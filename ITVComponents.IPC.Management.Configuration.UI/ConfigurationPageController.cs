using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.InterProcessCommunication.ManagementExtensions.Configuration;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.Settings;
using ITVComponents.UserInterface;
using ITVComponents.UserInterface.WindowExtensions;

namespace ITVComponents.IPC.Management.Configuration.UI
{
    public class ConfigurationPageController: IUserInterface, IKeyHandler, INotifyPropertyChanged, IDeferredInit
    {
        private string _selectedConfigurator;
        private ConfiguratorBase _selectedConfiguration;
        private JsonSettingsSection selectedJsonSection;
        private string _selectedSection;

        public ConfigurationPageController(string uniqueName)
        {
            Name = uniqueName;
        }

        public string Name { get; }

        public bool HandleKeyDown { get; } = false;
        public bool HandleKeyUp { get; } = false;

        public bool Initialized { get; }
        public bool ForceImmediateInitialization { get; }

        public string SelectedConfigurator
        {
            get => _selectedConfigurator;
            set
            {
                _selectedConfigurator = value; 
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(SelectedConfigurator)));
                SelectedConfiguration = ConfiguratorBase.GetConfigurator(_selectedConfigurator);
                SelectedSection = null;
            }
        }

        public string SelectedSection
        {
            get => _selectedSection;
            set
            {
                _selectedSection = value; 
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(SelectedSection)));
                if (!string.IsNullOrEmpty(_selectedSection))
                {
                    SelectedJsonSection = _selectedConfiguration.GetSetting(_selectedSection);
                }
            }
        }

        public JsonSettingsSection SelectedJsonSection
        {
            get => selectedJsonSection;
            set
            { 
                selectedJsonSection = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(SelectedJsonSection)));
            }
        }

        public ConfiguratorBase SelectedConfiguration
        {
            get => _selectedConfiguration;
            private set
            {
                _selectedConfiguration = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(SelectedConfiguration)));
                if (_selectedConfiguration != null)
                {
                    AvailableSettings.Clear();
                    _selectedConfiguration.ConfigurationNames.ForEach(AvailableSettings.Add);
                }
            }
        }

        public ObservableCollection<string> AvailableConfigurations { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> AvailableSettings { get; } = new ObservableCollection<string>();

        public Control GetUi()
        {
            ConfiguratorBase.KnownConfigurators.ForEach(AvailableConfigurations.Add);
            return new ConfigurationPage(this);
        }

        public bool KeyStroke(KeyEventArgs keyEventArgs)
        {
            if (keyEventArgs.IsDown && keyEventArgs.Key == Key.F5)
            {
                AvailableConfigurations.Clear();
                ConfiguratorBase.KnownConfigurators.ForEach(AvailableConfigurations.Add);
                SelectedConfigurator = _selectedConfigurator;
                return true;
            }
            return false;
        }

        public void Initialize()
        {
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        /// <summary>Occurs when a property value changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        public void SaveCurrentConfig()
        {
            if (selectedJsonSection != null && _selectedConfiguration != null)
            {
                _selectedConfiguration.UpdateSetting(_selectedSection, selectedJsonSection);
            }
        }
    }
}
