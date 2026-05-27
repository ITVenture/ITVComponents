using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.UserInterface.DefaultLayouts.Config;

namespace ITVComponents.UserInterface.DefaultLayouts
{
    public class DockLayoutSettings
    {
        /// <summary>
        /// Gets the available dock layouts
        /// </summary>
        public DockLayoutCollection DockLayouts { get; set; } = new DockLayoutCollection();
    }
}
