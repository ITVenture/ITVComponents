using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core;

namespace ITVComponents.Scripting.CScript.Interpreter
{
    public static class ExpressionInterpreter
    {
        public static ITVScriptingParser.ExpressionStatementContext ParseExpression(string expression)
        {
            var parser =
                ExpressionParser.GetRawExpressionTree(expression, ExpressionParser.ExpressionMode.Expression) as
                    ITVScriptingParser.ExpressionStatementContext;
            return null;
        }
    }
}
