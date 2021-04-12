using System;
using System.Windows.Controls;

namespace ITVComponents.UserInterface.DefaultLayouts.Config
{
    [Serializable]
    public class DockLayoutDefinition
    {
        /// <summary>
        /// Initializes a new instance of the DockLayoutDefinition class
        /// </summary>
        public DockLayoutDefinition()
        {
            Children = new DockLayoutItemCollection();
        }

        /// <summary>
        /// Gets or sets the Name of this DockLayout
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the the Height of this container
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the the Width of this container
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the the DefaultDock that is used for controls that do not provide their own dock property
        /// </summary>
        public Dock DefaultDock { get; set; }

        /// <summary>
        /// Gets or sets a list of Parameters that are used for this Step
        /// </summary>
        public DockLayoutItemCollection Children { get; set; }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Name))
            {
                return $"{Name} ({Width}x{Height}, @{DefaultDock}, {Children.Count} children)";
            }

            return base.ToString();
        }
    }
}