using System.Collections.Generic;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Tests
{
    /// <summary>
    /// Confirms that the WebPart assembly list understands the <c>:--&gt;</c> reference notation, so that the
    /// provider-dependent line in appsettings-parts.json can be steered by ONE key from an appended
    /// provider json instead of being rewritten in the host.
    /// <para>
    /// The notation itself is old; it was simply never applied here, because <c>WebPartOptions.Assemblies</c>
    /// carried no <see cref="AutoResolveChildrenAttribute"/> and therefore was never entered by the resolver.
    /// That is the half that is easy to miss: calling <c>RefResolve</c> alone would have changed nothing.
    /// </para>
    /// </summary>
    [TestClass]
    public class WebPartAssemblyRefResolveTests
    {
        [TestMethod]
        public void A_Referenced_Assembly_Name_Is_Taken_From_The_Configured_Key()
        {
            var options = Resolve(new Dictionary<string, string>
            {
                ["ProviderDefaults:SysProviderAssembly"] = "Toolkit.TenantSecurity.PostgreSql.dll",
                ["ITVenture:WebParts:Assemblies:0:AssemblyName"] =
                    ":-->ProviderDefaults:SysProviderAssembly??Toolkit.TenantSecurity.SqlServer.dll"
            });

            Assert.AreEqual("Toolkit.TenantSecurity.PostgreSql.dll", options.Assemblies[0].AssemblyName);
        }

        [TestMethod]
        public void Without_The_Provider_Json_The_Written_Default_Applies()
        {
            // The "provider json is missing" case: it must land on the default that is spelled out in the
            // main file, not on an empty string that would fail in Assembly.Load.
            var options = Resolve(new Dictionary<string, string>
            {
                ["ITVenture:WebParts:Assemblies:0:AssemblyName"] =
                    ":-->ProviderDefaults:SysProviderAssembly??Toolkit.TenantSecurity.SqlServer.dll"
            });

            Assert.AreEqual("Toolkit.TenantSecurity.SqlServer.dll", options.Assemblies[0].AssemblyName);
        }

        [TestMethod]
        public void A_Plain_Assembly_Name_Stays_Exactly_As_It_Was()
        {
            // Regression guard for every existing configuration: only strings starting with ":-->" or "$-->"
            // change their meaning.
            var options = Resolve(new Dictionary<string, string>
            {
                ["ITVenture:WebParts:Assemblies:0:AssemblyName"] = "Toolkit.WebParts.dll",
                ["ITVenture:WebParts:Assemblies:0:DetailConfigPath"] = "ITVenture:WebPartConfigurations:Plain"
            });

            Assert.AreEqual("Toolkit.WebParts.dll", options.Assemblies[0].AssemblyName);
            Assert.AreEqual("ITVenture:WebPartConfigurations:Plain", options.Assemblies[0].DetailConfigPath);
        }

        [TestMethod]
        public void Resolving_Twice_Changes_Nothing()
        {
            var config = BuildConfig(new Dictionary<string, string>
            {
                ["ProviderDefaults:SysProviderAssembly"] = "Toolkit.TenantSecurity.PostgreSql.dll",
                ["ITVenture:WebParts:Assemblies:0:AssemblyName"] = ":-->ProviderDefaults:SysProviderAssembly"
            });

            var options = config.GetSection<WebPartOptions>("ITVenture:WebParts");
            config.RefResolve(options);
            config.RefResolve(options);

            Assert.AreEqual("Toolkit.TenantSecurity.PostgreSql.dll", options.Assemblies[0].AssemblyName);
        }

        [TestMethod]
        public void The_Values_Of_DetailConfigPaths_Are_Resolved_As_Well()
        {
            // DetailConfigPaths is a Dictionary<string,string>. The resolver used to enter generic
            // string/object dictionaries only, so this map passed through untouched and the resolution
            // would have covered two of the three properties of an entry.
            var options = Resolve(new Dictionary<string, string>
            {
                ["ProviderDefaults:ActivationSection"] = "ITVenture:WebPartConfigurations:PostgreFeatures",
                ["ITVenture:WebParts:Assemblies:0:AssemblyName"] = "Toolkit.WebParts.dll",
                ["ITVenture:WebParts:Assemblies:0:DetailConfigPaths:ContextSettings"] =
                    "ITVenture:WebPartConfigurations:TenantViewTypeOptions",
                ["ITVenture:WebParts:Assemblies:0:DetailConfigPaths:ActivationSettings"] =
                    ":-->ProviderDefaults:ActivationSection??ITVenture:WebPartConfigurations:SecurityFeatureOptions"
            });

            var paths = options.Assemblies[0].DetailConfigPaths;
            Assert.AreEqual("ITVenture:WebPartConfigurations:TenantViewTypeOptions", paths["ContextSettings"]);
            Assert.AreEqual("ITVenture:WebPartConfigurations:PostgreFeatures", paths["ActivationSettings"]);
        }

        private static WebPartOptions Resolve(Dictionary<string, string> settings)
        {
            var config = BuildConfig(settings);
            var options = config.GetSection<WebPartOptions>("ITVenture:WebParts");
            config.RefResolve(options);
            return options;
        }

        private static IConfiguration BuildConfig(Dictionary<string, string> settings)
            => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
