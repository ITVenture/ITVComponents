using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ITVComponents.EFRepo.DbContextConfig.Expressions
{
    public class ExpressionFixVisitor:ExpressionVisitor
    {
        private readonly Dictionary<string, Expression> propertyReplacements = new();

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
            else
            {
                throw new InvalidOperationException("Unsupported Expression-Type!");
            }
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
