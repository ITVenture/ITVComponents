using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.AssemblyResolving
{
    /// <summary>
    /// Holds Plugin - Construction Instructions
    /// </summary>
    [Serializable]
    public class AssemblyResolverConfigurationCollection : List<AssemblyResolverConfigurationItem>
    {
        /// <summary>
        /// Gets an AssemblyConfiguration item for the given assemblyname
        /// </summary>
        /// <param name="name">the requested assemblyname</param>
        /// <returns>a resovlver configuration if it was found or null</returns>
        public AssemblyResolverConfigurationItem this[string name]
        {
            get
            {
                return
                    (from t in this where t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) select t)
                        .FirstOrDefault();
            }
        }
    }
}
