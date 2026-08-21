using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DbContextConfig.Expressions
{
    public class ExpressionFixVisitor:ExpressionVisitor
    {
        private readonly Dictionary<string, Expression> propertyReplacements = new();

        private readonly Dictionary<string, Expression> methodReplacements = new();

        public ExpressionFixVisitor()
        {
        }

        /// <summary>
        /// Registriert den Ausdruck, der kuenftig an die Stelle des gleichnamigen Platzhalters tritt.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Je Name gewinnt die ERSTE Registrierung.</b> Das ist Absicht und trägt zwei Dinge: die
        /// Konfiguration darf mehrfach ueber dasselbe Options-Objekt laufen (genau das tut
        /// <c>DbModelBuilderOptionsProvider.GetOptions</c>, wenn ihm bestehende Optionen mitgegeben
        /// werden), und in Produktion lebt das Options-Objekt als Plugin einmal im Prozess, waehrend die
        /// Kontexte kommen und gehen.
        /// </para>
        /// <para>
        /// <b>Warum das nicht bedeutet, dass der erste Kontext den Filter praegt:</b> der registrierte
        /// Ausdruck ist ein Zugriff auf eine <b>Konstante</b> - die Kontext-Instanz, die als erste hier
        /// vorbeikam. In einem Query-Filter ersetzt EF Core eine <c>DbContext</c>-Konstante durch einen
        /// Zugriff auf den <b>gerade laufenden</b> Kontext; der Filter wertet also jedes Mal auf der
        /// richtigen Instanz aus. Festgehalten in
        /// <c>WorkflowContextTenantTest.SharedOptionsProvider_FilterFollowsTheRunningContext_NotTheFirstOne</c>.
        /// </para>
        /// <para>
        /// <b>Und deshalb die Pruefung unten:</b> diese Zusage gilt <b>nur</b> fuer den Kontext. Wer
        /// stattdessen etwas anderes registriert - einen Dienst, eine lokale Variable, irgendein
        /// gefangenes Objekt -, bekommt keine Umbindung, sondern genau diese eine Instanz, dauerhaft, in
        /// jedem Filter, fuer jeden Benutzer. Das faellt sonst nirgends auf: es uebersetzt sauber und
        /// liefert Ergebnisse - nur die des Ersten. Also lieber hier und laut.
        /// </para>
        /// </remarks>
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

                VerifyBoundToContextOrStatic(mex.Expression, name, mex.Member.Name);
                if (propertyReplacements.TryGetValue(name, out var known))
                {
                    VerifySameMember(((MemberExpression)known).Member, mex.Member, name);
                    return;
                }

                propertyReplacements.Add(name, mex);
            }
            else if (tmp is MethodCallExpression cex)
            {
                string name = cex.Method.Name;
                if (Attribute.GetCustomAttribute(cex.Method, typeof(ExpressionMethodRedirectAttribute)) is
                    ExpressionMethodRedirectAttribute mtr)
                {
                    name = mtr.ReplacerName;
                }

                VerifyBoundToContextOrStatic(cex.Object, name, cex.Method.Name);
                if (methodReplacements.TryGetValue(name, out var known))
                {
                    VerifySameMember(((MethodCallExpression)known).Method, cex.Method, name);
                    return;
                }

                methodReplacements.Add(name, cex);
            }
            else
            {
                throw new InvalidOperationException("Unsupported Expression-Type!");
            }
        }

        /// <summary>
        /// Stellt sicher, dass der Ausdruck auf dem Kontext steht (oder statisch ist) - siehe
        /// <see cref="RegisterExpression{T}"/>.
        /// </summary>
        private static void VerifyBoundToContextOrStatic(Expression target, string replacerName, string memberName)
        {
            // Statisch: keine Instanz, an der etwas haengenbleiben koennte.
            if (target == null)
            {
                return;
            }

            // Bis zur Wurzel durch: "() => Inner.Value" auf dem Kontext ist so gut wie "() => Value".
            Expression root = target;
            while (root is MemberExpression inner && inner.Expression != null)
            {
                root = inner.Expression;
            }

            if (root is ConstantExpression constant && constant.Value is DbContext)
            {
                return;
            }

            throw new InvalidOperationException(
                $"The expression registered for '{replacerName}' ({memberName}) is not rooted in the DbContext "
                + $"but in '{root.Type.Name}'. Only a member of the context can be re-bound to the context that "
                + "is actually running a query - anything else would be pinned to the single instance that "
                + "happened to register first, silently, for every filter and every user. Move the value onto "
                + "the context (a property that reads the service) and register that.");
        }

        /// <summary>
        /// Eine erneute Registrierung desselben Namens ist der Normalfall (neuer Kontext, dieselbe
        /// Eigenschaft) - ein <b>anderes</b> Member unter demselben Namen ist es nicht: das waere
        /// stillschweigend verworfen worden.
        /// </summary>
        private static void VerifySameMember(MemberInfo known, MemberInfo incoming, string replacerName)
        {
            if (known == incoming)
            {
                return;
            }

            throw new InvalidOperationException(
                $"'{replacerName}' is already registered for {known.DeclaringType?.Name}.{known.Name} and cannot "
                + $"be re-registered for {incoming.DeclaringType?.Name}.{incoming.Name}. The first registration "
                + "wins - so the second one would have been dropped without a word.");
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
