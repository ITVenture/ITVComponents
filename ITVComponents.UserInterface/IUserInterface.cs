using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ITVComponents.Plugins;

namespace ITVComponents.UserInterface
{
    /// <summary>
    /// Represents a Plugin that is able to register UI - Elements on a LayoutManager object
    /// </summary>
    public interface IUserInterface:IPlugin
    {
        /// <summary>
        /// Gets the default UI of this UI - Element
        /// </summary>
        Control GetUi();
    }
}
