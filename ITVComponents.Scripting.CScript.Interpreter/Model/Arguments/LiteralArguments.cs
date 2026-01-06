using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class LiteralArguments : IExecutorArgument
    {
        public const string LiteralType = "LiteralType";
        public object Value { get; set; }
    }
}
