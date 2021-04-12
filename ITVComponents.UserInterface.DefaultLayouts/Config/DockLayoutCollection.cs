using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.UserInterface.DefaultLayouts.Config
{
    [Serializable]
    public class DockLayoutCollection : List<DockLayoutDefinition>
    {
        /// <summary>
        /// Gets the configurationitem with the given name
        /// </summary>
        /// <param name="name">the name of the requested item</param>
        /// <returns>the instance of the requested configuration item</returns>
        public DockLayoutDefinition this[string name] => (from t in this where t.Name == name select t).First();
    }
}
