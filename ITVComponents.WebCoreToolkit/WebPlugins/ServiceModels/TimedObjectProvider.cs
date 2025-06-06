using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DIServices;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ITVComponents.WebCoreToolkit.WebPlugins.ServiceModels
{
    internal class TimedObjectProvider:IObjectProvider
    {
        private ConcurrentDictionary<string, BufferedObject> objectBuffer =
            new ConcurrentDictionary<string, BufferedObject>();

        public T GetBufferedValue<T>(string key, out DateTime validThrough)
        {
            if (objectBuffer.TryGetValue(key, out var v))
            {
                validThrough = v.ValidThrough;
                return (T)v.Value;
            }

            validThrough = DateTime.MinValue;
            return default(T);
        }

        public T GetBufferedValue<T>(string key, Func<string, T> defaultValue, Func<DateTime> validUntil)
        {
            var buffer = objectBuffer.AddOrUpdate(key,
                k => new BufferedObject(defaultValue(k),  validUntil?.Invoke()??DateTime.Now.AddMinutes(30)),
                (k, x) =>
                {
                    if (x.ValidThrough < DateTime.Now)
                    {
                        var nx = validUntil?.Invoke() ?? DateTime.Now.AddMinutes(30);
                        return new BufferedObject(defaultValue(k), nx);
                    }

                    return x;
                });

            return (T)buffer.Value;
        }

        public T UpdateBufferedValue<T>(string key, T value, DateTime? validThrough = null)
        {
            return (T)objectBuffer.AddOrUpdate(key,
                k => new BufferedObject(value, validThrough ?? DateTime.Now.AddMinutes(30)),
                (k, x) => new BufferedObject(value, validThrough ?? DateTime.Now.AddMinutes(30))).Value;
        }
    }
}
