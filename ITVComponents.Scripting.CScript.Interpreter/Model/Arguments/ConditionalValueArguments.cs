using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class ConditionalValueArguments : IExecutorArgument
    {
        public const string Condition = "Condition";
        public const string FirstValue = "FirstValue";
        public const string AlternativeValue = "AlternativeValue";
        public ConditionalValueType Type { get; set; }
    }

    internal enum ConditionalValueType
    {
        IsNull,
        Ternary
    }
}
