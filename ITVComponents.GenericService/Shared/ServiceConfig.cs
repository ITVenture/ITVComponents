using System;
using System.Collections.Generic;
using System.ServiceProcess;
using ITVComponents.Plugins.Config;
using ITVComponents.Settings;

namespace ITVComponents.GenericService.Shared
{
    public class ServiceConfig:JsonSettingsSection
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use this Configuration object. If the value is set to false, the old .net Settings environment is used.
        /// </summary>
        public bool UseExtConfig { get; set; } = false;

        public string Path { get; set; }

        public string ServiceName { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public ServiceStartMode StartType { get; set; }

        public string ServiceUser { get; set; }

        public string ServicePassword { get; set; }

        public List<string> Dependencies { get;set; } = new List<string>();

        /// <summary>
        /// Gets or sets a value indicating whether the service fails to start when its plugins cannot be set up. Default:
        /// false - the service keeps running without them (the behavior so far). Set it for a service that must recover by
        /// itself: only a failed start lets the recovery actions of Windows (or <c>Restart=on-failure</c> of systemd) start
        /// it again.
        /// </summary>
        public bool FailOnStartupError { get; set; }

        public PluginConfigurationCollection PlugIns { get; set; } = new PluginConfigurationCollection();

        public GenericTypeConstructionCollection GenericTypeInformation { get; set; } = new GenericTypeConstructionCollection();
    }
}
