using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class IncrementArguments : IExecutorArgument
    {
        public const string BaseValue = "BaseValue";
        public IncrementType IncType { get; set; }
    }

    internal enum IncrementType
    {
        PreIncrement,
        PostIncrement,
        PreDecrement,
        PostDecrement
    }
}
