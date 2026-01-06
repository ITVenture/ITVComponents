using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class LoopArguments : IExecutorArgument
    {
        public const string Condition = "Condition";
        public const string Body = "Body";
        public const string Initializer = "Initializer";
        public const string Iterator = "Iterator";
        public const string ItemVariable = "ItemVariable";
        public LoopType Type { get; set; }
    }

    internal enum LoopType
    {
        While,
        DoWhile,
        For,
        ForEach
    }
}
