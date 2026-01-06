using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;

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
        //public const string Identifier = "Identifier";
        public const string Indexer = "Indexer";
        public const string MemberAccess = "MemberAccess";
        public const string ConditionalValue = "ConditionalValue";
        public const string ValueIsType = "ValueIsType";
        public const string New = "New";
        public const string Negate = "Negate";
        public const string Increment = "Increment";
        public const string Compare = "Compare";
        public const string UnaryOperation = "UnaryOperation";
        public const string ExpressionSequence = "ExpressionSequence";
        public const string Block = "Block";
        public const string TryStatement = "TryStatement";
        public const string ThrowStatement = "ThrowStatement";
        public const string SwitchCase = "SwitchCase";
        public const string Switch = "Switch";
        public const string ReturnStatement = "ReturnStatement";
        public const string LoopJump = "LoopJump";
        public const string Loop = "Loop";
        public const string IfBlock = "IfBlock";
        public const string IfCondition = "IfCondition";
        public const string EmptyStatement = "EmptyStatement";
    }
}
