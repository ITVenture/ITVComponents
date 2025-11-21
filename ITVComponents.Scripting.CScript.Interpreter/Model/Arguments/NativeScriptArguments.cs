using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class NativeScriptArguments
    {
        public const string ExpressionArguments = "ExpressionArguments";
        public const string ExpressionBody = "ExpressionBody";
        public const string ExpressionTarget = "ExpressionTarget";

        public string Configuration { get; set; }
        public string Text { get; set; }
        public string NameOfTarget { get; set; }
    }
}
