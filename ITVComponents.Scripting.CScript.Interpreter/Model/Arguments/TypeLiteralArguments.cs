using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class TypeLiteralArguments : IExecutorArgument
    {
        public const string GenericArguments = "GenericArguments";

        [JsonIgnore]
        public Type BaseType { get; set; }
        public bool IsByRef { get; set; }
    }
}
