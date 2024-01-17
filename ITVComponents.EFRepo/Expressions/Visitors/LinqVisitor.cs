using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Expressions.Visitors
{
    public class LinqVisitor:ExpressionVisitor
    {
        private Dictionary<string, ParameterExpression> arguments = new Dictionary<string, ParameterExpression>();

        public override Expression Visit(Expression node)
        {
            lock (arguments)
            {
                try
                {
                    return base.Visit(node);
                }
                finally
                {
                    arguments.Clear();
                }
            }
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ConstantExpression cx)
            {
                if (cx.Value is IPropertyInitializer pit)
                {
                    var g = arguments[pit.ArgumentName];
                    return Expression.MakeMemberAccess(g, pit.TargetProperty);
                }
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            arguments.Add(node.Name, node);
            return base.VisitParameter(node);
        }
    }
}
