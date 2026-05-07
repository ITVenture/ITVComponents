using ITVComponents.Helpers;
using ITVComponents.TypeConversion;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Extensions
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<ForeignKeyData<T>> CastForeignKey<T>(this IEnumerable source, Type expectedKeyType)
        {
            var method = LambdaHelper.GetMethodInfo(() => CastForeignKey<object, object>(null))
                .GetGenericMethodDefinition();
            var iml = method.MakeGenericMethod(expectedKeyType, typeof(T));
            return (IEnumerable<ForeignKeyData<T>>)iml.Invoke(null, [source]);
        }

        public static IEnumerable<ForeignKeyData<T>> CastForeignKey<TKey, T>(this IEnumerable source)
        {
            return source.Cast<ForeignKeyData<TKey>>().Select(n => new ForeignKeyData<T>
            {
                Key = (T)TypeConverter.Convert(n.Key, typeof(T)),
                Label = n.Label,
                FullRecord = n.FullRecord
            });
        }
    }
}
