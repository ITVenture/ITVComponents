using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DIServices;

namespace ITVComponents.WebCoreToolkit.WebPlugins.ServiceModels
{
    internal class DummyObjectProvider:IObjectProvider
    {
        public T GetBufferedValue<T>(string key, out DateTime validThrough)
        {
            validThrough = DateTime.MinValue;
            return default(T);
        }

        public T GetBufferedValue<T>(string key, Func<string, T> defaultValue, Func<DateTime> validThrough)
        {
            return defaultValue(key);
        }

        public T UpdateBufferedValue<T>(string key, T value, DateTime? validThrough = null)
        {
            return value;
        }
    }
}
