using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class SwitchCaseArguments : IExecutorArgument
    {
        public const string Statements = "Statements";
        public const string Label = "Label";
        public CaseType Type { get; set; }
    }

    internal enum CaseType
    {
        Standard,
        DefaultLabel
    }
}
