using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter
{
    public static class ScriptExecutionStatus
    {
        public const string TypeLiteral = "TypeLiteral";
        public const string Literal = "Literal";
        public const string FunctionLiteral = "FunctionExpression";
        public const string ObjectLiteral = "ObjectLiteral";
        public const string ExecutionSwitch = "ExecutionSwitch";
        public const string NativeLiteralExecute = "NativeLiteralExecute";
        public const string NativeExpressionExecute = "NativeExpressionExecute";
        public const string Void = "Void";
        public const string Operation = "Operation";
        public const string Assignment = "Assignment";
        public const string Identifier = "Identifier";
        public const string Indexer = "Indexer";
        public const string MemberAccess = "MemberAccess";
        public const string ConditionalValue = "ConditionalValue";
        public const string ValueIsType = "ValueIsType";
        public const string New = "New";
        public const string Negate = "Negate";
        public const string Increment = "Increment";
        public const string Compare = "Compare";
    }
}
