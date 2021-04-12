using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Logging;

namespace ITVComponents.UserInterface
{
    /// <summary>
    /// Provides a possibility to re-implement a Layout-Handler for a gui application
    /// </summary>
    public abstract class LayoutManager
    {
        /// <summary>
        /// indicates wheter this LayoutManager is the root Manager
        /// </summary>
        private bool isRoot = true;

        /// <summary>
        /// Prevents a default instance of the LayoutManager class from being created
        /// </summary>
        protected LayoutManager()
        {
            subManagers = new Stack<LayoutManager>();
        }

        /// <summary>
        /// the default layout manager
        /// </summary>
        private Stack<LayoutManager> subManagers;

        /// <summary>
        /// Gets or sets the root-manager
        /// </summary>
        public LayoutManager RootManager { get; private set; }

        /// <summary>
        /// Registers a specific UserInterface - Element on the default Window
        /// </summary>
        /// <param name="userInterface"></param>
        public void RegisterIterface(IUserInterface userInterface)
        {
            LogEnvironment.LogDebugEvent(string.Format("UserInterce is UiTerminator: {0}", userInterface is UiTerminator),
                                    LogSeverity.Report);
            if (!(userInterface is UiTerminator))
            {
                if (subManagers.Count == 0)
                {
                    AddUiComponent(userInterface);
                }
                else
                {
                    subManagers.Peek().RegisterIterface(userInterface);
                }
            }
            else
            {
                LogEnvironment.LogDebugEvent("Finalizing Layout", LogSeverity.Report);
                subManagers.Pop();
            }
        }

        /// <summary>
        /// Adds a new layout to the list of available layout managers
        /// </summary>
        /// <param name="nextLayout">the next layout manager that is used for layouting sub-plugins</param>
        protected void SetLayout(LayoutManager nextLayout)
        {
            if (!isRoot)
            {
                throw new InvalidOperationException("Only supported on root - layouter");
            }

            nextLayout.isRoot = false;
            nextLayout.RootManager = this;
            subManagers.Push(nextLayout);
        }

        /// <summary>
        /// Adds an UI - Component to this user Ui-Layouter
        /// </summary>
        /// <param name="userInterface"></param>
        protected abstract void AddUiComponent(IUserInterface userInterface);
    }
}
