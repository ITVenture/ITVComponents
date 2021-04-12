using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Dfa;
using ITVComponents.Scripting.CScript.Optimization;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Core
{
    public partial class ITVScriptingParser
    {
        private static DFA[] decisionToDFA;
        static ITVScriptingParser()
        {
            decisionToDFA = new DFA[_ATN.NumberOfDecisions];
            for (int i = 0; i < _ATN.NumberOfDecisions; i++)
            {
                decisionToDFA[i] = new DFA(_ATN.GetDecisionState(i), i);
            }
        }

        public ITVScriptingParser(ITokenStream input, TextWriter output, TextWriter errorOutput)
            : base(input, output, errorOutput)
        {
            Interpreter = new ParserATNSimulator(this, _ATN, decisionToDFA, new PredictionContextCache());
        }

        public ITVScriptingParser(ITokenStream input) : this(input, Console.Out, Console.Error) { }

        public partial class MemberDotExpressionContext:IScriptSymbol
        {
            private IExecutor preferredExecutor;

            public void SetPreferredExecutor(IExecutor executor)
            {
                preferredExecutor = executor;
            }

            public bool CanInvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck)
            {
                return preferredExecutor != null &&
                       (bypassCompatibilityCheck || preferredExecutor.CanExecute(value, arguments));
            }

            public object InvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck, out bool success)
            {
                success = CanInvokeExecutor(value,arguments,bypassCompatibilityCheck);
                if (success)
                {
                    return preferredExecutor.Invoke(value, arguments);
                }

                return null;
            }
        }

        public partial class MemberIndexExpressionContext:IScriptSymbol
        {
            private IExecutor preferredExecutor;

            public void SetPreferredExecutor(IExecutor executor)
            {
                preferredExecutor = executor;
            }

            public bool CanInvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck)
            {
                return preferredExecutor != null &&
                       (bypassCompatibilityCheck || preferredExecutor.CanExecute(value, arguments));
            }

            public object InvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck, out bool success)
            {
                success = CanInvokeExecutor(value, arguments, bypassCompatibilityCheck);
                if (success)
                {
                    return preferredExecutor.Invoke(value, arguments);
                }

                return null;
            }
        }

        public partial class MemberDotQExpressionContext:IScriptSymbol
        {
            private IExecutor preferredExecutor;

            public void SetPreferredExecutor(IExecutor executor)
            {
                preferredExecutor = executor;
            }

            public bool CanInvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck)
            {
                return preferredExecutor != null &&
                       (bypassCompatibilityCheck || preferredExecutor.CanExecute(value, arguments));
            }

            public object InvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck, out bool success)
            {
                success = CanInvokeExecutor(value, arguments, bypassCompatibilityCheck);
                if (success)
                {
                    return preferredExecutor.Invoke(value, arguments);
                }

                return null;
            }
        }

        public partial class AssignmentOperatorExpressionContext : IScriptSymbol
        {
            private IExecutor preferredExecutor;

            public void SetPreferredExecutor(IExecutor executor)
            {
                preferredExecutor = executor;
            }

            public bool CanInvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck)
            {
                return preferredExecutor != null &&
                       (bypassCompatibilityCheck || preferredExecutor.CanExecute(value, arguments));
            }

            public object InvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck, out bool success)
            {
                success = CanInvokeExecutor(value, arguments, bypassCompatibilityCheck);
                if (success)
                {
                    return preferredExecutor.Invoke(value, arguments);
                }

                return null;
            }
        }

        public partial class MultiplicativeExpressionContext : IScriptSymbol
        {
            private IExecutor preferredExecutor;

            public void SetPreferredExecutor(IExecutor executor)
            {
                preferredExecutor = executor;
            }

            public bool CanInvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck)
            {
                return preferredExecutor != null &&
                       (bypassCompatibilityCheck || preferredExecutor.CanExecute(value, arguments));
            }

            public object InvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck, out bool success)
            {
                success = CanInvokeExecutor(value, arguments, bypassCompatibilityCheck);
                if (success)
                {
                    return preferredExecutor.Invoke(value, arguments);
                }

                return null;
            }
        }

        public partial class AdditiveExpressionContext : IScriptSymbol
        {
            private IExecutor preferredExecutor;

            public void SetPreferredExecutor(IExecutor executor)
            {
                preferredExecutor = executor;
            }

            public bool CanInvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck)
            {
                return preferredExecutor != null &&
                       (bypassCompatibilityCheck || preferredExecutor.CanExecute(value, arguments));
            }

            public object InvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck, out bool success)
            {
                success = CanInvokeExecutor(value, arguments, bypassCompatibilityCheck);
                if (success)
                {
                    return preferredExecutor.Invoke(value, arguments);
                }

                return null;
            }
        }

        public partial class RelationalExpressionContext : IScriptSymbol
        {
            private IExecutor preferredExecutor;

            public void SetPreferredExecutor(IExecutor executor)
            {
                preferredExecutor = executor;
            }

            public bool CanInvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck)
            {
                return preferredExecutor != null &&
                       (bypassCompatibilityCheck || preferredExecutor.CanExecute(value, arguments));
            }

            public object InvokeExecutor(object value, ScriptValue[] arguments, bool bypassCompatibilityCheck, out bool success)
            {
                success = CanInvokeExecutor(value, arguments, bypassCompatibilityCheck);
                if (success)
                {
                    return preferredExecutor.Invoke(value, arguments);
                }

                return null;
            }
        }

        /*public partial class NewExpressionContext : IScriptSymbol
        {
            private IExecutor preferredExecutor;

            public void SetPreferredExecutor(IExecutor executor)
            {
                preferredExecutor = executor;
            }

            public object InvokeExecutor(object value, object[] arguments, out bool success)
            {
                success = preferredExecutor.CanExecute(value, arguments);
                return preferredExecutor.Invoke(value, arguments);
            }
        }*/
    }
}
