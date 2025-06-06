using System;
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
        public RunArguments(Action<Dictionary<string, object>> initialize)
        {
            arguments = new();
            initialize(arguments);
        }

        private Dictionary<string, object> arguments;

        public T Argument<T>(string name, bool remove = false)
        {
            T retVal = default;
            if (arguments.ContainsKey(name))
            {
                retVal = (T)arguments[name];
                if (remove)
                {
                    arguments.Remove(name);
                }
            }

            return retVal;
        }

        public void Set(string name, object value) => arguments[name] = value;

        public bool Remove(string name)
        {
            return arguments.Remove(name);
        }
    }
}
