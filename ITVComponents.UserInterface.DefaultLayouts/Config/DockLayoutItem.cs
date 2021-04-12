using System;
using System.Windows.Controls;

namespace ITVComponents.UserInterface.DefaultLayouts.Config
{
    [Serializable]
    public class DockLayoutItem
    {
        /// <summary>
        /// Initializes a new instance of the DockLayoutItem class
        /// </summary>
        public DockLayoutItem()
        {
        }

        /// <summary>
        /// Gets or sets the Name of this DockItem
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Dock that is used for this LayoutItem
        /// </summary>
        public Dock Dock { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the DockItem should fill the rest of the remaining space
        /// </summary>
        public bool Fill { get; set; }

        /// <summary>
        /// Gets or sets the Width of this Child
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the Height of this Child
        /// </summary>
        public int Height { get; set; }
    }
}