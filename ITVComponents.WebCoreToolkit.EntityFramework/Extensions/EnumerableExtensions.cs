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
            var typed = source.Cast<ForeignKeyData<TKey>>();

            // When the requested key type matches the source key type (the common case),
            // we hand the sequence through unchanged via an object-hop. Compiler does not
            // accept `(IEnumerable<ForeignKeyData<T>>)typed` directly because T and TKey
            // are independent generic parameters from its point of view.
            if (typeof(TKey) == typeof(T))
            {
                return (IEnumerable<ForeignKeyData<T>>)(object)typed;
            }

            return typed.Select(n => new ForeignKeyData<T>
            {
                Key = ConvertKey<TKey, T>(n.Key),
                Label = n.Label,
                FullRecord = n.FullRecord
            });
        }

        private static T ConvertKey<TKey, T>(TKey key)
        {
            // Direct cast via object hop. The C# compiler refuses `(T)key` for unrelated
            // generic parameters; going through object lets the runtime do the type check.
            // This handles the common Nullable<U>/U pair (e.g. TKey=int -> T=int?, since
            // a boxed int matches `is int?`) and reference-type identity.
            if (key is T direct)
            {
                return direct;
            }
            if (key is null)
            {
                return default!;
            }
            return (T)TypeConverter.Convert(key, typeof(T))!;
        }
    }
}
