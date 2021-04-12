using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.UserInterface
{
    /// <summary>
    /// Provides Methods to control the main-application of an ITVUserInterface - App
    /// </summary>
    public interface IUIApp
    {
        /// <summary>
        /// Gets or sets a value indicating whether to automatically exit an application
        /// </summary>
        bool AutoShutdown { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to currently display the main-ui
        /// </summary>
        bool ShowMainWindow { get; set; }

        /// <summary>
        /// Shuts down the application
        /// </summary>
        void ExitApplication();
    }
}
