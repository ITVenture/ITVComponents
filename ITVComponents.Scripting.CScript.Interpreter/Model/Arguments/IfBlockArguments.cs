using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class IfBlockArguments : IExecutorArgument
    {
        public const string ElseBlock = "Else";
        public const string Condition = "Condition";
        public const string Body = "Body";
        public const string Alternative = "Alternative";
    }
}
