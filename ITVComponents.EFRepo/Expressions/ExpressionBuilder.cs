using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.TypeConversion;
using ParameterExpression = System.Linq.Expressions.ParameterExpression;

namespace ITVComponents.EFRepo.Expressions
{
    public static class ExpressionBuilder
    {
        public static Expression<Func<T, bool>> BuildExpression<T>(FilterBase filter, Func<string, string> redirectColumnName = null)
        {
            var parameter = Expression.Parameter(typeof(T));
            var x = BuildExpression<T>(filter, parameter, redirectColumnName);
            //var red = block.Reduce();
            return Expression.Lambda<Func<T, bool>>(x, parameter);
        }

        public static Expression<Func<T,object>> BuildPropertyAccessExpression<T>(string name, Func<string, string> redirectColumnName = null)
        {
            var parameter = Expression.Parameter(typeof(T));
            Expression x = BuildPropertyAccess(name, parameter, redirectColumnName);
            x = Expression.Convert(x, typeof(object));
            return Expression.Lambda<Func<T, object>>(x, parameter);
        }

        public static Expression<Func<T, object>> Fubar<T>(T honk)
        {
            DateTime? hink = null;
            return T => hink;
        }

        private static Expression BuildExpression<T>(FilterBase filter, Expression parameter, Func<string, string> redirectColumnName)
        {
            if (filter is CompositeFilter comp)
            {
                return BuildComposite<T>(comp, parameter, redirectColumnName);
            }
            else if (filter is CompareFilter cop)
            {
                return BuildCompare<T>(cop, parameter, redirectColumnName);
            }
            else if (filter is CustomFilter<T> cut)
            {
                return Expression.Invoke(cut.Filter, parameter);
            }

            throw new InvalidOperationException("Invalid Filter object");
        }

        private static MemberExpression BuildPropertyAccess(string propertyName, Expression parameter, Func<string, string> redirectColumnName)
        {
            var prp = (redirectColumnName?.Invoke(propertyName)??propertyName).Split(".");
            var prpAccess = Expression.Property(parameter, prp[0]);
            if (prp.Length > 1)
            {
                for (int a = 1; a < prp.Length; a++)
                {
                    prpAccess = Expression.Property(prpAccess, prp[a]);
                }
            }

            return prpAccess;
        }

        private static Expression BuildCompare<T>(CompareFilter filter, Expression parameter, Func<string, string> redirectColumnName)
        {
            var prpAccess = BuildPropertyAccess(filter.PropertyName, parameter, redirectColumnName);
            var body = CompareExpression(filter.Operator, prpAccess, filter.Value, filter.Value2);
            return body;
        }

        private static Expression CompareExpression(CompareOperator filterOperator, MemberExpression compareTarget, object filterValue, object filterValue2)
        {
            var propertyType = ((PropertyInfo)compareTarget.Member).PropertyType;
            var value = TypeConverter.Convert(filterValue, propertyType);
            var fix = Expression.PropertyOrField(Expression.Constant(ValueHold.ValueHoldFor(propertyType, value, out var ft), ft), "Value");
            switch (filterOperator)
            {
                case CompareOperator.Equal:
                    return Expression.Equal(compareTarget, fix);
                case CompareOperator.NotEqual:
                    return Expression.NotEqual(compareTarget, fix);
                    break;
                case CompareOperator.GreaterThan:
                    return Expression.GreaterThan(compareTarget, fix);
                    break;
                case CompareOperator.GreaterThanOrEqual:
                    return Expression.GreaterThanOrEqual(compareTarget, fix);
                    break;
                case CompareOperator.LessThan:
                    return Expression.LessThan(compareTarget, fix);
                    break;
                case CompareOperator.LessThanOrEqual:
                    return Expression.LessThanOrEqual(compareTarget, fix);
                    break;
                case CompareOperator.Contains:
                    return Expression.Call(compareTarget, propertyType.GetMethod("Contains", new[] { propertyType }), fix);
                    break;
                case CompareOperator.ContainsNot:
                    return Expression.Not(Expression.Call(compareTarget, propertyType.GetMethod("Contains", new[] { propertyType }), fix));
                    break;
                case CompareOperator.StartsWith:
                    return Expression.Call(compareTarget, propertyType.GetMethod("StartsWith", new[] { propertyType }), fix);
                    break;
                case CompareOperator.StartsNotWith:
                    return Expression.Not(Expression.Call(compareTarget, propertyType.GetMethod("StartsWith", new[] { propertyType }), fix));
                    break;
                case CompareOperator.EndsWith:
                    return Expression.Call(compareTarget, propertyType.GetMethod("EndsWith", new[] { propertyType }), fix);
                    break;
                case CompareOperator.EndsNotWith:
                    return Expression.Not(Expression.Call(compareTarget, propertyType.GetMethod("EndsWith", new[] { propertyType }), fix));
                    break;
                case CompareOperator.Between:
                    var fix2 = Expression.PropertyOrField(Expression.Constant(ValueHold.ValueHoldFor(propertyType, filterValue2, out var ftx2), ftx2), "Value");
                    return Expression.AndAlso(Expression.GreaterThanOrEqual(compareTarget, fix), Expression.LessThanOrEqual(compareTarget, fix2));
                    break;
                case CompareOperator.NotBetween:
                    var fix2N = Expression.PropertyOrField(Expression.Constant(ValueHold.ValueHoldFor(propertyType, filterValue2, out var ftx2N), ftx2N), "Value");
                    return Expression.Not(Expression.AndAlso(Expression.GreaterThanOrEqual(compareTarget, fix), Expression.LessThanOrEqual(compareTarget, fix2N)));
                    break;
                case CompareOperator.IsNull:
                    fix = Expression.PropertyOrField(Expression.Constant(ValueHold.ValueHoldFor(propertyType, null, out _), ft), "Value");
                    return Expression.Equal(compareTarget, fix);
                    break;
                case CompareOperator.IsNotNull:
                    fix = Expression.PropertyOrField(Expression.Constant(ValueHold.ValueHoldFor(propertyType, null, out _), ft), "Value");
                    return Expression.NotEqual(compareTarget, fix);
                    break;
                case CompareOperator.IsEmpty:
                    return Expression.Call(null, propertyType.GetMethod("IsNullOrEmpty", BindingFlags.Static | BindingFlags.Public, new[] { propertyType }), compareTarget);
                    break;
                case CompareOperator.IsNotEmpty:
                    return Expression.Not(Expression.Call(null, propertyType.GetMethod("IsNullOrEmpty", BindingFlags.Static | BindingFlags.Public, new[] { propertyType }), compareTarget));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(filterOperator), filterOperator, null);
            }
        }

        private static Expression BuildComposite<T>(CompositeFilter filter, Expression parameter, Func<string, string> redirectColumnName)
        {
            var first = filter.Children.FirstOrDefault();
            if (first != null)
            {
                Expression rootEx = BuildExpression<T>(first, parameter, redirectColumnName);
                for (int i = 1; i < filter.Children.Length; i++)
                {
                    var tp = BuildExpression<T>(filter.Children[i], parameter, redirectColumnName);
                    if (filter.Operator == BoolOperator.And)
                    {
                        rootEx = Expression.AndAlso(rootEx, tp);
                    }
                    else
                    {
                        rootEx = Expression.OrElse(rootEx, tp);
                    }
                }
                
                return rootEx;
            }

            return Expression.Constant(true);
        }
    }
}
