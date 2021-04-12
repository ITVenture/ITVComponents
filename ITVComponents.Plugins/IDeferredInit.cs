using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Plugins
{
    public interface IDeferredInit
    {
        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        bool Initialized { get; }

        /// <summary>
        /// Indicates whether this Object requires immediate Initialization right after calling the constructor
        /// </summary>
        bool ForceImmediateInitialization { get; }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        void Initialize();
    }
}
