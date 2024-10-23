using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImpromptuInterface;

namespace ITVComponents.DuckTyping.Extensions
{
    public static class TypeExtensions
    {
        public static T WrapType<T>(this Type staticType, Type parameterProvider, Type paramExtractInterface = null,
            int minGenericParameterCount = 0,
            params (string name, Type type)[] fixParameters) where T : class
        {
            var wrapper =
                new GenericMethodsWrapper(staticType, parameterProvider, paramExtractInterface, minGenericParameterCount, fixParameters);
            return wrapper.ActLike<T>();
        }

        public static void ExtendWithStatic(this object obj, Type staticType, Type parameterProvider,
            Type paramExtractInterface = null,
            params (string name, Type type)[] fixParameters)
        {
            if (obj is IActLikeProxy proc)
            {
                if (proc.Original is GenericMethodsWrapper gmw)
                {
                    gmw.ExtendWith(staticType, parameterProvider, paramExtractInterface, fixParameters);
                }
                else
                {
                    throw new InvalidOperationException($"Unable to extend object of Type {((object)proc.Original).GetType()}");
                }
            }
            else
            {
                throw new InvalidOperationException("ActLikeProxy object required for extending methods");
            }
        }
    }
}
