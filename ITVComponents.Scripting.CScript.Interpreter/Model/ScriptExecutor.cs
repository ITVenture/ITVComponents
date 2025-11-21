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

        public ScriptExecutor(int pos, int length)
        {
            Pos = pos;
            Length = length;
        }

        public string StatusName { get; set; }

        public string ElementName { get; set; }

        public List<ScriptExecutor> ChildExecutors { get; } = new();

        public ScriptExecutor Parent { get; set; }

        public int Pos { get; }
        public int Length { get; }

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
