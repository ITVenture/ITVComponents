using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class UnaryOpArguments : IExecutorArgument
    {
        public const string BaseValue = "baseValue";

        public UnaryOperator Operator { get; set; }
    }

    public enum UnaryOperator
    {
        Plus,
        Minus,
        Not,
        Invert
    }
}
