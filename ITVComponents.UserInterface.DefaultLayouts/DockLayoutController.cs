using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.UserInterface.DefaultLayouts.Config;

namespace ITVComponents.UserInterface.DefaultLayouts
{
    public class DockLayoutController:LayoutManager, IUserInterface, IDeferredInit
    {
        /// <summary>
        /// the layout object that is used to put UI-Elements on
        /// </summary>
        private DockLayout layout;

        /// <summary>
        /// The Configuration of this DockLayout item
        /// </summary>
        private DockLayoutDefinition config;

        /// <summary>
        /// Initializes a new instance of the StackLayoutController class
        /// </summary>
        public DockLayoutController(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        public bool ForceImmediateInitialization => false;

        /// <summary>
        /// Gets the default UI of this UI - Element
        /// </summary>
        public Control GetUi()
        {
            return layout;
        }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        public void Initialize()
        {
            if (!Initialized)
            {
                try
                {
                    config = DockLayoutConfig.Helper.DockLayouts[Name];
                    layout = new DockLayout();
                    if (config.Height != 0)
                    {
                        layout.MinHeight = config.Height;
                    }

                    if (config.Width != 0)
                    {
                        layout.MinWidth= config.Width;
                    }

                    Init();
                }
                finally
                {
                    Initialized = true;
                }
            }
        }

        /// <summary>
        /// Runs Initializations on derived objects
        /// </summary>
        protected virtual void Init()
        {
        }

        /// <summary>
        /// Adds an UI - Component to this user Ui-Layouter
        /// </summary>
        /// <param name="userInterface"></param>
        protected override void AddUiComponent(IUserInterface userInterface)
        {
            Control gui = userInterface.GetUi();
            ConfigureUi(userInterface.Name, gui);
            layout.mainDock.Children.Add(gui);
            LogEnvironment.LogDebugEvent(string.Format("Adding {0} to {1}",userInterface.Name,Name), LogSeverity.Report);
        }

        /// <summary>
        /// configures the given gui
        /// </summary>
        /// <param name="name">the name of the plugin that provided the gui element</param>
        /// <param name="gui">the gui element</param>
        private void ConfigureUi(string name, Control gui)
        {
            DockLayoutItem item = config.Children[name];
            if (item != null)
            {
                DockPanel.SetDock(gui, item.Dock);
                if (item.Height != 0)
                {
                    gui.Height= item.Width;
                }

                if (item.Width != 0)
                {
                    gui.Width = item.Width;
                }

                if (item.Fill)
                {
                    gui.HorizontalAlignment = HorizontalAlignment.Stretch;
                    gui.VerticalContentAlignment = VerticalAlignment.Stretch;
                    LogEnvironment.LogDebugEvent(string.Format("Stretching {0}", name), LogSeverity.Report);
                }
            }
            else
            {
                DockPanel.SetDock(gui, config.DefaultDock);
            }
        }
    }
}
