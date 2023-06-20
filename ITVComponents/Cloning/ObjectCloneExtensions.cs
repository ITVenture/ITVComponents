using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Cloning.Model;
using ITVComponents.TypeConversion;

namespace ITVComponents.Cloning
{
    public static class ObjectCloneExtensions
    {
        private static ConcurrentDictionary<Type, Delegate> objectCloneMethods = new ();

        /// <summary>
        /// Executes a rudimentary copy-action for the given object
        /// </summary>
        /// <typeparam name="T">the object-type of which to get a copy</typeparam>
        /// <param name="source">the source-object</param>
        /// <returns>a flat-copy of the given object</returns>
        public static T Copy<T>(this T source, Func<string,bool> propertyFilter = null) where T : class, new()
        {
            propertyFilter ??= s => true;
            var retVal = new T();
            var a = GetCopyActionFor<T>();
            a(source, retVal, propertyFilter);
            return retVal;
        }

        /// <summary>
        /// Builds an assignment block for setting all valid properties of the target-object to the values of the source-object
        /// </summary>
        /// <typeparam name="TSource">the source-type</typeparam>
        /// <typeparam name="TTarget">the Destination-type</typeparam>
        /// <param name="assignmentSource">the Getter and Setter methods of the target-properties</param>
        /// <returns></returns>
        public static Action<TSource, TTarget, Func<string,bool>> BuildAssignmentLambda<TSource, TTarget>(IEnumerable<AssignmentHolder> assignmentSource, bool useUtcSpecify)
        {
            var p1 = Expression.Parameter(typeof(TSource));
            var p2 = Expression.Parameter(typeof(TTarget));
            var p3 = Expression.Parameter(typeof(Func<string,bool>));
            var tryConvertMethod = typeof(TypeConverter).GetMethod("TryConvert",
                BindingFlags.Public | BindingFlags.Static, null, new Type[]
                {
                    typeof(object),
                    typeof(Type)
                }, null);
            var specifyKindMethod = typeof(DateTime).GetMethod("SpecifyKind",
                BindingFlags.Public | BindingFlags.Static, null, new Type[]
                {
                    typeof(DateTime),
                    typeof(DateTimeKind)
                }, null);
            var assignments = new List<Expression>();
            foreach (var item in assignmentSource)
            {
                var propNameX = Expression.Constant(item.Destination.Name);
                var res = Expression.Invoke(p3, propNameX);
                var propNullType = item.PropType.IsPrimitive ? typeof(Nullable<>).MakeGenericType(item.PropType) : null;
                var propNullValue = propNullType?.GetProperty("Value");
                if (propNullType != null && item.Source.PropertyType == propNullType && !item.UseConvert)
                {
                    var propX = Expression.Property(p1, item.Source);
                    Expression propXP = Expression.Property(propX, propNullValue);
                    if (item.SpecifyDateTimeAsUtc && useUtcSpecify)
                    {
                        propXP = Expression.Call(null, specifyKindMethod, propXP,
                            Expression.Constant(DateTimeKind.Utc));
                    }

                    Expression valX = Expression.Condition(Expression.NotEqual(propX, Expression.Constant(null, propNullType)), propXP, Expression.Default(item.PropType));
                    Expression assignment;
                    if (item.Setter == null)
                    {
                        assignment=Expression.Assign(Expression.Property(p2, item.Destination), valX);
                    }
                    else
                    {
                        assignment=Expression.Call(p2, item.Setter, valX);
                    }

                    assignments.Add(Expression.IfThen(res,assignment));
                }
                else if (item.PropType != item.Source.PropertyType && !item.UseConvert)
                {
                    Expression valX = Expression.Property(p1, item.Source);
                    if (item.SpecifyDateTimeAsUtc && useUtcSpecify)
                    {
                        if (!item.SourceNullable)
                        {
                            valX = Expression.Call(null, specifyKindMethod, valX,
                                Expression.Constant(DateTimeKind.Utc));
                        }
                        else
                        {
                            var nprop = Expression.Convert(Expression.Call(null, specifyKindMethod, Expression.Property(valX, item.Source.PropertyType?.GetProperty("Value")),
                                    Expression.Constant(DateTimeKind.Utc))
                                , item.Source.PropertyType);
                            valX = Expression.Condition(Expression.NotEqual(valX, Expression.Constant(null, item.Source.PropertyType)), nprop, Expression.Constant(null, item.Source.PropertyType));
                        }
                    }

                    valX = Expression.Convert(valX, item.PropType);
                    Expression assignment;
                    if (item.Setter == null)
                    {
                        assignment = Expression.Assign(Expression.Property(p2, item.Destination), valX);
                    }
                    else
                    {
                        assignment = Expression.Call(p2, item.Setter, valX);
                    }

                    assignments.Add(Expression.IfThen(res, assignment));
                }
                else if (item.UseConvert)
                {
                    Expression valX = Expression.Property(p1, item.Source);
                    if (item.SpecifyDateTimeAsUtc && useUtcSpecify)
                    {
                        if (!item.SourceNullable)
                        {
                            valX = Expression.Call(null, specifyKindMethod, valX,
                                Expression.Constant(DateTimeKind.Utc));
                        }
                        else
                        {
                            var nprop = Expression.Convert(Expression.Call(null, specifyKindMethod, Expression.Property(valX, item.Source.PropertyType?.GetProperty("Value")),
                                    Expression.Constant(DateTimeKind.Utc))
                                , item.Source.PropertyType);
                            valX = Expression.Condition(Expression.NotEqual(valX, Expression.Constant(null, item.Source.PropertyType)), nprop, Expression.Constant(null, item.Source.PropertyType));
                        }
                    }
                    valX = Expression.Call(null, tryConvertMethod, Expression.Convert(valX, typeof(object)),
                        Expression.Constant(item.PropType));
                    valX = Expression.Convert(valX, item.PropType);
                    Expression assignment;
                    if (item.Setter == null)
                    {
                        assignment = Expression.TryCatch(Expression.Block(typeof(void), Expression.Assign(Expression.Property(p2, item.Destination), valX)), Expression.Catch(typeof(Exception), Expression.Empty()));
                    }
                    else
                    {
                        assignment = Expression.TryCatch(Expression.Block(typeof(void), Expression.Call(p2, item.Setter, valX)), Expression.Catch(typeof(Exception), Expression.Empty()));
                    }

                    assignments.Add(Expression.IfThen(res, assignment));
                }
                else
                {
                    Expression valX = Expression.Property(p1, item.Source);
                    if (item.SpecifyDateTimeAsUtc && useUtcSpecify)
                    {
                        if (!item.SourceNullable)
                        {
                            valX = Expression.Call(null, specifyKindMethod, valX,
                                Expression.Constant(DateTimeKind.Utc));
                        }
                        else
                        {
                            var nprop = Expression.Convert(Expression.Call(null, specifyKindMethod, Expression.Property(valX, item.Source.PropertyType?.GetProperty("Value")),
                                    Expression.Constant(DateTimeKind.Utc))
                                , item.Source.PropertyType);
                            valX = Expression.Condition(Expression.NotEqual(valX, Expression.Constant(null, item.Source.PropertyType)), nprop, Expression.Constant(null, item.Source.PropertyType));
                        }
                    }

                    Expression assignment;
                    if (item.Setter == null)
                    {
                        assignment = Expression.TryCatch(Expression.Block(typeof(void), Expression.Assign(Expression.Property(p2, item.Destination), valX)), Expression.Catch(typeof(Exception), Expression.Empty()));
                    }
                    else
                    {
                        assignment = Expression.TryCatch(Expression.Block(typeof(void), Expression.Call(p2, item.Setter, valX)), Expression.Catch(typeof(Exception), Expression.Empty()));
                    }

                    assignments.Add(Expression.IfThen(res, assignment));
                }
            }

            var block = Expression.Block(assignments);
            return Expression.Lambda<Action<TSource, TTarget, Func<string,bool>>>(block, p1, p2, p3).Compile();
        }

