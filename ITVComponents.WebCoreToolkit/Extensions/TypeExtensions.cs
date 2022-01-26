using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class TypeExtensions
    {
        public static IEnumerable<Type> GetBaseTypes(this Type t)
        {
            var ret = t;
            while (ret != null)
            {
                ret = ret.BaseType;
                if (ret != null && ret != typeof(object))
                {
                    yield return ret;
                }
                else
                {
                    yield break;
                }
            }
        }
    }
}
