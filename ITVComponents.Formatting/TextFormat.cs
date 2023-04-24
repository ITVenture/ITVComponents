using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting.CustomFormat.Impl;
using ITVComponents.Formatting.DefaultExtensions;
using ITVComponents.Formatting.Elements;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Security;
using ITVComponents.Scripting.CScript.Security.Extensions;
using ITVComponents.Scripting.CScript.Security.Restrictions;
using ICustomFormatter = ITVComponents.Formatting.CustomFormat.ICustomFormatter;
namespace ITVComponents.Formatting
{
    public static class TextFormat
    {
        private static ConcurrentDictionary<string, ICustomFormatter> customFormatHints =
            new ConcurrentDictionary<string, ICustomFormatter>(StringComparer.Ordinal);

        public static ScriptingPolicy DefaultFormatPolicy { get; } = ScriptingPolicy.Default.Configure(n =>
        {
            n.CreateNewInstances = PolicyMode.Deny;
            n.NativeScripting = PolicyMode.Deny;
            n.TypeLoading = PolicyMode.Deny;
        }).Lock();

        public static ScriptingPolicy DefaultFormatPolicyWithPrimitives { get; } = DefaultFormatPolicy
            .WithTypeRestriction(typeof(byte), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(sbyte), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(short), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(ushort), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(int), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(uint), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(long), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(ulong), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(float), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(double), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(decimal), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(string), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(char), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(DateTime), TypeAccessMode.Any, PolicyMode.Allow)
            .WithTypeRestriction(typeof(TimeSpan), TypeAccessMode.Any, PolicyMode.Allow);

        static TextFormat()
        {
            DefaultFormatExtensions.RegisterDefaultFormatExtensions();
        }

        public static string FormatText(this object target, string format, ScriptingPolicy policy = null)
        {
            return FormatText(target, format, default(Func<string, string, string, object>), policy);
        }

        public static string FormatText(this object target, string format, Func<string, string, string, object> argumentsCallback, ScriptingPolicy policy = null)
        {

            var tmp = TokenizeString(format);
            using (var context = CreateScriptingSession(target, policy))
            {
                return string.Concat(from t in tmp select Stringify(t, context, argumentsCallback));
            }
        }

        public static string FormatText(this object target, string format, CustomExpressionParse customExpressionParser, ScriptingPolicy policy = null)
        {
            return FormatText(target, format, customExpressionParser, null, policy);
        }

        public static string FormatText(this object target, string format, CustomExpressionParse customExpressionParser, Func<string, string, string, object> argumentsCallback, ScriptingPolicy policy = null)
        {
            var tmp = TokenizeString(format);
            for (int i = 0; i < tmp.Length; i++)
            {
                if (tmp[i] is FormatElement fmt)
                {
                    bool keep = customExpressionParser(fmt.Content.ToString(), out var newContent);
                    if (!keep)
                    {
                        string pref = "";
                        switch (fmt.CodeType)
                        {
                            case CodeType.Block:
                                pref = "$";
                                break;
                            case CodeType.RecursiveExpression:
                                pref = "£";
                                break;
                            case CodeType.RecursiveBlock:
                                pref = "$£";
                                break;
                        }

                        tmp[i] = new StringElement { Length = fmt.Length, Start = fmt.Start };
                        tmp[i].Content.Append($"{pref}[{fmt.Content}]");
                        if (fmt.IsRecursive && fmt.RecursionDepth != 1)
                        {
                            tmp[1].Content.Append($"{{{fmt.RecursionDepth}}}");
                        }
                    }
                    else
                    {
                        fmt.Content.Clear();
                        fmt.Content.Append(newContent);
                    }
                }
            }

            using (var context = CreateScriptingSession(target, policy))
            {
                return string.Concat(from t in tmp select Stringify(t, context, argumentsCallback));
            }
        }

        public static string FormatText(this IDisposable scriptingContext, string format,
            CustomExpressionParse customExpressionParser)
        {
            return FormatText(scriptingContext, format, customExpressionParser, null);
        }

