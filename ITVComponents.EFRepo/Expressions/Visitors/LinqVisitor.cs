using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using ITVComponents.Scripting.CScript.ReflectionHelpers;
using Expression = System.Linq.Expressions.Expression;

namespace ITVComponents.EFRepo.Expressions.Visitors
{
    public class LinqVisitor:ExpressionVisitor
    {
        private Dictionary<string, ParameterExpression> arguments = new Dictionary<string, ParameterExpression>();

        public  Expression VisitLinqExpression(Expression node)
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
                    if (pit.ValueExpression == null && pit.TargetProperty != null)
                    {
                        return Expression.MakeMemberAccess(g, pit.TargetProperty);
                    }
                    else if (pit.ValueExpression != null)
                    {
                        return ParameterReplaceVisitor.ReplaceFuncParams(pit.ValueExpression, g);
                    }

                    throw new InvalidOperationException("Illegal PropertyConfiguration provided");
                }
            }
            else if (node.Expression is NewExpression nwx)
            {
                if (typeof(IPropertyInitializer).IsAssignableFrom(nwx.Type))
                {
                    PropertyInfo pi = null;
                    string name = null;
                    Expression valex = null;
                    if (nwx.Arguments[0] is ConstantExpression prop && prop.Value is PropertyInfo piv)
                    {
                        pi = piv;
                    }
                    else if (nwx.Arguments[0] is MemberExpression mex && mex.Member is FieldInfo mexf && mex.Expression is ConstantExpression cmex)
                    {
                        pi = (PropertyInfo)mexf.GetValue(cmex.Value);
                    }
                    
                    if (nwx.Arguments[1] is ConstantExpression nam && nam.Value is string nams)
                    {
                        name = nams;
                    }
                    else if (nwx.Arguments[1] is MemberExpression mex && mex.Member is FieldInfo mexf &&
                             mex.Expression is ConstantExpression cmex)
                    {
                        name = (string)mexf.GetValue(cmex.Value);
                    }

                    if (nwx.Arguments[2] is ConstantExpression vxi && vxi.Value is Expression ve)
                    {
                        valex = ve;
                    }
                    else if (nwx.Arguments[2] is MemberExpression mex && mex.Member is FieldInfo mexf &&
                             mex.Expression is ConstantExpression cmex)
                    {
                        valex = (Expression)mexf.GetValue(cmex.Value);
                    }

                    if ((pi != null || valex != null) && !string.IsNullOrEmpty(name))
                    {
                        var g = arguments[name];
                        if (valex == null)
                        {
                            return Expression.MakeMemberAccess(g, pi);
                        }
                        else
                        {
                            return ParameterReplaceVisitor.ReplaceFuncParams(valex, g);
                        }
                    }
                }
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            arguments.Clear();
            foreach (var arg in node.Parameters)
            {
                arguments.Add(arg.Name, arg);
            }

            return base.VisitLambda(node);
        }

        /*protected override Expression VisitParameter(ParameterExpression node)
        {
            arguments.Add(node.Name, node);
            return base.VisitParameter(node);
        }*/
    }
}
