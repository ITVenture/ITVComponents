using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ITVComponents.Logging;
using ITVComponents.Settings.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.AgentServices.Test
{
    /// <summary>
    /// A user settings file that every account of the machine may write only contributes the sections it is meant for
    /// (§123) - otherwise any account overrides ITVenture:AssemblyResolving for the agent of the next one.
    /// </summary>
    /// <remarks>NativeSettings is static - not parallel, and every test resets it.</remarks>
    [TestClass]
    [DoNotParallelize]
    public class SectionScopedUserSettingsTest
    {
        private const string Devices = NativeDeviceSettingsStore.SectionPath;

        private const string Content = """
            {
              "ITVenture": {
                "AgentServices": {
                  "Devices": { "epson": { "PrinterName": "EPSON" } },
                  "DevicesX": { "Value": "next to the section, not in it" }
                },
                "AssemblyResolving": { "FixedAssemblies": [ { "Name": "Some.Assembly", "Path": "C:\\evil\\Some.Assembly.dll" } ] },
                "Plugins": { "Constants": { "Secret": "do-not-log-me" } }
              },
              "itventure:agentservices:devices:star": { "PrinterName": "Star" }
            }
            """;

        private string directory = null!;
        private string file = null!;

        [TestInitialize]
        public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "scopedsettings-" + Guid.NewGuid().ToString("N"));
            file = Path.Combine(directory, "appsettings.json");
            Directory.CreateDirectory(directory);
            File.WriteAllText(file, Content);
            NativeSettings.Init(true);
        }

        [TestCleanup]
        public void Cleanup()
        {
            NativeSettings.Init(true);
            Directory.Delete(directory, true);
        }

        [TestMethod]
        public void Without_Sections_The_File_Still_Overrides_Everything()
        {
            NativeSettings.AddUserSettings(file);

            Assert.IsNull(NativeSettings.UserSettingsSections);
            Assert.IsTrue(NativeSettings.SectionExists("ITVenture:AssemblyResolving"));
            Assert.IsTrue(NativeSettings.UserSettingsAllow("ITVenture:AssemblyResolving"));
        }

        [TestMethod]
        public void With_Sections_Only_Keys_Below_Them_Are_Taken_On_A_Colon_Boundary_And_Ignoring_Case()
        {
            NativeSettings.AddUserSettings(file, Devices);

            CollectionAssert.AreEqual(new[] { Devices }, NativeSettings.UserSettingsSections);
            Assert.AreEqual("EPSON", NativeSettings.Configuration[$"{Devices}:epson:PrinterName"]);
            Assert.AreEqual("Star", NativeSettings.Configuration[$"{Devices}:star:PrinterName"], "a key in other case is still within the section");
            Assert.IsFalse(NativeSettings.SectionExists("ITVenture:AssemblyResolving"));
            Assert.IsFalse(NativeSettings.SectionExists("ITVenture:Plugins:Constants"));
            Assert.IsFalse(NativeSettings.SectionExists("ITVenture:AgentServices:DevicesX"), "a prefix is no section");
            Assert.IsFalse(NativeSettings.UserSettingsAllow("ITVenture:AssemblyResolving"));
            Assert.IsTrue(NativeSettings.UserSettingsAllow($"{Devices}:epson"));
        }

        [TestMethod]
        public void A_Reload_Filters_Again()
        {
            NativeSettings.AddUserSettings(file, Devices);
            Assert.AreEqual("EPSON", NativeSettings.Configuration[$"{Devices}:epson:PrinterName"]);

            File.WriteAllText(file, """{ "ITVenture": { "AgentServices": { "Devices": { "epson": { "PrinterName": "NEW" } } }, "AssemblyResolving": { "FixedAssemblies": [ { "Name": "X", "Path": "Y" } ] } } }""");
            NativeSettings.Reload();

            Assert.AreEqual("NEW", NativeSettings.Configuration[$"{Devices}:epson:PrinterName"]);
            Assert.IsFalse(NativeSettings.SectionExists("ITVenture:AssemblyResolving"));
        }

        [TestMethod]
        public void Ignored_Sections_Are_Reported_Without_Their_Values()
        {
            var warnings = new List<string>();
            NativeSettings.AddUserSettings(file, Devices);
            using (LogEnvironment.SetTemporaryLogHook((debug, text, severity, context) =>
                   {
                       if (severity == LogSeverity.Warning)
                       {
                           warnings.Add(text);
                       }
                   }))
            {
                Assert.IsNotNull(NativeSettings.Configuration);
            }

            var warning = warnings.Single(w => w.Contains("may only contribute"));
            StringAssert.Contains(warning, "ITVenture:AssemblyResolving");
            StringAssert.Contains(warning, "ITVenture:Plugins");
            StringAssert.Contains(warning, "ITVenture:AgentServices:DevicesX");
            Assert.IsFalse(warning.Contains("do-not-log-me") || warning.Contains("evil"), warning);
        }

        [TestMethod]
        public void Writing_A_Device_Outside_The_Allowed_Sections_Fails_While_Setting_It_Up()
        {
            NativeSettings.AddUserSettings(file, "ITVenture:Other");

            Assert.ThrowsExactly<InvalidOperationException>(() => new NativeDeviceSettingsStore().Write("epson", new PrinterSettings()));
        }

        [TestMethod]
        public void Writing_A_Device_Within_The_Allowed_Sections_Works_And_Keeps_The_Rest_Of_The_File()
        {
            NativeSettings.AddUserSettings(file, Devices);
            var store = new NativeDeviceSettingsStore();
            store.Write("epson", new PrinterSettings { PrinterName = "B", Columns = 32 });

            Assert.AreEqual("B", ((PrinterSettings)store.Read("epson", typeof(PrinterSettings))).PrinterName);
            StringAssert.Contains(File.ReadAllText(file), "AssemblyResolving", "ignored is not deleted - the file belongs to whoever wrote it");
            Assert.IsFalse(NativeSettings.SectionExists("ITVenture:AssemblyResolving"));
        }

        [TestMethod]
        public void Only_Blank_Sections_Are_A_Mistake_Not_Everything()
        {
            Assert.ThrowsExactly<ArgumentException>(() => NativeSettings.AddUserSettings(file, " ", ":"));
        }
    }
}
