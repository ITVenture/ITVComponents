using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Interpreter.Model.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model
{
    public class ScriptExecutor
    {
        private object statusArguments;

        public ScriptExecutor(ParserRuleContext sourceElement)
        {
            Pos = sourceElement.SourceInterval.a;
            Length = sourceElement.SourceInterval.Length;
            Line = sourceElement.Start.Line;
            LineCol = sourceElement.Start.Column;
            ChildExecutors = new ChildList(
                whenAdded: c => c.Parent = this,
                whenRemoved: c => c.Parent = null);
        }

        public string StatusName { get; set; }

        public string ElementName { get; set; }

        public IList<ScriptExecutor> ChildExecutors { get; }

        public ScriptExecutor Parent { get; private set; }

        public int Pos { get; }
        public int Length { get; }

        public int Line { get; }

        public int LineCol { get; }

        public void SetStatusArguments(object value)
        {
            statusArguments = value;
        }

        public T GetStatusArguments<T>()
        {
            return (T)statusArguments;
        }
    }
}
