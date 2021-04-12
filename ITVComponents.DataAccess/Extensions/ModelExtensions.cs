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
using ITVComponents.DataAccess.DataAnnotations;

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
        public static T ToViewModel<TDbModel, T>(this TDbModel modelObject, Func<Type, object> requestObjectInstance, Action<TDbModel,T> postProcessAction = null) where T : class, new()
                                                                                                                where TDbModel: class
        {
            if (modelObject == null)
            {
                return null;
            }

            T retVal = new T();
            Type viewType = typeof (T);
            PropertyInfo[] allViewProperties = viewType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            var customValueProps = (from t in allViewProperties
                where t.CanWrite && t.GetIndexParameters().Length == 0 && Attribute.IsDefined(t, typeof(CustomValueSourceAttribute), true)
                select new {Property = t, Attributes = System.Linq.Enumerable.Cast<CustomValueSourceAttribute>(Attribute.GetCustomAttributes(t, typeof(CustomValueSourceAttribute), true)).First()});

            var action = GetActionFor<TDbModel, T>(toViewModelActions, SelectToViewModelProps<TDbModel, T>);
            action(modelObject, retVal);
            foreach (var customValue in customValueProps)
            {
                var computed = customValue.Attributes.GetCustomValueFor(modelObject, requestObjectInstance);
                customValue.Property.SetValue(retVal, computed, null);
            }

            postProcessAction?.Invoke(modelObject, retVal);
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
        public static void CopyToModel<TDbModel, T>(this TDbModel modelObject, T target, Action<TDbModel, T> postProcessAction = null) where T : class
                                                                                                                where TDbModel : class
        {
            if (modelObject == null || target == null)
            {
                return ;
            }

            var action = GetActionFor<TDbModel, T>(modelCopyActions, SelectCopyToVmProps<TDbModel, T>);
            action(modelObject, target);
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
                    !Attribute.IsDefined(t, typeof (IgnorePropertyAttribute), true)
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
        public static void CopyToDbModel<TViewModel, TDbModel>(this TViewModel modelObject, TDbModel target) where TViewModel : class
                                                                                                                where TDbModel : class
        {
            if (modelObject == null || target == null)
            {
                return ;
            }

            var action = GetActionFor<TViewModel, TDbModel>(assignmentDbActions, CreateDbCopyMethod<TViewModel, TDbModel>);
            action(modelObject, target);
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
                select new AssignmentHolder{Destination = t, Source= a, PropType=t.PropertyType});
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
            Type viewType = typeof (TViewModel);
            PropertyInfo[] allModelProperties =
                modelType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            PropertyInfo[] allViewProperties = viewType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            var tmp = (from t in allViewProperties
                join a in allModelProperties on t.Name equals a.Name
                where t.CanWrite && t.GetIndexParameters().Length == 0 && !Attribute.IsDefined(t, typeof(IgnorePropertyAttribute), true) && !Attribute.IsDefined(a, typeof(IgnorePropertyAttribute), true)
                select new {Property = t, SourceProperty = a});
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
                    yield return new AssignmentHolder {PropType = item.Property.PropertyType, Source = item.SourceProperty, Destination = item.Property};
                }
                else if (Attribute.IsDefined(item.Property, typeof(SetterMethodAttribute)))
                {
                    SetterMethodAttribute sat =
                        (SetterMethodAttribute)
                        Attribute.GetCustomAttribute(item.Property, typeof(SetterMethodAttribute));
                    MethodInfo inf = viewType.GetMethod(sat.MethodName,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null,
                        new[] {item.SourceProperty.PropertyType}, null);
                    /*if (inf != null)
                    {
                        inf.Invoke(retVal, new[] {item.Value});
                    }*/
                    yield return new AssignmentHolder {Setter = inf, PropType = item.SourceProperty.PropertyType, Source = item.SourceProperty, Destination = null};
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
        private static Action<TSource, TTarget> GetActionFor<TSource, TTarget>(ConcurrentDictionary<Type,ConcurrentDictionary<Type, Delegate>> methodTargetDic, Func<IEnumerable<AssignmentHolder>> selector)
        {
            var tmp1 = methodTargetDic.GetOrAdd(typeof(TSource), t => new ConcurrentDictionary<Type, Delegate>());
            Action<TSource, TTarget> copyAction = (Action<TSource, TTarget>) tmp1.GetOrAdd(typeof(TTarget), t => BuildAssignmentLambda<TSource,TTarget>(selector()));
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
                select new AssignmentHolder{ PropType = t.PropertyType, Source = a, Destination = t});
        }

        /// <summary>
        /// Builds an assignment block for setting all valid properties of the target-object to the values of the source-object
        /// </summary>
        /// <typeparam name="TSource">the source-type</typeparam>
        /// <typeparam name="TTarget">the Destination-type</typeparam>
        /// <param name="assignmentSource">the Getter and Setter methods of the target-properties</param>
        /// <returns></returns>
        private static Action<TSource, TTarget> BuildAssignmentLambda<TSource, TTarget>(IEnumerable<AssignmentHolder> assignmentSource)
        {
            var p1 = Expression.Parameter(typeof(TSource));
            var p2 = Expression.Parameter(typeof(TTarget));
            var assignments = new List<Expression>();
            foreach (var item in assignmentSource)
            {
                var propNullType = item.PropType.IsPrimitive?typeof(Nullable<>).MakeGenericType(item.PropType):null;
                var propNullValue = propNullType?.GetProperty("Value");
                if (propNullType != null && item.Source.PropertyType == propNullType)
                {
                    var propX = Expression.Property(p1, item.Source);
                    var valX = Expression.Condition(Expression.NotEqual(propX, Expression.Constant(null, propNullType)), Expression.Property(propX, propNullValue), Expression.Default(item.PropType));
                    if (item.Setter == null)
                    {
                        assignments.Add(Expression.Assign(Expression.Property(p2, item.Destination), valX));
                    }
                    else
                    {
                        assignments.Add(Expression.Call(p2, item.Setter, valX));
                    }
                }
                else if (item.PropType != item.Source.PropertyType)
                {
                    var valX = Expression.Convert(Expression.Property(p1, item.Source), item.PropType);
                    if (item.Setter == null)
                    {
                        assignments.Add(Expression.Assign(Expression.Property(p2, item.Destination), valX));
                    }
                    else
                    {
                        assignments.Add(Expression.Call(p2, item.Setter, valX));
                    }
                }
                else
                {
                    var valX = Expression.Property(p1, item.Source);
                    if (item.Setter == null)
                    {
                        assignments.Add(Expression.Assign(Expression.Property(p2, item.Destination), valX));
                    }
                    else
                    {
                        assignments.Add(Expression.Call(p2, item.Setter, valX));
                    }
                }
            }

            var block = Expression.Block(assignments);
            return Expression.Lambda<Action<TSource, TTarget>>(block, p1, p2).Compile();
        }

        /// <summary>
        /// Declares an assignment-holder that is used to create an Assignment Lambda-expression
        /// </summary>
        private class AssignmentHolder
        {
            public PropertyInfo Source{get;set;}
            public PropertyInfo Destination{get;set;}
            //public MethodInfo Getter{get;set;}
            public MethodInfo Setter{get;set;}
            public Type PropType { get; set; }
        }
    }
}
