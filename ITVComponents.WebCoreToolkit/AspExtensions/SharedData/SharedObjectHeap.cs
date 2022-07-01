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
        public PropertyRef<T> Property<T>(string name)
        {
            var tmp = properties.GetOrAdd(name, n => new PropertyRef<T>());
            if (tmp is not PropertyRef<T> ret)
            {
                throw new InvalidOperationException("Property already declared as different type!");
            }

            return ret;
        }
    }
}
