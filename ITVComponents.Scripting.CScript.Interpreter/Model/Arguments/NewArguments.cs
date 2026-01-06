using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class NewArguments : IExecutorArgument
    {
        public const string Type = "Type";
        public const string ConstructorArguments = "ConstructorArguments";
        public const string InitialValues = "InitialValues";
        public const string GenericArguments = "GenericArguments";
        public bool UseDefaultConstructor { get; set; } = false;
    }
}
