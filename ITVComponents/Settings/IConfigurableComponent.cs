using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Settings
{
    /// <summary>
    /// Enables a class to dynamically re-configure while the application is running
    /// </summary>
    public interface IConfigurableComponent
    {
        /// <summary>
        /// Gets a list of consumed sections by the implementing component
        /// </summary>
        JsonSectionDefinition[] ConsumedSections { get; }

        /// <summary>
        /// Gets a value indicating whether the component is currently in the configuration mode
        /// </summary>
        bool Configuring { get; }

        /// <summary>
        /// Suspends all tasks executed by this component and waits for new settings
        /// </summary>
        void EnterConfigurationMode();

        /// <summary>
        /// Resumes all tasks, after the new configuration settings have been applied
        /// </summary>
        void LeaveConfigurationMode();
    }

    /// <summary>
    /// Declares a Section that is comsumed by an object implementing the IConfigurableComponent class
    /// </summary>
    public class JsonSectionDefinition
    {
        /// <summary>
        /// Gets or sets the name of the consumed Settings section
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Type of the consumed settings section
        /// </summary>
        public Type SettingsType { get; set; }
    }
}