        public static string FormatText(this IDisposable scriptingContext, string format, CustomExpressionParse customExpressionParser, Func<string, string, string, object> argumentsCallback)
        {
            if (ExpressionParser.IsReplSession(scriptingContext))
            {
                var tmp = TokenizeString(format);
                for (int i = 0; i < tmp.Length; i++)
                {
                    if (tmp[i] is FormatElement fmt)
                    {
                        bool keep = customExpressionParser(fmt.Content.ToString(), out var newContent);
                        if (!keep)
                        {
                            string pref = "";
                            switch (fmt.CodeType)
                            {
                                case CodeType.Block:
                                    pref = "$";
                                    break;
                                case CodeType.RecursiveExpression:
                                    pref = "£";
                                    break;
                                case CodeType.RecursiveBlock:
                                    pref = "$£";
                                    break;
                            }

                            tmp[i] = new StringElement { Length = fmt.Length, Start = fmt.Start };
                            tmp[i].Content.Append($"{pref}[{fmt.Content}]");
                            if (fmt.IsRecursive && fmt.RecursionDepth != 1)
                            {
                                tmp[1].Content.Append($"{{{fmt.RecursionDepth}}}");
                            }
                        }
                        else
                        {
                            fmt.Content.Clear();
                            fmt.Content.Append(newContent);
                        }
                    }
                }

                return string.Concat(from t in tmp select Stringify(t, scriptingContext, argumentsCallback));
            }

            return FormatText((object)scriptingContext, format, customExpressionParser, argumentsCallback);
        }

        public static string FormatText(IDisposable scriptingContext, string format)
        {
            return FormatText(scriptingContext, format, default(Func<string, string, string, object>));
        }

        public static string FormatText(IDisposable scriptingContext, string format, Func<string, string, string, object> argumentsCallback)
        {
            if (ExpressionParser.IsReplSession(scriptingContext))
            {
                var tmp = TokenizeString(format);
                return string.Concat(from t in tmp select Stringify(t, scriptingContext, argumentsCallback));
            }

            return FormatText((object)scriptingContext, format, argumentsCallback);
        }

        public static void AddCustomFormatHint(string hint, Func<object, string> formatFunction)
        {
            customFormatHints.TryAdd(hint, new CallbackFormatter(hint, formatFunction));
        }

        public static void AddCustomFormatHint(ICustomFormatter formatter)
        {
            customFormatHints.TryAdd(formatter.Hint, formatter);
        }

        private static IDisposable CreateScriptingSession(object target, ScriptingPolicy policy = null)
        {
            var pol = policy ?? DefaultFormatPolicy;
            Scope s = new Scope(new Dictionary<string, object> { { "$data", target } },pol);
            s.ImplicitContext = "$data";
            var context = ExpressionParser.BeginRepl(s,
                (i) => DefaultCallbacks.PrepareDefaultCallbacks(i.Scope, i.ReplSession),pol);
            return context;
        }

        private static string Stringify(IFormatElement element, IDisposable sessionContext, Func<string, string, string, object> argumentsCallback)
        {
            if (element is StringElement)
            {
                return element.Content.ToString();
            }

            FormatElement fe = (FormatElement)element;
            object val = null;
            if (fe.Content.Equals("."))
            {
                fe.Content.Clear();
                fe.Content.Append("$data");
            }

            if (fe.CodeType == CodeType.Expression || fe.CodeType == CodeType.RecursiveExpression)
            {
                val = ExpressionParser.Parse(fe.Content.ToString(), sessionContext);
            }
            else
            {
                val = ExpressionParser.ParseBlock(fe.Content.ToString(), sessionContext);
            }

            if (fe.CodeType == CodeType.RecursiveExpression || fe.CodeType == CodeType.RecursiveBlock)
            {
                for (int i = 0; i < fe.RecursionDepth; i++)
                {
                    if (val is string recVal && !string.IsNullOrEmpty(recVal))
                    {
                        val = FormatText(sessionContext, recVal);
                    }
                }
            }

            var fint = fe.FormatHint.ToString();
            bool useFormat = fe.FormatHint.Length != 0 && !customFormatHints.ContainsKey(fint);
            bool preFormat = fe.FormatHint.Length != 0 && customFormatHints.ContainsKey(fint);

            if (preFormat)
            {
                val = customFormatHints[fint].ApplyFormat(fe.Content.ToString(), val, (a,b,c) => argumentsCallback?.Invoke(a,b,c));
            }

            return
                string.Format(
                    $"{{0{(fe.FormatLength.Length != 0 ? $",{fe.FormatLength}" : "")}{(useFormat ? $":{fe.FormatHint}" : "")}}}", val);
        }

