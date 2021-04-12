using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using ITVComponents.Settings;
using ITVComponents.Settings.Native;
using ITVComponents.UserInterface.DefaultLayouts.Config;

namespace ITVComponents.UserInterface.DefaultLayouts
{
    [Serializable]
    public class DockLayoutConfig:JsonSettingsSection
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use this Configuration object. If the value is set to false, the old .net Settings environment is used.
        /// </summary>
        public bool UseExtConfig { get; set; } = false;

        /// <summary>
        /// Gets the available dock layouts
        /// </summary>
        public DockLayoutCollection DockLayouts { get; set; } = new DockLayoutCollection();

        /// <summary>
        /// Creates a default configuration
        /// </summary>
        /// <returns>a default collection that contains a dummy configuration</returns>
        protected override void LoadDefaults()
        {
            DockLayouts.Clear();
            DockLayouts.AddRange(Helper.nativeSection.DockLayouts);
        }

        public static class Helper
        {
            private static DockLayoutConfig Section => GetSection<DockLayoutConfig>("ITVComponents_UserInterface_DefaultLayouts_DockLayoutConfiguration");

            internal static DockLayoutSettings nativeSection = NativeSettings.GetSection<DockLayoutSettings>("ITVenture:UserInterface:Dock");

            public static DockLayoutCollection DockLayouts => Section.UseExtConfig ? Section.DockLayouts: nativeSection.DockLayouts;
        }
    }
}
