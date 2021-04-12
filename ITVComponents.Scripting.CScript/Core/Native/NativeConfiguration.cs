using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Core.Native
{
    public class NativeConfiguration
    {
        public List<string> Usings { get; } = new List<string>();

        public List<string> References { get; } = new List<string>();

        public bool UseRoslyn = false;
    }
}
