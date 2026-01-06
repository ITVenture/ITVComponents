using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class MemberAccessArguments : IExecutorArgument
    {
        public const string BaseValue = "BaseValue";

        public const string ExplicitType = "ExplicitType";
        public const string MethodArguments = "MethodArguments";
        public const string GenericArguments = "GenericArguments";
        public const string DirectMethod = "DirectMethod";
        public List<string> MemberPath { get; set; } = new List<string>();

        public MemberAccessType ExpectedMemberType { get; set; } = MemberAccessType.PropertyOrFieldOrEvent;
        public bool NullPropageted { get; set; } = false;
        public bool Indicator { get; set; } = false;
    }

    internal enum MemberAccessType
    {
        PropertyOrFieldOrEvent,
        Method
    }
}
