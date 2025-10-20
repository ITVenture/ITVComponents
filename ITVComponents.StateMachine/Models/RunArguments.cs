using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ObjectiveC;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.TypeConversion;

namespace ITVComponents.StateMachine.Models
{
    public class RunArguments
    {
        public RunArguments(Action<ConcurrentDictionary<string, object>> initialize)
        {
            arguments = new();
            initialize(arguments);
        }

        private ConcurrentDictionary<string, object> arguments;

        public T Argument<T>(string name, bool remove = false)
        {
            T retVal = default;
            object tmp;
            var isOk = !remove ? arguments.TryGetValue(name, out tmp) : arguments.TryRemove(name, out tmp);
            if (isOk)
            {
                retVal = (T)tmp;
            }

            return retVal;
        }

        public void Set<T>(string name, T value, Func<T,T,T> updateExistingValue = null)
        {
            if (updateExistingValue == null)
            {
                updateExistingValue = (_, v) => v;
            }

            arguments.AddOrUpdate(name, value, (n, o) => updateExistingValue((T)o, value));
        }

        public bool Remove(string name)
        {
            return arguments.Remove(name, out _);
        }
    }
}
