using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class LoopJumpArguments : IExecutorArgument
    {
        public LoopJumpType Type { get; set; }
    }

    internal enum LoopJumpType
    {
        Continue,
        Break
    }
}
