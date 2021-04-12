using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace ITVComponents.UserInterface.WindowExtensions
{
    public interface IKeyHandler
    {
        /// <summary>
        /// Gets a value indicating whether this handler is able to process keydown - events
        /// </summary>
        bool HandleKeyDown { get; }

        /// <summary>
        /// Gets a value indicating whether this handler is able to process keyup - events
        /// </summary>
        bool HandleKeyUp { get; }

        /// <summary>
        /// Processes a keystroke and returns a value indicating whether the provided stroke was handled
        /// </summary>
        /// <param name="keyEventArgs">the keystroke that needs to be handled</param>
        /// <returns>a value indicating whether the provided keystroke was handled</returns>
        bool KeyStroke(KeyEventArgs keyEventArgs);
    }
}
