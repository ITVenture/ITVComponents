using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class ExecutionSwitchArguments : IExecutorArgument
    {
        public ExecutionSwitchType SwitchType { get; set; }

        public bool Flag { get; set; }
    }

    internal enum ExecutionSwitchType
    {
        None,
        TypeSafety,
        LazyInvokation,
        BypassCompatibilityForLazyInvokation
    }
}
