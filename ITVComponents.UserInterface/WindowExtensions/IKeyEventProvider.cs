using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.UserInterface.WindowExtensions
{
    public interface IKeyEventProvider
    {
        /// <summary>
        /// Registers a KeyHandler that is capable for handling Keystrokes on the main-window
        /// </summary>
        /// <param name="handler">the handler that will process keystrokes</param>
        void RegisterKeyHandler(IKeyHandler handler);

        /// <summary>
        /// Removes a registered handler
        /// </summary>
        /// <param name="handler">the handler that does not process keystrokes anymore</param>
        void UnRegisterKeyHandler(IKeyHandler handler);


    }
}
