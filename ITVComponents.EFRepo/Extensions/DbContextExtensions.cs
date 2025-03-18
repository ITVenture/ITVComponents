using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Antlr4.Runtime.Sharpen;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.EFRepo.Internal;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.Methods;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Proxies;
namespace ITVComponents.EFRepo.Extensions
{
    public static class DbContextExtensions
    {
        /// <summary>
        /// holds constructed remove methods for later use
        /// </summary>
        private static ConcurrentDictionary<Type, Action<DbContext, object>> constructedRemoveMethods = new ConcurrentDictionary<Type, Action<DbContext, object>>();

        /// <summary>
        /// holds constructed dbset-types for later use
        /// </summary>
        private static ConcurrentDictionary<Type, Type> constructedDbsetTypes = new ConcurrentDictionary<Type, Type>();

        /// <summary>
        /// the raw nethod that is used to do the cascading remove-magic
        /// </summary>
        private static MethodInfo rawMethod = typeof(DbContextExtensions).GetMethod(nameof(RemoveFromSet), BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// the raw-type that is used to construct typed db-sets
        /// </summary>
        private static Type rawDbSetType = typeof(DbSet<>);

        /// <summary>
        /// Gets a proxy object created by the DB-Context for the specified entity-type
        /// </summary>
        /// <typeparam name="T">the type fo which to get a proxy</typeparam>
        /// <param name="context">the db context on which to create the proxy object</param>
        /// <param name="item">the item that is being cloned</param>
        /// <returns>the new created proxyobject</returns>
        public static T GetProxyObject<T>(this DbContext context, T item) where T:class
        {
            T proxyObject = context.Set<T>().CreateProxy();
            item.CopyToModel<T,T>(proxyObject);
            return proxyObject;
        }

        /// <summary>
        /// Performs a cascading delete on the provided item for the given db-context
        /// </summary>
        /// <typeparam name="T">the db-set type that is used to perform the cascade delete</typeparam>
        /// <param name="context">the db-context wrapping the database</param>
        /// <param name="item">the item that must be deleted</param>
        /// <returns>the removed item</returns>
        public static T CascadeDelete<T>(this DbContext context, T item) where T : class
        {

            var dataSet = context.Set<T>();
            var underSets = GetUnderSets(context, typeof(T));
            foreach (var set in underSets)
            {
                object[] items = ((IEnumerable) set.ItemProperty.GetValue(item)).Cast<object>().ToArray();
                items.ForEach(i => set.RemoveMethod(context, i));
            }

            return dataSet.Remove(item).Entity;
        }

        /// <summary>
        /// Creates a non-generic Set-Decorator instance that can be used to manipulate entities of an unknown type in EntityFramewokr Core
        /// </summary>
        /// <param name="context">the target context</param>
        /// <param name="t">the target type</param>
        /// <returns>a DbSet-Decorator that mimics the underlaying DbSet</returns>
        public static IDbSet Set(this DbContext context, Type t)
        {
            Type contextType = context.GetType();
            bool staticMethod = false;
            LogEnvironment.LogDebugEvent($"Creating Decorator for {t}", LogSeverity.Report);
            var method = MethodHelper.GetCapableMethod(contextType, new[] {t}, "Set", ref staticMethod, new object[] { }, out var _);//contextType.GetMethod("Set`1", Type.EmptyTypes);
            LogEnvironment.LogDebugEvent($"Method that was found: {method}", LogSeverity.Report);
            var set = method.Invoke(context, new object[0]);
            var decoratorType = typeof(DbSetDecorator<>).MakeGenericType(t);
            LogEnvironment.LogDebugEvent($"Final Decorator Type: {decoratorType}", LogSeverity.Report);
            var constructor = decoratorType.GetConstructor(new [] {method.ReturnType});
            LogEnvironment.LogDebugEvent($"Decorator Constructor: {constructor}", LogSeverity.Report);
            return (IDbSet) constructor.Invoke(new[] {set});
        }

        /// <summary>
        /// Creates a non-generic Set-Decorator instance that can bei used to manipulate and query an unknown Table in EntityFramework core
        /// </summary>
        /// <param name="context">the target db-context</param>
        /// <param name="name">the set-name</param>
        /// <returns>a DbSet Decorator instance</returns>
        public static IDbSet Set(this DbContext context, string name)
        {
            Type contextType = context.GetType();
            var prop = contextType.GetProperty(name,
                BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);
            var set = prop.GetValue(context);
            var setType = prop.PropertyType;
            if (setType.IsGenericType && setType.GetGenericTypeDefinition() == typeof(DbSet<>))
            {
                var t = setType.GetGenericArguments()[0];
                var decoratorType = typeof(DbSetDecorator<>).MakeGenericType(t);
                var constructor = decoratorType.GetConstructor(new[] { typeof(PropertyInfo), setType });
                LogEnvironment.LogDebugEvent($"Decorator Constructor: {constructor}", LogSeverity.Report);
                return (IDbSet)constructor.Invoke(new[] { prop, set });
            }

            throw new Exception("Provided Property does not return a DbSet");
        }

        /// <summary>
        /// Gets all depending items from the provided model type and generates remove-methods for each of them
        /// </summary>
        /// <param name="context">the db-context on which to delete all the data</param>
        /// <param name="t">the target model-type</param>
        /// <returns>an array containing cascader-objects that can be fired one after another</returns>
        private static Cascader[] GetUnderSets(DbContext context, Type t)
        {
            var properties = t.GetProperties().Where(n => n.PropertyType != typeof(string) && n.PropertyType.GetInterfaces().Contains(typeof(IEnumerable)) && n.PropertyType.IsGenericType).Select(n => new {Property = n, ElementType = n.PropertyType.GetGenericArguments()[0], DbsetType = constructedDbsetTypes.GetOrAdd(n.PropertyType, tp => rawDbSetType.MakeGenericType(tp.GetGenericArguments()[0]))});
            var contextProps = context.GetType().GetProperties(); 
            return (from a in contextProps join b in properties on a.PropertyType equals b.DbsetType select new Cascader {PropertyName = a.Name, ItemProperty = b.Property, RemoveMethod = constructedRemoveMethods.GetOrAdd(b.ElementType, tp => { return (Action<DbContext, object>) Delegate.CreateDelegate(typeof(Action<DbContext, object>), rawMethod.MakeGenericMethod(tp)); })}).ToArray();
        }

        /// <summary>
        /// the raw-remove-method that is used to perform the cascade-delete
        /// </summary>
        /// <typeparam name="T">the element-type for which to perform the delete</typeparam>
        /// <param name="context">the target db-context</param>
        /// <param name="value">the value that must be of type T</param>
        private static void RemoveFromSet<T>(DbContext context, object value) where T : class
        {
            T item = (T)value;
            context.CascadeDelete(item);
        }

        /// <summary>
        /// Dummy class for holding all the cascader-information
        /// </summary>
        private class Cascader
        {
            public string PropertyName { get; set; }

            public PropertyInfo ItemProperty { get; set; }

            public Action<DbContext,object> RemoveMethod { get; set; }
        }
    }
}
