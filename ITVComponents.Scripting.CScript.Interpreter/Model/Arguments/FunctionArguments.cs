using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class FunctionArguments : IExecutorArgument
    {
        public const string FunctionBody = "FunctionBody";
        public string[] Arguments { get; set; }

        public string Name { get; set; }
    }
}
