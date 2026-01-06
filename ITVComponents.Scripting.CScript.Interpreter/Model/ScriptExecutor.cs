using Antlr4.Runtime;
using ITVComponents.Scripting.CScript.Interpreter.Model.Arguments;
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
        public IExecutorArgument StatusArguments { get; set; }

        public ScriptExecutor(ParserRuleContext sourceElement)
        {
            UpdateRuleContext(sourceElement);
            ChildExecutors = new ChildList(
                whenAdded: c => c.Parent = this,
                whenRemoved: c => c.Parent = null);
        }

        public void UpdateRuleContext(ParserRuleContext sourceElement)
        {
            Pos = sourceElement.SourceInterval.a;
            Length = sourceElement.SourceInterval.Length;
            Line = sourceElement.Start.Line;
            LineCol = sourceElement.Start.Column;
        }

        public string StatusName { get; set; }

        public string ElementName { get; set; }

        public IList<ScriptExecutor> ChildExecutors { get; }

        public ScriptExecutor Parent { get; private set; }

        public int Pos { get; private set; }
        public int Length { get; private set; }

        public int Line { get; private set; }

        public int LineCol { get; private set; }

        public void SetStatusArguments(IExecutorArgument value)
        {
            StatusArguments = value;
        }

        public T GetStatusArguments<T>() where T: IExecutorArgument
        {
            return (T)StatusArguments;
        }

        public ScriptExecutor[] GetElements(string explicitType)
        {
            var retVal = new List<ScriptExecutor>();
            foreach (var child in ChildExecutors)
            {
                if (child.ElementName == explicitType)
                {
                    retVal.Add(child);
                }
            }

            return retVal.ToArray();
        }

        public void ReleaseFromParent()
        {
            if (Parent != null)
            {
                Parent.ChildExecutors.Remove(this);
                Parent = null;
                ElementName = null;
            }
        }
    }
}