        private static IFormatElement[] TokenizeString(string s)
        {
            CodeType[] recursiveBlocks = new[] { CodeType.RecursiveBlock, CodeType.RecursiveExpression };
            Stack<ParseState> parserStack = new Stack<ParseState>();
            ParseState currentState = ParseState.String;
            IFormatElement currentElement = new StringElement();
            int len = s.Length;
            List<IFormatElement> elements = new List<IFormatElement>();
            for (int i = 0; i < len; i++)
            {
                string o = s.Substring(i, 1);
                string t = "";
                if (i < len - 1)
                {
                    t = s.Substring(i, i < len - 1 ? 2 : 1);
                }

                string t2 = "";
                if (i < len - 2 && (t == "$[" || t == "£[" || t == "$£"))
                {
                    t2 = s.Substring(i, 3);
                }
                string t3 = "";
                if (i < len - 3 && t2 == "$£[")
                {
                    t3 = s.Substring(i, 4);
                }
                switch (currentState)
                {
                    case ParseState.String:
                        if (o == "[" && t != "[[")
                        {
                            if (currentElement.Length != 0)
                            {
                                elements.Add(currentElement);
                            }

                            currentElement = new FormatElement { Start = i };
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatExpression;
                        }
                        else if (o == "$" && t == "$[" && t2 != "$[[")
                        {
                            i++;
                            if (currentElement.Length != 0)
                            {
                                elements.Add(currentElement);
                            }

                            currentElement = new FormatElement { Start = i, CodeType = CodeType.Block };
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatExpression;
                        }
                        else if (o == "£" && t == "£[" && t2 != "£[[")
                        {
                            i++;
                            if (currentElement.Length != 0)
                            {
                                elements.Add(currentElement);
                            }

                            currentElement = new FormatElement { Start = i, CodeType = CodeType.RecursiveExpression };
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatExpression;
                        }
                        else if (o == "$" && t == "$£" && t2 == "$£[" && t3 != "$£[[")
                        {
                            i += 2;
                            if (currentElement.Length != 0)
                            {
                                elements.Add(currentElement);
                            }

                            currentElement = new FormatElement { Start = i, CodeType = CodeType.RecursiveBlock };
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatExpression;
                        }
                        else
                        {
                            currentElement.Length++;
                            if (t != "[[" && t != "]]" && t != "$$" && t != "££")
                            {
                                currentElement.Content.Append(o);
                            }
                            else
                            {
                                i++;
                                currentElement.Content.Append(o);
                            }
                        }

                        break;
                    case ParseState.FormatExpression:
                        if (o == "(")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatParenthesis;
                        }
                        else if (o == "[")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatIndexer;
                        }
                        else if (o == "{")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatBracket;
                        }
                        else if (o == "@" && t == "@\"")
                        {
                            currentElement.Length += 2;
                            currentElement.Content.Append(t);
                            parserStack.Push(currentState);
                            i++;
                            currentState = ParseState.FormatVerbatimString;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatString;
                        }
                        else if (o == "?" && t != "?." && t != "?[") //Exclude Null-Propagations
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatTerentary;
                        }
                        else if (o == ":")
                        {
                            currentElement.Length++;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatterHint;
                        }
                        else if (o == ",")
                        {
                            currentElement.Length++;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatterLength;
                        }
                        else if (o == "]" ||
                                 (o == "}" && ((FormatElement)currentElement).IsRecursive))
                        {
                            if (t != "]{")
                            {
                                if (((FormatElement)currentElement).RecursionDepthString.Length != 0)
                                {
                                    ((FormatElement)currentElement).RecursionDepth = int.Parse(((FormatElement)currentElement).RecursionDepthString.ToString());
                                }

                                if (currentElement.Length != 0)
                                {
                                    elements.Add(currentElement);
                                }

                                currentElement = new StringElement();
                                currentElement.Start = i + 1;
                                currentState = parserStack.Pop();
                                if (currentState != ParseState.String)
                                {
                                    throw new FormatException($"Unexpected Token @{i}!");
                                }

                                if (t == "]#")
                                {
                                    i++;
                                }
                            }
                            else
                            {
                                parserStack.Push(currentState);
                                ((FormatElement)currentElement).IsRecursive = true;
                                if (!recursiveBlocks.Contains(((FormatElement)currentElement).CodeType))
                                {
                                    throw new FormatException(
                                        "RecursionDepth is not supported for non-recursive elements!");
                                }

                                currentState = ParseState.RecursionDepth;
                                i++;
                            }
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                        }
                        break;
                    case ParseState.FormatParenthesis:
                        if (o == "@" && t == "@\"")
                        {
                            currentElement.Length += 2;
                            currentElement.Content.Append(t);
                            parserStack.Push(currentState);
                            i++;
                            currentState = ParseState.FormatVerbatimString;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatString;
                        }
                        else if (o == "(")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatParenthesis;
                        }
                        else if (o == ")")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                        }
                        break;
                    case ParseState.FormatBracket:
                        if (o == "@" && t == "@\"")
                        {
                            currentElement.Length += 2;
                            currentElement.Content.Append(t);
                            parserStack.Push(currentState);
                            i++;
                            currentState = ParseState.FormatVerbatimString;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatString;
                        }
                        else if (o == "{")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatBracket;
                        }
                        else if (o == "}")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                        }
                        break;
                    case ParseState.FormatIndexer:
                        if (o == "@" && t == "@\"")
                        {
                            currentElement.Length += 2;
                            currentElement.Content.Append(t);
                            parserStack.Push(currentState);
                            i++;
                            currentState = ParseState.FormatVerbatimString;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatString;
                        }
                        else if (o == "[")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatIndexer;
                        }
                        else if (o == "]")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                        }
                        break;
                    case ParseState.FormatString:
                        if (t == "\\\"")
                        {
                            i++;
                            currentElement.Length += 2;
                            currentElement.Content.Append(t);
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                        }
                        break;
                    case ParseState.FormatVerbatimString:
                        if (t == "\"\"")
                        {
                            i++;
                            currentElement.Length += 2;
                            currentElement.Content.Append(t);
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                        }


                        break;
                    case ParseState.FormatTerentary:
                        if (o == "@" && t == "@\"")
                        {
                            currentElement.Length += 2;
                            currentElement.Content.Append(t);
                            parserStack.Push(currentState);
                            i++;
                            currentState = ParseState.FormatVerbatimString;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatString;
                        }
                        else if (o == "?" && t != "?." && t != "?[")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatTerentary;
                        }
                        else if (o == ":")
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content.Append(o);
                        }
                        break;
                    case ParseState.FormatterHint:
                        if (o == "]")
                        {
                            i--;
                            currentState = parserStack.Pop();
                        }
                        else if (o == ",")
                        {
                            currentElement.Length++;
                            currentState = ParseState.FormatterLength;
                        }
                        else
                        {
                            currentElement.Length++;
                            ((FormatElement)currentElement).FormatHint.Append(o);
                        }
                        break;
                    case ParseState.FormatterHintString:
                        if (t == "\\\"")
                        {
                            i++;
                            currentElement.Length += 2;
                            ((FormatElement)currentElement).FormatHint.Append(t);
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            ((FormatElement)currentElement).FormatHint.Append(o);
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            ((FormatElement)currentElement).FormatHint.Append(o);
                        }
                        break;
                    case ParseState.FormatterLength:
                        if (o == "]")
                        {
                            i--;
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            ((FormatElement)currentElement).FormatLength.Append(o);
                        }
                        break;
                    case ParseState.RecursionDepth:
                    {
                        if (char.IsDigit(o, 0))
                        {
                            if (!((FormatElement)currentElement).IsRecursive)
                            {
                                throw new FormatException($"Recursion depth is invalid at this point.");
                            }

                            ((FormatElement)currentElement).RecursionDepthString.Append(o);
                        }
                        else if (o == "}")
                        {
                            i--;
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            throw new FormatException($"Invalid character in Recursive Depth definition! ({o}).");
                        }

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (currentState != ParseState.String)
            {
                throw new FormatException("Formatstring was not well-formed!");
            }
            if (currentElement.Length != 0)
            {
                elements.Add(currentElement);
            }

            return elements.ToArray();
        }
    }

    /// <summary>
    /// A Callback that allows the provider to replace an expression by a custom expression and return a value indicating whether the expression must be executed.
    /// If false is returned, the expression-object will bereplaces by a literal expression that will not be evaluated by the expression parser
    /// </summary>
    /// <param name="expressionContent">the epxression content</param>
    /// <param name="actualExpression">the expression that actually should be evaluated</param>
    /// <returns>a value indicating whether this is actually an expression (true) or a literal (false)</returns>
    public delegate bool CustomExpressionParse(string expressionContent, out string actualExpression);

    internal enum ParseState
    {
        String,
        FormatExpression,
        FormatParenthesis,
        FormatBracket,
        FormatIndexer,
        FormatString,
        FormatVerbatimString,
        FormatTerentary,
        FormatterHint,
        FormatterHintString,
        FormatterLength,
        RecursionDepth
    }
}
