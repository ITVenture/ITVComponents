using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Expressions.Visitors
{
    public class ParameterReplaceVisitor:ExpressionVisitor
    {
        private ParameterExpression[] original;

        private ParameterExpression[] replacements;

        public static Expression ReplaceFuncParams(Expression lambda, params ParameterExpression[] parameters)
        {
            var prv = new ParameterReplaceVisitor();
            return prv.ReplaceParameterInBody(lambda, parameters);
        }

        public Expression ReplaceParameterInBody(Expression lambda, params ParameterExpression[] parameters)
        {
            if (lambda is LambdaExpression lex)
            {
                if (lex.Parameters.Count == parameters.Length)
                {
                    var ok = true; //lex.Body is GotoExpression{Kind: GotoExpressionKind.Return};
                    for (int i = 0; i < lex.Parameters.Count && ok; i++)
                    {
                        var t1 = lex.Parameters[i].Type;
                        var t2 = parameters[i].Type;
                        ok &= t1 == t2 || t1.IsAssignableFrom(t2);
                    }

                    if (ok)
                    {
                        original = lex.Parameters.ToArray();
                        replacements = parameters;
                        return Visit(lex.Body);
                    }

                    throw new InvalidOperationException("Argument types do not match!");
                }

                throw new InvalidOperationException("Length of parameter array does not match");
            }

            throw new InvalidOperationException("Provided Expression is not lambda!");
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            var id = Array.IndexOf(original, node);
            if (id != -1)
            {
                return replacements[id];
            }

            return node;
        }
    }
}
