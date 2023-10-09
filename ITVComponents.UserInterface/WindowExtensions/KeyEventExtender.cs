using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using ITVComponents.Plugins;

namespace ITVComponents.UserInterface.WindowExtensions
{
    public class KeyEventExtender:IWindowExtender, IKeyEventProvider
    {
        /// <summary>
        /// Holds a all handlers that are capable for handling events
        /// </summary>
        private List<IKeyHandler> handlers = new List<IKeyHandler>();

        /// <summary>
        /// indicates whether this key-handler is attached to a window
        /// </summary>
        private bool connected = false;

        /// <summary>
        /// the window that needs to be extended by this window-extender
        /// </summary>
        private Window window;
        
        /// <summary>
        /// Extends the target window with the required functions
        /// </summary>
        /// <param name="window">the window that needs to be extended</param>
        public void Connect(Window window)
        {
            if (connected)
            {
                throw new InvalidOperationException("Can only extend one windows at a time. Disconnect first");
            }

            connected = true;
            this.window = window;
            window.PreviewKeyDown+=ProcessKeyDown;
            window.PreviewKeyUp += ProcessKeyUp;
        }

        public void Disconnect(Window window)
        {
            if (!connected || window != this.window)
            {
                throw new InvalidOperationException("Currently not connected to the provided window");
            }

            connected = false;
            window.PreviewKeyDown -= ProcessKeyDown;
            window.PreviewKeyUp -= ProcessKeyUp;
            this.window = null;
        }

        /// <summary>
        /// Processes the KeyUp - Event
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="e">the event arguments</param>
        private void ProcessKeyUp(object sender, KeyEventArgs e)
        {
            IKeyHandler[] keyHandlers;
            lock (handlers)
            {
                keyHandlers = handlers.ToArray();
            }

            e.Handled = keyHandlers.Any(n => n.HandleKeyUp && n.KeyStroke(e));
        }

        /// <summary>
        /// Processes the KeyDown - Event
        /// </summary>
        /// <param name="sender">the event-sender</param>
        /// <param name="e">the event arguments</param>
        private void ProcessKeyDown(object sender, KeyEventArgs e)
        {
            IKeyHandler[] keyHandlers;
            lock (handlers)
            {
                keyHandlers = handlers.ToArray();
            }

            e.Handled = keyHandlers.Any(n => n.HandleKeyDown && n.KeyStroke(e));
        }

        /// <summary>
        /// Registers a KeyHandler that is capable for handling Keystrokes on the main-window
        /// </summary>
        /// <param name="handler">the handler that will process keystrokes</param>
        public void RegisterKeyHandler(IKeyHandler handler)
        {
            lock (handlers)
            {
                handlers.Add(handler);
            }
        }

        /// <summary>
        /// Removes a registered handler
        /// </summary>
        /// <param name="handler">the handler that does not process keystrokes anymore</param>
        public void UnRegisterKeyHandler(IKeyHandler handler)
        {
            lock (handlers)
            {
                if (handlers.Contains(handler))
                {
                    handlers.Remove(handler);
                }
            }
        }
    }
}
