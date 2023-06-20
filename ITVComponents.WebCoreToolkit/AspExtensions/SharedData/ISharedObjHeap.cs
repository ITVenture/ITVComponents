using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dynamitey;
using ITVComponents.WebCoreToolkit.AspExtensions.Helpers;

namespace ITVComponents.WebCoreToolkit.AspExtensions.SharedData
{
    public interface ISharedObjHeap
    {

        public LaxPropertyRef<T> Property<T>(string name) where T: class;

        public LaxPropertyRef<T> Property<T>(string name, bool createDefaultValue) where T : class, new();
    }
}
