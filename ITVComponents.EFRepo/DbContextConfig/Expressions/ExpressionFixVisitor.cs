using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ITVComponents.EFRepo.DbContextConfig.Expressions
{
    public class ExpressionFixVisitor:ExpressionVisitor
    {
        private readonly Dictionary<string, Expression> propertyReplacements = new();

        private readonly Dictionary<string, Expression> methodReplacements = new();

        public ExpressionFixVisitor()
        {
        }

        internal void RegisterExpression<T>(Expression<Func<T>> expression)
        {
            var tmp = expression.Body;
            if (tmp is MemberExpression mex)
            {
                string name = mex.Member.Name;
                if (Attribute.GetCustomAttribute(mex.Member, typeof(ExpressionPropertyRedirectAttribute)) is
                    ExpressionPropertyRedirectAttribute ptr)
                {
                    name = ptr.ReplacerName;
                }

                if (!propertyReplacements.ContainsKey(name))
                {
                    propertyReplacements.Add(name, mex);
                }
            }
            else if (tmp is MethodCallExpression cex)
            {
                string name = cex.Method.Name;
                if (Attribute.GetCustomAttribute(cex.Method, typeof(ExpressionMethodRedirectAttribute)) is
                    ExpressionMethodRedirectAttribute mtr)
                {
                    name = mtr.ReplacerName;
                }

                if (!methodReplacements.ContainsKey(name))
                {
                    methodReplacements.Add(name, cex);
                }
            }
            else
            {
                throw new InvalidOperationException("Unsupported Expression-Type!");
            }
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var tmp = node.Method;
            if (Attribute.GetCustomAttribute(tmp, typeof(ExpressionMethodRedirectAttribute)) is
                ExpressionMethodRedirectAttribute ptr)
            {
                if (methodReplacements.TryGetValue(ptr.ReplacerName, out var rex) && rex is MethodCallExpression mex && mex.Arguments.Count == node.Arguments.Count)
                {
                    var gr = (from t in node.Arguments select Visit(t)).ToArray();
                    return Expression.Call(mex.Object, mex.Method, gr);
                }

                throw new ArgumentException($"No replacer found for Method {ptr.ReplacerName}", ptr.ReplacerName);
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var tmp = node.Member;
            if (Attribute.GetCustomAttribute(tmp, typeof(ExpressionPropertyRedirectAttribute)) is ExpressionPropertyRedirectAttribute ptr)
            {
                if (propertyReplacements.TryGetValue(ptr.ReplacerName, out var rex))
                {
                    return rex;
                }

                throw new ArgumentException($"No replacer found for Property {ptr.ReplacerName}", ptr.ReplacerName);
            }

            return base.VisitMember(node);
        }
    }
}
