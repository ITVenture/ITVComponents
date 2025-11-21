using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class OperationArguments
    {
        public const string LeftOperand = "LeftOperand";
        public const string RightOperand = "RightOperand";

        public BaseOperations Operation { get; set; }
    }

    internal enum BaseOperations
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulus,
        And,
        Or,
        Xor,
        LeftShift,
        RightShift,
        None
    }
}
