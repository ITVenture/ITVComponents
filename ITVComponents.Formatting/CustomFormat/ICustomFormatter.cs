using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Formatting.CustomFormat
{
    public interface ICustomFormatter
    {
        string ApplyFormat(string name, object rawValue, Func<string, string, string, object> argumentsCallback);
        string Hint { get;  }
    }
}
