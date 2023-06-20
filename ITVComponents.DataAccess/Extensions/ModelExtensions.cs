using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using ITVComponents.Cloning;
using ITVComponents.Cloning.Model;
using ITVComponents.DataAccess.DataAnnotations;
using ITVComponents.TypeConversion;
using Microsoft.CodeAnalysis;

namespace ITVComponents.DataAccess.Extensions
{
    /// <summary>
    /// Model extensions enabling a Model to create implicitly
    /// </summary>
    public static class ModelExtensions
    {
        /// <summary>
        /// Holds buffered setter methods for the assignment of ViewModel to model
        /// </summary>
        private static ConcurrentDictionary<Type, ConcurrentDictionary<Type, Delegate>> assignmentDbActions = new ConcurrentDictionary<Type, ConcurrentDictionary<Type, Delegate>>();

        /// <summary>
        /// Holds buffered setter methods for the assignment of model to an other
        /// </summary>
        private static ConcurrentDictionary<Type, ConcurrentDictionary<Type, Delegate>> modelCopyActions = new ConcurrentDictionary<Type, ConcurrentDictionary<Type, Delegate>>();

        /// <summary>
        /// Holds buffered setter methods for the assignment of model to ViewModel
        /// </summary>
        private static ConcurrentDictionary<Type, ConcurrentDictionary<Type, Delegate>> toViewModelActions = new ConcurrentDictionary<Type, ConcurrentDictionary<Type, Delegate>>();

        /// <summary>
        /// Creates an instance of the given ViewModelType and sets all correspondig Properties on it
        /// </summary>
        /// <typeparam name="T">the ViewModel-Type</typeparam>
        /// <typeparam name="TDbModel">the dbmodel for which to create a viewModel</typeparam>
        /// <param name="modelObject">the Data, that was read from the database</param>
        /// <param name="postProcessAction">a user defined action that is performed after converting the requested data to a viewModel object</param>
        /// <returns>a Viewmodel that contains all Data that was read from the model</returns>
        public static T ToViewModel<TDbModel, T>(this TDbModel modelObject, Action<TDbModel, T> postProcessAction = null) where T : class, new()
            where TDbModel : class
        {
            return ToViewModel(modelObject, t => null, postProcessAction);
        }

        /// <summary>
        /// Creates an instance of the given ViewModelType and sets all correspondig Properties on it
        /// </summary>
        /// <typeparam name="T">the ViewModel-Type</typeparam>
        /// <typeparam name="TDbModel">the dbmodel for which to create a viewModel</typeparam>
        /// <param name="modelObject">the Data, that was read from the database</param>
        /// <param name="requestObjectInstance">offsers a customValueSoruce attribute to access custom required objects</param>
        /// <param name="postProcessAction">a user defined action that is performed after converting the requested data to a viewModel object</param>
        /// <returns>a Viewmodel that contains all Data that was read from the model</returns>
        public static T ToViewModel<TDbModel, T>(this TDbModel modelObject, Func<Type, object> requestObjectInstance, Action<TDbModel, T> postProcessAction = null, Func<string,bool> propertyFilter = null) where T : class, new()
                                                                                                                where TDbModel : class
        {
            propertyFilter ??= (s => true);
            if (modelObject == null)
            {
                return null;
            }

            T retVal = new T();
            Type viewType = typeof(T);
            PropertyInfo[] allViewProperties = viewType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            var customValueProps = (from t in allViewProperties
                                    where t.CanWrite && 
                                          t.GetIndexParameters().Length == 0 && 
                                          Attribute.IsDefined(t, typeof(CustomValueSourceAttribute), true)
                                    select new { Property = t, Attributes = System.Linq.Enumerable.Cast<CustomValueSourceAttribute>(Attribute.GetCustomAttributes(t, typeof(CustomValueSourceAttribute), true)).First() }).ToArray();
            var hashCalc = customValueProps.FirstOrDefault(n => n.Attributes is ModelHashAttribute);
            var action = GetActionFor<TDbModel, T>(toViewModelActions, SelectToViewModelProps<TDbModel, T>, true);
            action(modelObject, retVal, propertyFilter);
            foreach (var customValue in customValueProps)
            {
                if (customValue != hashCalc)
                {
                    var computed = customValue.Attributes.GetCustomValueFor(modelObject, requestObjectInstance);
                    customValue.Property.SetValue(retVal, computed, null);
                }
            }

            postProcessAction?.Invoke(modelObject, retVal);
            if (hashCalc != null)
            {
                var allProps = allViewProperties.Where(n => n.Name != hashCalc.Property.Name && n.CanRead).OrderBy(n => n.Name);
                var raw = string.Join(";", allProps.Select(n => n.GetValue(retVal)?.ToString()));
                //Console.WriteLine(raw);
                hashCalc.Property.SetValue(retVal, hashCalc.Attributes.GetCustomValueFor(raw, requestObjectInstance), null);
            }

            return retVal;
        }

