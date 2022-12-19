using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Helpers
{
    public static class LambdaHelper
    {
        /// <summary>
        /// Given a lambda expression that calls a method, returns the method info.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public static MethodInfo GetMethodInfo(Expression<Action> call)
        {
            return GetMethodInfoInt(call);
        }

        /// <summary>
        /// Given a lambda expression that calls a method, returns the method info.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public static MethodInfo GetMethodInfo<T>(Expression<Func<T,object>> call)
        {
            return GetMethodInfoInt(call);
        }

        /// <summary>
        /// Given a lambda expression that calls a method, returns the method info.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public static MethodInfo GetMethodInfo<T>(Expression<Action<T>> call)
        {
            return GetMethodInfoInt(call);
        }

        /// <summary>
        /// Given a lambda expression that calls a method, returns the method info.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public static MethodInfo GetMethodInfo(Expression<Func<object>> call)
        {
            return GetMethodInfoInt(call);
        }

        /// <summary>
        /// Gets the event from the given type
        /// </summary>
        /// <param name="t">the type on which to search for an event</param>
        /// <param name="eventName">the name of the requested event</param>
        /// <param name="instance">indicates whether to search for instance or static events</param>
        /// <returns></returns>
        public static EventInfo GetEventInfo(Type t, string eventName, bool instance)
        {
            return t.GetEvent(eventName, BindingFlags.Public | (instance?BindingFlags.Instance:BindingFlags.Static));
        }

        /// <summary>
        /// Gets the event from the given type
        /// </summary>
        /// <param name="eventName">the name of the requested event</param>
        /// <param name="instance">indicates whether to search for instance or static events</param>
        /// <typeparam name="T">the class on which to subscribe the event</typeparam>
        /// <returns></returns>
        public static EventInfo GetEventInfo<T>(string eventName, bool instance)
        {
            return GetEventInfo(typeof(T), eventName, instance);
        }

        public static FieldInfo GetFieldInfo<T>(Expression<Func<T, object>> fieldAccess)
        {
            return GetFieldInfoInt(fieldAccess);
        }

        public static FieldInfo GetFieldInfo(Expression<Func<object>> fieldAccess)
        {
            return GetFieldInfoInt(fieldAccess);
        }

        public static PropertyInfo GetPropertyInfo<T>(Expression<Func<T, object>> propertyAccess)
        {
            return GetPropertyInfoInt(propertyAccess);
        }

        public static MemberExpression GetPropertyExpression<T>(this Expression<Func<T>> propertyAccess)
        {
            return GetPropertyExpressionInt(propertyAccess);
        }

        public static PropertyInfo GetPropertyInfo(Expression<Func<object>> propertyAccess)
        {
            return GetPropertyInfoInt(propertyAccess);
        }

        /// <summary>
        /// Given a lambda expression that calls a method, returns the method info.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        private static MethodInfo GetMethodInfoInt(LambdaExpression expression)
        {
            MethodCallExpression outermostExpression = expression.Body as MethodCallExpression;

            if (expression.Body is MethodCallExpression{Method: MethodInfo me})
            {
                return me;
            }

            throw new ArgumentException("Invalid Expression. Expression should consist of a Method call only.");
        }

        private static FieldInfo GetFieldInfoInt(LambdaExpression expression)
        {
            if (expression.Body is MemberExpression { Member: FieldInfo fi })
            {
                return fi;
            }

            if (expression.Body is UnaryExpression
                {
                    NodeType: ExpressionType.Convert, Operand: MemberExpression { Member: FieldInfo fio }
                })
            {
                return fio;
            }

            throw new ArgumentException("Invalid Expression. Expression should consist of a field-access statement only.");
        }

        private static PropertyInfo GetPropertyInfoInt(LambdaExpression expression)
        {
            return GetPropertyExpressionInt(expression).Member as PropertyInfo;
        }

        private static MemberExpression GetPropertyExpressionInt(LambdaExpression expression)
        {
            if (expression.Body is MemberExpression { Member: PropertyInfo} pr)
            {
                return pr;
            }

            if (expression.Body is UnaryExpression
                {
                    NodeType: ExpressionType.Convert, Operand: MemberExpression { Member: PropertyInfo} pro
                })
            {
                return pro;
            }

            throw new ArgumentException("Invalid Expression. Expression should consist of an property-access statement only.");
        }
    }
}
