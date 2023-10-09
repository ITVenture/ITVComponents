using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins;

namespace ITVComponents.Formatting.ScriptExtensions
{
    /// <summary>
    /// Dummy-Plugin that will register Scripting-features of the TextFormat lib
    /// </summary>
    public class ScriptFormatInitializer
    {
        /// <summary>
        /// Initializes a new instance of the ScriptFormatInitializer Plugin
        /// </summary>
        public ScriptFormatInitializer()
        {
            ScriptExtensionHelper.Register();
        }
    }
}
