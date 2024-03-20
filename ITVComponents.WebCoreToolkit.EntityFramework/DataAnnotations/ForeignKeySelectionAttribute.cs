using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Help.QueryExtenders;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ForeignKeySelectionAttribute:Attribute
    {
        private readonly Type selectionHelperType;

        /// <summary>
        /// Initializes a new instance of the ForeignKeySelectionAttribute class
        /// </summary>
        /// <param name="selectionHelperType">the Type implementing the IForeignKeySelectorHelper interface that supports custom selections on the given Model-Class</param>
        public ForeignKeySelectionAttribute(Type selectionHelperType)
        {
            if (Array.IndexOf(selectionHelperType.GetInterfaces(), typeof(IForeignKeySelectorHelper)) == -1)
            {
                throw new ArgumentException("Type must implement IForeignKeySelectorHelper",
                    nameof(selectionHelperType));
            }

            if (selectionHelperType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new ArgumentException("Type must have a default-constructor!",
                    nameof(selectionHelperType));
            }

            this.selectionHelperType = selectionHelperType;
        }

        public IForeignKeySelectorHelper CreateTypeInstance(Type finalType)
        {
            var t = GetAccurateType(finalType, selectionHelperType);
            var ct = t.GetConstructor(Type.EmptyTypes);
            var ret = ct.Invoke(Array.Empty<object>());
            return (IForeignKeySelectorHelper)ret;
        }

        private Type GetAccurateType(Type currentEntityType, Type declaredType)
        {
            var tmpT = declaredType;
            if (declaredType.IsGenericTypeDefinition)
            {
                var args = declaredType.GetGenericArguments();
                if (args.Length == 1)
                {
                    try
                    {
                        tmpT = tmpT.MakeGenericType(currentEntityType);
                        return tmpT;
                    }
                    catch
                    {
                    }
                }

                var pt = currentEntityType;
                bool ok = false;
                while (!ok)
                {
                    if (pt == typeof(object))
                    {
                        throw new InvalidOperationException("Unable to find appropriate Type-Arguments");
                    }

                    if (pt.IsGenericType)
                    {
                        var t = pt.GetGenericArguments();
                        if (t.Length == args.Length)
                        {
                            try
                            {
                                tmpT = tmpT.MakeGenericType(t);
                                ok = true;
                            }
                            catch
                            {
                            }
                        }
                        else if (t.Length +1 == args.Length)
                        {
                            var argsWithT = new[] { currentEntityType }.Concat(t).ToArray();
                            try
                            {
                                tmpT = tmpT.MakeGenericType(argsWithT);
                                ok = true;
                            }
                            catch
                            {
                            }
                        }
                    }

                    pt = pt.BaseType;
                }
            }

            return tmpT;
        }
    }
}
