using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class CompareArguments : IExecutorArgument
    {
        public const string Left = "Left";
        public const string Right = "Right";
        public ComparisonType ComparisonType { get; set; }
    }

    internal enum ComparisonType
    {
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Equal,
        NotEqual
    }
}
