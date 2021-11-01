using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Scripting.CScript.Core.Literals
{
    public class DirectFunctionLiteral:FunctionLiteral
    {
        /// <summary>
        /// The Script visitor that is used to interpret this method
        /// </summary>
        private ScriptVisitor visitor;

        /// <summary>
        /// The functionbody of this method
        /// </summary>
        private ITVScriptingParser.FunctionBodyContext body;

        /// <summary>
        /// Initializes a new instance of the FunctionLiteral class
        /// </summary>
        /// <param name="values">the local values that are surrounding the method at the moment of creation</param>
        /// <param name="parent">the parent scope of this method</param>
        /// <param name="arguments">the argument names that are passed to this method</param>
        /// <param name="body">the method body of this method</param>
        public DirectFunctionLiteral(Dictionary<string, object> values, string[] arguments,
            ITVScriptingParser.FunctionBodyContext body):base(values,arguments)
        {
            visitor = new ScriptVisitor(Scope);
            this.body = body;
        }

        protected override object InvokeFunction()
        {
            return ScriptValueHelper.GetScriptValueResult<object>(visitor.Visit(body), false);
        }

        protected override FunctionLiteral CopyMethod(Dictionary<string, object> values, string[] arguments)
        {
            return new DirectFunctionLiteral(values, arguments, body);
        }
    }
}
