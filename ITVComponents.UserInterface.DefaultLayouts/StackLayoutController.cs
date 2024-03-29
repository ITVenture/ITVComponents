﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using ITVComponents.Logging;
using ITVComponents.Plugins;

namespace ITVComponents.UserInterface.DefaultLayouts
{
    public class StackLayoutController:LayoutManager, IUserInterface
    {
        /// <summary>
        /// the layout object that is used to put UI-Elements on
        /// </summary>
        private StackLayout layout;

        /// <summary>
        /// Initializes a new instance of the StackLayoutController class
        /// </summary>
        /// <param name="landscapeStack"></param>
        public StackLayoutController(bool landscapeStack)
        {
            layout = new StackLayout();
            layout.mainStack.Orientation = landscapeStack ? Orientation.Horizontal : Orientation.Vertical;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Gets the default UI of this UI - Element
        /// </summary>
        public Control GetUi()
        {
            return layout;
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            layout.mainStack.Children.Clear();
        }

        /// <summary>
        /// Adds an UI - Component to this user Ui-Layouter
        /// </summary>
        /// <param name="userInterface"></param>
        protected override void AddUiComponent(IUserInterface userInterface)
        {
            LogEnvironment.LogDebugEvent(string.Format("Adding {0} to {1}", userInterface.UniqueName, UniqueName), LogSeverity.Report);
            layout.mainStack.Children.Add(userInterface.GetUi());
        }

        /// <summary>
        /// Raises the disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            EventHandler handler = Disposed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