        /// <summary>
        /// Assigns an action to the list of buffered actions
        /// </summary>
        /// <typeparam name="T">the object type to flat-clone</typeparam>
        /// <returns></returns>
        private static Action<T, T, Func<string, bool>> GetCopyActionFor<T>()
        {
            Action<T, T, Func<string, bool>> copyAction = (Action<T, T, Func<string,bool>>)objectCloneMethods.GetOrAdd(typeof(T), t => BuildAssignmentLambda<T, T>(CreateFlatCopyMethod<T>(), false));
            return copyAction;
        }

        /// <summary>
        /// Creates a Copy block that is capable for ViewModel-To-DbModel copy methods
        /// </summary>
        /// <typeparam name="T">the object-type of which to create a copy</typeparam>
        /// <returns>the created action using Lambda-Expressions</returns>
        private static IEnumerable<AssignmentHolder> CreateFlatCopyMethod<T>()
        {
            Type modelType = typeof(T);
            PropertyInfo[] allProperties = modelType.GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
            return (from t in allProperties
                where t.CanWrite && t.CanRead && t.GetIndexParameters().Length == 0
                select new AssignmentHolder
                {
                    PropType = t.PropertyType,
                    Source = t,
                    Destination = t,
                    SpecifyDateTimeAsUtc = false,
                    DestinationNullable = t.PropertyType.IsGenericType &&
                                          t.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>),
                    SourceNullable = t.PropertyType.IsGenericType &&
                                     t.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)
                });
        }
    }
}
