using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
        public static T Copy<T>(this T source) where T : class, new()
        {
            var retVal = new T();
            var a = GetCopyActionFor<T>();
            a(source, retVal);
            return retVal;
        }

        /// <summary>
        /// Builds an assignment block for setting all valid properties of the target-object to the values of the source-object
        /// </summary>
        /// <typeparam name="TSource">the source-type</typeparam>
        /// <typeparam name="TTarget">the Destination-type</typeparam>
        /// <param name="assignmentSource">the Getter and Setter methods of the target-properties</param>
        /// <returns></returns>
        public static Action<TSource, TTarget> BuildAssignmentLambda<TSource, TTarget>(IEnumerable<AssignmentHolder> assignmentSource, bool useUtcSpecify)
        {
            var p1 = Expression.Parameter(typeof(TSource));
            var p2 = Expression.Parameter(typeof(TTarget));
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
                    if (item.Setter == null)
                    {
                        assignments.Add(Expression.Assign(Expression.Property(p2, item.Destination), valX));
                    }
                    else
                    {
                        assignments.Add(Expression.Call(p2, item.Setter, valX));
                    }
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
                    if (item.Setter == null)
                    {
                        assignments.Add(Expression.Assign(Expression.Property(p2, item.Destination), valX));
                    }
                    else
                    {
                        assignments.Add(Expression.Call(p2, item.Setter, valX));
                    }
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
                    if (item.Setter == null)
                    {
                        assignments.Add(Expression.TryCatch(Expression.Block(typeof(void), Expression.Assign(Expression.Property(p2, item.Destination), valX)), Expression.Catch(typeof(Exception), Expression.Empty())));
                    }
                    else
                    {
                        assignments.Add(Expression.TryCatch(Expression.Block(typeof(void), Expression.Call(p2, item.Setter, valX)), Expression.Catch(typeof(Exception), Expression.Empty())));
                    }
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

                    if (item.Setter == null)
                    {
                        assignments.Add(Expression.TryCatch(Expression.Block(typeof(void), Expression.Assign(Expression.Property(p2, item.Destination), valX)), Expression.Catch(typeof(Exception), Expression.Empty())));
                    }
                    else
                    {
                        assignments.Add(Expression.TryCatch(Expression.Block(typeof(void), Expression.Call(p2, item.Setter, valX)), Expression.Catch(typeof(Exception), Expression.Empty())));
                    }
                }
            }

            var block = Expression.Block(assignments);
            return Expression.Lambda<Action<TSource, TTarget>>(block, p1, p2).Compile();
        }

        /// <summary>
        /// Assigns an action to the list of buffered actions
        /// </summary>
        /// <typeparam name="T">the object type to flat-clone</typeparam>
        /// <returns></returns>
        private static Action<T, T> GetCopyActionFor<T>()
        {
            Action<T, T> copyAction = (Action<T, T>)objectCloneMethods.GetOrAdd(typeof(T), t => BuildAssignmentLambda<T, T>(CreateFlatCopyMethod<T>(), false));
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