        /// <summary>
        /// Creates an instance of the given ViewModelType and sets all correspondig Properties on it
        /// </summary>
        /// <typeparam name="T">the ViewModel-Type</typeparam>
        /// <typeparam name="TDbModel">the dbmodel for which to create a viewModel</typeparam>
        /// <param name="modelObject">the Data, that was read from the database</param>
        /// <param name="target">the target object to which to copy the entire content of the source object</param>
        /// <param name="postProcessAction">a user defined action that is performed after converting the requested data to a viewModel object</param>
        /// <returns>a Viewmodel that contains all Data that was read from the model</returns>
        public static void CopyToModel<TDbModel, T>(this TDbModel modelObject, T target, Action<TDbModel, T> postProcessAction = null, Func<string,bool> propertyFilter = null) where T : class
                                                                                                                where TDbModel : class
        {
            propertyFilter ??= (s => true);
            if (modelObject == null || target == null)
            {
                return;
            }

            var action = GetActionFor<TDbModel, T>(modelCopyActions, SelectCopyToVmProps<TDbModel, T>, false);
            action(modelObject, target, propertyFilter);
            postProcessAction?.Invoke(modelObject, target);
        }

        /// <summary>
        /// Creates a list of Include - Columns depending on the ModelType
        /// </summary>
        /// <typeparam name="T">the ModelType</typeparam>
        /// <returns>a value of strings that might have been updated</returns>
        public static string[] GetModelUpdateColumns<T>()
        {
            Type viewType = typeof(T);
            PropertyInfo[] allViewProperties = viewType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            return (from t in allViewProperties
                    where
                        t.GetIndexParameters().Length == 0 &&
                        t.CanWrite &&
                        !Attribute.IsDefined(t, typeof(IgnorePropertyAttribute), true)
                    select t.Name).ToArray();
        }

        /// <summary>
        /// Creates an instance of the given ViewModelType and sets all correspondig Properties on it
        /// </summary>
        /// <typeparam name="T">the ViewModel-Type</typeparam>
        /// <typeparam name="TDbModel">the dbmodel for which to create a viewModel</typeparam>
        /// <param name="modelObject">the Data, that was read from the database</param>
        /// <param name="target">the target object to which to copy the entire content of the source object</param>
        /// <returns>a Viewmodel that contains all Data that was read from the model</returns>
        public static void CopyToDbModel<TViewModel, TDbModel>(this TViewModel modelObject, TDbModel target, Func<string, bool> propertyFilter = null) where TViewModel : class
                                                                                                                where TDbModel : class
        {
            propertyFilter ??= (s => true);
            if (modelObject == null || target == null)
            {
                return;
            }

            var action = GetActionFor<TViewModel, TDbModel>(assignmentDbActions, CreateDbCopyMethod<TViewModel, TDbModel>, false);
            action(modelObject, target, propertyFilter);
        }

