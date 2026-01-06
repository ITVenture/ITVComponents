using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Interpreter.Model;

namespace ITVComponents.Scripting.CScript.Interpreter
{
    public static class ExpressionInterpreter
    {
        public static ScriptExecutor ParseExpression(string expression)
        {
            var parser =
                ExpressionParser.GetRawExpressionTree(expression, ExpressionParser.ExpressionMode.Expression) as
                    ITVScriptingParser.ExpressionStatementContext;
            var compiler = new ExpressionExecutorBuilder();
            return compiler.Visit(parser);
        }

        public static ScriptExecutor ReadScriptFile(string fileName)
        {
            var parser = ExpressionParser.GetExpressionTreeFromFile(fileName, out _, out _, out _);
            var compiler = new ExpressionExecutorBuilder();
            return compiler.Visit(parser);
        }
    }
}
