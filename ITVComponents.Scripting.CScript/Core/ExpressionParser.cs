using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Buffering;
using ITVComponents.Scripting.CScript.Exceptions;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Core
{
    public static class ExpressionParser
    {
        /// <summary>
        /// Holds all previously parsed expressions
        /// </summary>
        private static ConcurrentDictionary<string, Lazy<ExpressionCompiler>> parsedExpressions = new ConcurrentDictionary<string, Lazy<ExpressionCompiler>>();

        /// <summary>
        /// Holds all blocks that were evaluated before
        /// </summary>
        private static ConcurrentDictionary<string, Lazy<ExpressionCompiler>> parsedPrograms = new ConcurrentDictionary<string, Lazy<ExpressionCompiler>>();

        /// <summary>
        /// Parses one single Expression
        /// </summary>
        /// <param name="expression">the target expression to execute</param>
        /// <param name="variables">the initial variables that are provided to the expression</param>
        /// <param name="scopeInitializer">an initializer that can be used to provide special methods and values to the initial scope</param>
        /// <returns>the evaluation result</returns>
        public static object Parse(string expression, IDictionary<string, object> variables, InitializeScopeVariables scopeInitializer = null)
        {
            ScriptVisitor visitor;
            using (var session = InterpreterBuffer.GetReplInstance(variables, scopeInitializer, out visitor))
            {
                return Parse(expression, session);
            }
        }

        /// <summary>
        /// Parses one single Expression-Block
        /// </summary>
        /// <param name="expression">the target expression to execute</param>
        /// <param name="variables">the initial variables that are provided to the expression</param>
        /// <param name="scopeInitializer">an initializer that can be used to provide special methods and values to the initial scope</param>
        /// <returns>the evaluation result</returns>
        public static object ParseBlock(string expression, IDictionary<string, object> variables, InitializeScopeVariables scopeInitializer = null)
        {
            ScriptVisitor visitor;
            using (var session = InterpreterBuffer.GetReplInstance(variables, scopeInitializer, out visitor))
            {
                return ParseBlock(expression, session);
            }
        }

        /// <summary>
        /// Parses one single Expression
        /// </summary>
        /// <param name="expression">the target expression to execute</param>
        /// <param name="implicitContext">implicit context that is used for the interpreter-session that is used for the expression</param>
        /// <param name="scopeInitializer">an initializer that can be used to provide special methods and values to the initial scope</param>
        /// <returns>the evaluation result</returns>
        public static object Parse(string expression, object implicitContext, InitializeScopeVariables scopeInitializer = null)
        {
            ScriptVisitor visitor;
            using (var session = InterpreterBuffer.GetReplInstance(implicitContext, scopeInitializer, out visitor))
            {
                return Parse(expression, session);
            }
        }

        /// <summary>
        /// Parses one single Expression-Block
        /// </summary>
        /// <param name="expression">the target expression to execute</param>
        /// <param name="implicitContext">implicit context that is used for the interpreter-session that is used for the codeblock</param>
        /// <param name="scopeInitializer">an initializer that can be used to provide special methods and values to the initial scope</param>
        /// <returns>the evaluation result</returns>
        public static object ParseBlock(string expression, object implicitContext, InitializeScopeVariables scopeInitializer = null)
        {
            ScriptVisitor visitor;
            using (var session = InterpreterBuffer.GetReplInstance(implicitContext, scopeInitializer, out visitor))
            {
                return ParseBlock(expression, session);
            }
        }

        /// <summary>
        /// Parses a Block inside an open ReplSession
        /// </summary>
        /// <param name="expression">the Expression-Block that must be executed</param>
        /// <param name="replSession">the current repl-session</param>
        /// <returns>the result of the Execution-block</returns>
        public static object ParseBlock(string expression, IDisposable replSession)
        {
            if (replSession is InterpreterBuffer.RunnerItem rii)
            {
                ITVScriptingBaseVisitor<ScriptValue> visitor = InterpreterBuffer.GetInterpreter(rii);
                ITVScriptingParser.ProgramContext executor = GetProgramTree(expression);
                ScriptValue retVal = visitor.VisitProgram(executor);
                return ScriptValueHelper.GetScriptValueResult<object>(retVal, false);
            }

            return ParseBlock(expression, replSession, null);
        }

        /// <summary>
        /// Parses an expression for a repl-session
        /// </summary>
        /// <param name="expression">the expression to parse</param>
        /// <param name="replSession">the repl-session that is currently running</param>
        /// <returns>the result of the provided expression</returns>
        public static object Parse(string expression, IDisposable replSession)
        {
            if (replSession is InterpreterBuffer.RunnerItem rii)
            {
                ITVScriptingBaseVisitor<ScriptValue> visitor = InterpreterBuffer.GetInterpreter(rii);
                ITVScriptingParser.ExpressionStatementContext executor = GetExpressionTree(expression);
                ScriptValue retVal = visitor.Visit(executor);
                return ScriptValueHelper.GetScriptValueResult<object>(retVal, true);
            }

            return Parse(expression, replSession, null);
        }

        /// <summary>
        /// Begins a repl - session
        /// </summary>
        /// <param name="baseValues">the base values that are used for the current session</param>
        /// <param name="scopePreparer">a callback that is used to prepare the repl session variables</param>
        /// <returns>a value that can be used to end this repl - session</returns>
        public static IDisposable BeginRepl(IDictionary<string, object> baseValues, InitializeScopeVariables scopePreparer)
        {
            ScriptVisitor visitor;
            return InterpreterBuffer.GetReplInstance(baseValues, scopePreparer, out visitor);
        }

        /// <summary>
        /// Gets a value indicating whether the demanded object is a repl-session
        /// </summary>
        /// <param name="session">the potential session object to use for interpreting an expression or block</param>
        /// <returns>a value indicating whether the provided object is a repl-session</returns>
        public static bool IsReplSession(IDisposable session)
        {
            return session is InterpreterBuffer.RunnerItem;
        }

        /// <summary>
        /// Gets the ExpressionTree for a specific Expression
        /// </summary>
        /// <param name="expression">the expression for which to get the tree</param>
        /// <returns>a ScriptParser containing the entire ExpressionTree for the provided expression</returns>
        private static ITVScriptingParser.ExpressionStatementContext GetExpressionTree(string expression)
        {
            var tmp = parsedExpressions.GetOrAdd(expression,
                ex =>
                    new Lazy<ExpressionCompiler>(() => new ExpressionCompiler(expression, ExpressionMode.Expression),
                        LazyThreadSafetyMode.ExecutionAndPublication));
            if (!tmp.Value.Successful)
            {
                throw new ScriptException(tmp.Value.Errors);
            }

            return tmp.Value.SingleExpression;
        }

        /// <summary>
        /// Gets the ExpressionTree for a specific Expression
        /// </summary>
        /// <param name="expression">the expression for which to get the tree</param>
        /// <returns>a ScriptParser containing the entire ExpressionTree for the provided expression</returns>
        private static ITVScriptingParser.ProgramContext GetProgramTree(string expression)
        {
            var tmp = parsedPrograms.GetOrAdd(expression,
                ex =>
                    new Lazy<ExpressionCompiler>(() => new ExpressionCompiler(expression, ExpressionMode.Program),
                        LazyThreadSafetyMode.ExecutionAndPublication));
            if (!tmp.Value.Successful)
            {
                throw new ScriptException(tmp.Value.Errors);
            }

            return tmp.Value.Program;
        }

        private enum ExpressionMode
        {
            Expression,
            Program
        }

        private class ExpressionCompiler
        {
            /// <summary>
            /// the error listener that is used to provide errors on expressions
            /// </summary>
            private static ErrorListener listener = new ErrorListener();

            /// <summary>
            /// the parser object that is used to process this expression
            /// </summary>
            private ITVScriptingParser parser;

            /// <summary>
            /// the assigned program context
            /// </summary>
            private ITVScriptingParser.ProgramContext program;

            /// <summary>
            /// the assigned expression context
            /// </summary>
            private ITVScriptingParser.ExpressionStatementContext singleExpression;

            /// <summary>
            /// initializes this expression holder
            /// </summary>
            /// <param name="expression">the expression that is being parsed</param>
            /// <param name="mode">the parsing mode of the expression</param>
            public ExpressionCompiler(string expression, ExpressionMode mode)
            {
                var lex = new ITVScriptingLexer(new AntlrInputStream(expression));
                parser = new ITVScriptingParser(new CommonTokenStream(lex));
                parser.RemoveErrorListeners();
                parser.AddErrorListener(listener);
                //parser.ErrorHandler = new BailErrorStrategy();
                object obj;
                if (mode == ExpressionMode.Expression)
                {
                    obj = SingleExpression;
                }
                else
                {
                    obj = Program;
                }

                Successful = parser.NumberOfSyntaxErrors == 0;
                SuspectLine = listener.SuspectLine;
                Errors = listener.GetAllErrors();
            }
            
            public bool Successful { get; private set; }

            public string Errors { get; private set; }

            public int SuspectLine { get; }

            public ITVScriptingParser.ProgramContext Program
            {
                get
                {
                    if (program == null)
                    {
                        program = parser.program();
                    }

                    return program;
                }
            }

            public ITVScriptingParser.ExpressionStatementContext SingleExpression
            {
                get
                {
                    if (singleExpression == null)
                    {
                        singleExpression = parser.expressionStatement();
                    }

                    return singleExpression;
                }
            }
        }
    }
}
