using System.Collections.Generic;
using System.ServiceProcess;
using ITVComponents.GenericService.Shared;
using ITVComponents.Plugins.Config;
using ITVComponents.Settings;
using ITVComponents.Settings.Native;

namespace ITVComponents.GenericService
{
    internal static class ServiceConfigHelper
    {

        public static ServiceConfig Section => JsonSettingsSection.GetSection<ServiceConfig>("GenericService_ServiceConfiguration");

        public static ServiceSettings Default = NativeSettings.GetSection<ServiceSettings>("ITVenture:ServiceConfiguration");

        public static string Path => Section.UseExtConfig ? Section.Path : Default.Path;

        public static string ServiceName => Section.UseExtConfig ? Section.ServiceName : Default.ServiceName;

        public static string DisplayName => Section.UseExtConfig ? Section.DisplayName : Default.DisplayName;

        public static string Description => Section.UseExtConfig ? Section.Description : Default.Description;

        public static ServiceStartMode StartType => Section.UseExtConfig ? Section.StartType : Default.StartType;

        public static string ServiceUser => Section.UseExtConfig ? Section.ServiceUser : Default.ServiceUser;

        public static string ServicePassword => Section.UseExtConfig ? Section.ServicePassword : Default.ServicePassword;

        public static PluginConfigurationCollection PlugIns => Section.UseExtConfig ? Section.PlugIns : Default.PlugIns;

        public static IList<string> Dependencies => Section.UseExtConfig ? (IList<string>)Section.Dependencies : Default.Dependencies;
    }
}