        private static IEnumerable<AssignmentHolder> SelectCopyToVmProps<TModel, TViewModel>()
        {
            Type modelType = typeof(TModel);
            Type viewType = typeof(TViewModel);
            PropertyInfo[] allModelProperties =
                modelType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            PropertyInfo[] allViewProperties = viewType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            return (from t in allViewProperties
                    join a in allModelProperties on t.Name equals a.Name
                    where t.CanWrite && t.GetIndexParameters().Length == 0 && !Attribute.IsDefined(t, typeof(IgnorePropertyAttribute), true) && !Attribute.IsDefined(a, typeof(IgnorePropertyAttribute), true)
                    select new AssignmentHolder { 
                        Destination = t, 
                        Source = a, 
                        PropType = t.PropertyType, 
                        SpecifyDateTimeAsUtc = Attribute.IsDefined(t, typeof(UtcDateTimeAttribute)) || Attribute.IsDefined(a, typeof(UtcDateTimeAttribute)),
                        DestinationNullable = t.PropertyType.IsGenericType &&
                                              t.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>),
                        SourceNullable = a.PropertyType.IsGenericType &&
                                         a.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)
                    });
        }

        /// <summary>
        /// Selectes ToViewModel properties for the given model and viewmodel
        /// </summary>
        /// <typeparam name="TModel">the db-model from which to select the data</typeparam>
        /// <typeparam name="TViewModel">the view-model to which the target-data is assigned</typeparam>
        /// <returns>a set of assignment-holders that are used to construct an assignment-lambda</returns>
        private static IEnumerable<AssignmentHolder> SelectToViewModelProps<TModel, TViewModel>()
        {
            Type modelType = typeof(TModel);
            Type viewType = typeof(TViewModel);
            PropertyInfo[] allModelProperties =
                modelType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            PropertyInfo[] allViewProperties = viewType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            Type dateTime = typeof(DateTime);
            var tmp = (from t in allViewProperties
                       join a in allModelProperties on t.Name equals a.Name
                       where t.CanWrite && t.GetIndexParameters().Length == 0 && 
                             !Attribute.IsDefined(t, typeof(IgnorePropertyAttribute), true) && 
                             !Attribute.IsDefined(a, typeof(IgnorePropertyAttribute), true)
                       select new { Property = t, SourceProperty = a, UseUtcDateTime = 
                           Attribute.IsDefined(t,typeof(UtcDateTimeAttribute)) ||
                           Attribute.IsDefined(a,typeof(UtcDateTimeAttribute)) });
            foreach (var item in tmp)
            {
                if (item.Property.PropertyType.IsAssignableFrom(item.SourceProperty.PropertyType) ||
                    (item.Property.PropertyType.IsGenericType &&
                     item.Property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                     Nullable.GetUnderlyingType(item.Property.PropertyType)
                         .IsAssignableFrom(item.SourceProperty.PropertyType)) ||
                    (item.SourceProperty.PropertyType.IsGenericType &&
                     item.SourceProperty.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                     item.Property.PropertyType.IsAssignableFrom(
                         Nullable.GetUnderlyingType(item.SourceProperty.PropertyType))))
                {
                    yield return new AssignmentHolder
                    {
                        PropType = item.Property.PropertyType, 
                        Source = item.SourceProperty, 
                        Destination = item.Property, 
                        SpecifyDateTimeAsUtc = item.UseUtcDateTime,
                        DestinationNullable = item.Property.PropertyType.IsGenericType &&
                                              item.Property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>),
                        SourceNullable = item.SourceProperty.PropertyType.IsGenericType &&
                                         item.SourceProperty.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)
                    };
                }
                else if (Attribute.IsDefined(item.Property, typeof(SetterMethodAttribute)))
                {
                    SetterMethodAttribute sat =
                        (SetterMethodAttribute)
                        Attribute.GetCustomAttribute(item.Property, typeof(SetterMethodAttribute));
                    MethodInfo inf = viewType.GetMethod(sat.MethodName,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null,
                        new[] { item.SourceProperty.PropertyType }, null);
                    /*if (inf != null)
                    {
                        inf.Invoke(retVal, new[] {item.Value});
                    }*/
                    yield return new AssignmentHolder { 
                        Setter = inf, 
                        PropType = item.SourceProperty.PropertyType, 
                        Source = item.SourceProperty, 
                        Destination = null, 
                        SpecifyDateTimeAsUtc = item.UseUtcDateTime ,
                        DestinationNullable = item.Property.PropertyType.IsGenericType &&
                                              item.Property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>),
                        SourceNullable = item.SourceProperty.PropertyType.IsGenericType &&
                                         item.SourceProperty.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)
                    };
                }
                else
                {
                    yield return new AssignmentHolder
                    {
                        Source = item.SourceProperty, 
                        Destination = item.Property, 
                        UseConvert = true, 
                        PropType = item.Property.PropertyType, 
                        SpecifyDateTimeAsUtc = item.UseUtcDateTime,
                        DestinationNullable = item.Property.PropertyType.IsGenericType &&
                                              item.Property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>),
                        SourceNullable = item.SourceProperty.PropertyType.IsGenericType &&
                                         item.SourceProperty.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)
                    };
                }
            }
        }

        /// <summary>
        /// Assigns an action to the list of buffered actions
        /// </summary>
        /// <typeparam name="TSource">the source-type for a copy-action</typeparam>
        /// <typeparam name="TTarget">the destination-type for a copy-action</typeparam>
        /// <param name="methodTargetDic">the Dictionary where the assignment-method is being stored</param>
        /// <param name="selector">the Property-Selector method</param>
        /// <returns></returns>
        private static Action<TSource, TTarget, Func<string,bool>> GetActionFor<TSource, TTarget>(ConcurrentDictionary<Type, ConcurrentDictionary<Type, Delegate>> methodTargetDic, Func<IEnumerable<AssignmentHolder>> selector, bool useUtcSpecify)
        {
            var tmp1 = methodTargetDic.GetOrAdd(typeof(TSource), t => new ConcurrentDictionary<Type, Delegate>());
            Action<TSource, TTarget, Func<string,bool>> copyAction = (Action<TSource, TTarget, Func<string, bool>>)tmp1.GetOrAdd(typeof(TTarget), t => ObjectCloneExtensions.BuildAssignmentLambda<TSource, TTarget>(selector(), useUtcSpecify));
            return copyAction;
        }

        /// <summary>
        /// Creates a Copy block that is capable for ViewModel-To-DbModel copy methods
        /// </summary>
        /// <typeparam name="TViewModel">the view-model from which to copy the data</typeparam>
        /// <typeparam name="TDbModel">the target db-model</typeparam>
        /// <returns>the created action using Lambda-Expressions</returns>
        private static IEnumerable<AssignmentHolder> CreateDbCopyMethod<TViewModel, TDbModel>()
        {
            Type modelType = typeof(TDbModel);
            Type viewType = typeof(TViewModel);
            PropertyInfo[] allVmProperties =
                viewType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            PropertyInfo[] allDbProperties = modelType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            return (from t in allDbProperties
                    join a in allVmProperties on t.Name equals a.Name
                    where t.CanWrite && t.GetIndexParameters().Length == 0 && !Attribute.IsDefined(a, typeof(KeyAttribute), true)
                          && t.PropertyType == a.PropertyType
                    select new AssignmentHolder
                    {
                        PropType = t.PropertyType, 
                        Source = a, 
                        Destination = t, 
                        SpecifyDateTimeAsUtc = Attribute.IsDefined(t, typeof(UtcDateTimeAttribute)) || Attribute.IsDefined(a, typeof(UtcDateTimeAttribute)),
                        DestinationNullable = t.PropertyType.IsGenericType &&
                                              t.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>),
                        SourceNullable = a.PropertyType.IsGenericType &&
                                         a.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)
                    });
        }
    }
}
