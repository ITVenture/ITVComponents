using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.DIServices
{
    public interface IObjectProvider
    {
        T GetBufferedValue<T>(string key, out DateTime validThrough);

        T GetBufferedValue<T>(string key, Func<string, T> defaultValue, Func<DateTime> validThrough);

        T UpdateBufferedValue<T>(string key, T value, DateTime? validThrough = null);
    }
}
