using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using Microsoft.CSharp.RuntimeBinder;
using Binder = System.Reflection.Binder;

namespace ITVComponents.DuckTyping
{
    public static class DuckTypeWrapper
    {
        /// <summary>
        /// Creates a Duck-Instance for a specific Type
        /// </summary>
        /// <typeparam name="T">the Target - Interface you want to wrap</typeparam>
        /// <param name="o">the instance that needs to be wrapped</param>
        /// <param name="otherTypes">a list of other interface-types that have to be implemented as well</param>
        /// <returns>a wrapper instance of the given interface</returns>
        public static T ActLike<T>(this object o, params Type[] otherTypes) where T : class
        {
            return ImpromptuInterface.Impromptu.ActLike<T>(o, otherTypes);
        }

        /// <summary>
        /// Creates a Duck-Instance for a specific Type
        /// </summary>
        /// <typeparam name="T">the Target - Interface you want to wrap</typeparam>
        /// <param name="o">the instance that needs to be wrapped</param>
        /// <returns>a wrapper instance of the given interface</returns>
        public static T ActLike<T>(this object o) where T : class
        {
            return ImpromptuInterface.Impromptu.ActLike<T>(o);
        }
    }
}
