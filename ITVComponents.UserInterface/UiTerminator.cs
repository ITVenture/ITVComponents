using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace ITVComponents.UserInterface
{
    public sealed class UiTerminator:IUserInterface
    {
        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string Name { get; }

        public UiTerminator(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets the default UI of this UI - Element
        /// </summary>
        public Control GetUi()
        {
            return null;
        }
    }
}
