using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings;
using ITVComponents.Settings.Native;

namespace ITVComponents.AssemblyResolving
{
    [Serializable]
    public class ResolverConfig : JsonSettingsSection
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use this Configuration object. If the value is set to false, the old .net Settings environment is used.
        /// </summary>
        public bool UseExtConfig { get; set; } = false;

        /// <summary>
        /// Gets a list of fix registered assemblies that can be resolved by the activated assemblyresolver object
        /// </summary>
        public AssemblyResolverConfigurationCollection FixedAssemblies { get; set; } = new AssemblyResolverConfigurationCollection();

        protected override void LoadDefaults()
        {
            //FixedAssemblies.AddRange(ResolverSettings.Default.FixedAssemblies);
        }

        public static class Helper
        {
            private static ResolverConfig section = JsonSettingsSection.GetSection<ResolverConfig>("ITVComponents_AssemblyResolving_AssemblyResolverConfig");
            private static ResolverSettings nativeSection = NativeSettings.GetSection<ResolverSettings>("ITVenture:AssemblyResolving");
            public static AssemblyResolverConfigurationCollection FixedAssemblies => section.UseExtConfig?section.FixedAssemblies:nativeSection.FixedAssemblies;
        }
    }
}
