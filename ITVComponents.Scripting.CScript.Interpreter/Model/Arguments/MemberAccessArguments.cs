using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Arguments
{
    internal class MemberAccessArguments
    {
        public const string BaseValue = "BaseValue";

        public const string ExplicitType = "ExplicitType";
        public const string MethodArguments = "MethodArguments";
        public const string GenericArguments = "GenericArguments";
        public string MemberName { get; set; }

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
