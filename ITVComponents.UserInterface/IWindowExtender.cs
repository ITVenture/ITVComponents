using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace ITVComponents.UserInterface
{
    /// <summary>
    /// Offers an interface that allows a plugin to extend a window
    /// </summary>
    public interface IWindowExtender
    {
        /// <summary>
        /// Extends the target window with the required functions
        /// </summary>
        /// <param name="window">the window that needs to be extended</param>
        void Connect(Window window);

        /// <summary>
        /// Removes the Extension on the target window
        /// </summary>
        /// <param name="window">the window that was previously extended by this WindowExtender object</param>
        void Disconnect(Window window);
    }
}
