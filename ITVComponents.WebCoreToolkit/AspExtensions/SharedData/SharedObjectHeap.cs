using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Helpers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ITVComponents.WebCoreToolkit.AspExtensions.SharedData
{
    internal class SharedObjectHeap:ISharedObjHeap
    {
        private ConcurrentDictionary<string, object> properties = new();
        public LaxPropertyRef<T> Property<T>(string name) where T : class
        {
            var tmp = properties.GetOrAdd(name, n => new LaxPropertyRef<T>());
            if (tmp is not LaxPropertyRef<T> ret)
            {
                throw new InvalidOperationException("Property already declared as different type!");
            }

            return ret;
        }

        public LaxPropertyRef<T> Property<T>(string name, bool createDefaultValue) where T : class, new()
        {
            var tmp = properties.GetOrAdd(name, n => new PropertyRef<T>(createDefaultValue));
            if (tmp is not LaxPropertyRef<T> ret)
            {
                throw new InvalidOperationException("Property already declared as different type!");
            }

            return ret;
        }
    }
}
