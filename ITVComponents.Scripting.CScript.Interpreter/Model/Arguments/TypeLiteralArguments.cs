using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class TypeLiteralArguments
    {
        public const string GenericArguments = "GenericArguments";

        public Type BaseType { get; set; }
        public bool IsByRef { get; set; }
    }
}
