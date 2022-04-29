using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Formatting.CustomFormat.Impl
{
    internal class CallbackFormatter:ICustomFormatter
    {
        private readonly Func<object, string> executor;

        public CallbackFormatter(string hint, Func<object,string> executor)
        {
            Hint = hint;
            this.executor = executor;
        }

        public string ApplyFormat(string name, object rawValue, Func<string, string, string, object> argumentsCallback)
        {
            return executor(rawValue);
        }

        public string Hint { get; }
    }
}
