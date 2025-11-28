using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Interpreter.Model.Helpers
{
    internal class ChildList: IList<ScriptExecutor>
    {
        private readonly Action<ScriptExecutor> whenAdded;
        private readonly Action<ScriptExecutor> whenRemoved;
        private List<ScriptExecutor> inner = new List<ScriptExecutor>();

        public ChildList(Action<ScriptExecutor> whenAdded, Action<ScriptExecutor> whenRemoved)
        {
            this.whenAdded = whenAdded;
            this.whenRemoved = whenRemoved;
        }
        public IEnumerator<ScriptExecutor> GetEnumerator()
        {
            return inner.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(ScriptExecutor item)
        {
            inner.Add(item);
            whenAdded(item);
        }

        public void Clear()
        {
            var tmp = inner.ToArray();
            inner.Clear();
            foreach (var t in tmp)
            {
                whenRemoved(t);
            }
        }

        public bool Contains(ScriptExecutor item)
        {
            return inner.Contains(item);
        }

        public void CopyTo(ScriptExecutor[] array, int arrayIndex)
        {
            inner.CopyTo(array, arrayIndex);
        }

        public bool Remove(ScriptExecutor item)
        {
            var retVal = inner.Remove(item);
            if (retVal)
            {
                whenRemoved(item);
            }

            return retVal;
        }

        public int Count => inner.Count;
        public bool IsReadOnly => false;
        public int IndexOf(ScriptExecutor item)
        {
            return inner.IndexOf(item);
        }

        public void Insert(int index, ScriptExecutor item)
        {
            inner.Insert(index, item);
            whenAdded(item);
        }

        public void RemoveAt(int index)
        {
            var item = inner[index];
            inner.RemoveAt(index);
            whenRemoved(item);
        }

        public ScriptExecutor this[int index]
        {
            get => inner[index];
            set => inner[index] = value;
        }
    }
}
