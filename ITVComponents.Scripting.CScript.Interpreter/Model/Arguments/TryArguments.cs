using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class TryArguments : IExecutorArgument
    {
        public const string Try = "Try";

        public const string Catch = "Catch";

        public const string Finally = "Finally";


        public string CatchVariable { get; set; }
        public bool HasCatch { get; set; }
        public bool HasFinally { get; set; }
    }
}
