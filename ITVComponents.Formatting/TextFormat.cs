using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting.DefaultExtensions;
using ITVComponents.Formatting.Elements;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;

namespace ITVComponents.Formatting
{
    public static class TextFormat
    {
        private static ConcurrentDictionary<string, Func<object, string>> customFormatHints =
            new ConcurrentDictionary<string, Func<object, string>>(StringComparer.Ordinal);

        static TextFormat()
        {
            DefaultFormatExtensions.RegisterDefaultFormatExtensions();
        }

        public static string FormatText(this object target, string format)
        {

            var tmp = TokenizeString(format);
            using (var context = CreateScriptingSession(target))
            {
                return string.Concat(from t in tmp select Stringify(t, context));
            }
        }

        public static string FormatText(this object target, string format, CustomExpressionParse customExpressionParser)
        {
            var tmp = TokenizeString(format);
            for (int i = 0; i < tmp.Length; i++)
            {
                if (tmp[i] is FormatElement fmt)
                {
                    bool keep = customExpressionParser(fmt.Content, out var newContent);
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
                        
                        tmp[i] = new StringElement {Content = $"{pref}[{fmt.Content}]", Length = fmt.Length, Start = fmt.Start};
                    }
                    else
                    {
                        fmt.Content = newContent;
                    }
                }
            }
            
            using (var context = CreateScriptingSession(target))
            {
                return string.Concat(from t in tmp select Stringify(t, context));
            }
        }

        public static string FormatText(this IDisposable scriptingContext, string format, CustomExpressionParse customExpressionParser)
        {
            if (ExpressionParser.IsReplSession(scriptingContext))
            {
                var tmp = TokenizeString(format);
                for (int i = 0; i < tmp.Length; i++)
                {
                    if (tmp[i] is FormatElement fmt)
                    {
                        bool keep = customExpressionParser(fmt.Content, out var newContent);
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

                            tmp[i] = new StringElement {Content = $"{pref}[{fmt.Content}]", Length = fmt.Length, Start = fmt.Start};
                        }
                        else
                        {
                            fmt.Content = newContent;
                        }
                    }
                }

                return string.Concat(from t in tmp select Stringify(t, scriptingContext));
            }

            return FormatText((object) scriptingContext, format, customExpressionParser);
        }

        public static string FormatText(IDisposable scriptingContext, string format)
        {
            if (ExpressionParser.IsReplSession(scriptingContext))
            {
                var tmp = TokenizeString(format);
                return string.Concat(from t in tmp select Stringify(t, scriptingContext));
            }

            return FormatText((object) scriptingContext, format);
        }

        public static void AddCustomFormatHint(string hint, Func<object, string> formatFunction)
        {
            customFormatHints.TryAdd(hint, formatFunction);
        }

        private static IDisposable CreateScriptingSession(object target)
        {
            Scope s = new Scope(new Dictionary<string, object> { { "$data", target } });
            s.ImplicitContext = "$data";
            var context = ExpressionParser.BeginRepl(s,
                (i) => DefaultCallbacks.PrepareDefaultCallbacks(i.Scope, i.ReplSession));
            return context;
        }

        private static string Stringify(IFormatElement element, IDisposable sessionContext)
        {
            if (element is StringElement)
            {
                return element.Content;
            }

            FormatElement fe = (FormatElement)element;
            object val = null;
            if (fe.Content == ".")
            {
                fe.Content = "$data";
            }

            if (fe.CodeType == CodeType.Expression || fe.CodeType == CodeType.RecursiveExpression)
            {
                val = ExpressionParser.Parse(fe.Content, sessionContext);
            }
            else
            {
                val = ExpressionParser.ParseBlock(fe.Content,sessionContext);
            }

            if (fe.CodeType == CodeType.RecursiveExpression || fe.CodeType == CodeType.RecursiveBlock)
            {
                if (val is string recVal && !string.IsNullOrEmpty(recVal))
                {
                    val = FormatText(sessionContext, recVal);
                }
            }

            bool useFormat = !string.IsNullOrEmpty(fe.FormatHint) && !customFormatHints.ContainsKey(fe.FormatHint);
            bool preFormat = !string.IsNullOrEmpty(fe.FormatHint) && customFormatHints.ContainsKey(fe.FormatHint);

            if (preFormat)
            {
                val = customFormatHints[fe.FormatHint](val);
            }

            return
                string.Format(
                    $"{{0{(!string.IsNullOrEmpty(fe.FormatLength) ? $",{fe.FormatLength}" : "")}{(useFormat ? $":{fe.FormatHint}" : "")}}}",val);
        }

        private static IFormatElement[] TokenizeString(string s)
        {
            Stack<ParseState> parserStack= new Stack<ParseState>();
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
                if (i < len -3 && t2 == "$£[")
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

                            currentElement = new FormatElement {Start = i};
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
                            i+=2;
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
                                currentElement.Content += o;
                            }
                            else
                            {
                                i++;
                                currentElement.Content += o;
                            }
                        }

                        break;
                    case ParseState.FormatExpression:
                        if (o == "(")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatParenthesis;
                        }
                        else if (o == "[")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatIndexer;
                        }
                        else if (o == "{")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatBracket;
                        }
                        else if (o == "@" && t == "@\"")
                        {
                            currentElement.Length+=2;
                            currentElement.Content += t;
                            parserStack.Push(currentState);
                            i++;
                            currentState = ParseState.FormatVerbatimString;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatString;
                        }
                        else if (o == "?" && t != "?." && t != "?[") //Exclude Null-Propagations
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
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
                        else if (o == "]")
                        {
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
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                        }
                        break;
                    case ParseState.FormatParenthesis:
                        if (o == "@" && t == "@\"")
                        {
                            currentElement.Length += 2;
                            currentElement.Content += t;
                            parserStack.Push(currentState);
                            i++;
                            currentState = ParseState.FormatVerbatimString;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatString;
                        }
                        else if (o == "(")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatParenthesis;
                        }
                        else if (o == ")")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                        }
                        break;
                    case ParseState.FormatBracket:
                        if (o == "@" && t == "@\"")
                        {
                            currentElement.Length += 2;
                            currentElement.Content += t;
                            parserStack.Push(currentState);
                            i++;
                            currentState = ParseState.FormatVerbatimString;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatString;
                        }
                        else if (o == "{")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatBracket;
                        }
                        else if (o == "}")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                        }
                        break;
                    case ParseState.FormatIndexer:
                        if (o == "@" && t == "@\"")
                        {
                            currentElement.Length += 2;
                            currentElement.Content += t;
                            parserStack.Push(currentState);
                            i++;
                            currentState = ParseState.FormatVerbatimString;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatString;
                        }
                        else if (o == "[")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatIndexer;
                        }
                        else if (o == "]")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                        }
                        break;
                    case ParseState.FormatString:
                        if (t == "\\\"")
                        {
                            i++;
                            currentElement.Length += 2;
                            currentElement.Content += t;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                        }
                        break;
                    case ParseState.FormatVerbatimString:
                        if (t == "\"\"")
                        {
                            i++;
                            currentElement.Length += 2;
                            currentElement.Content += t;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                        }
                        
                        
                            break;
                    case ParseState.FormatTerentary:
                        if (o == "@" && t == "@\"")
                        {
                            currentElement.Length += 2;
                            currentElement.Content += t;
                            parserStack.Push(currentState);
                            i++;
                            currentState = ParseState.FormatVerbatimString;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatString;
                        }
                        else if (o == "?" && t != "?." && t != "?[")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            parserStack.Push(currentState);
                            currentState = ParseState.FormatTerentary;
                        }
                        else if (o == ":")
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            currentElement.Content += o;
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
                            ((FormatElement) currentElement).FormatHint += o;
                        }
                        break;
                    case ParseState.FormatterHintString:
                        if (t == "\\\"")
                        {
                            i++;
                            currentElement.Length += 2;
                            ((FormatElement)currentElement).FormatHint += t;
                        }
                        else if (o == "\"")
                        {
                            currentElement.Length++;
                            ((FormatElement)currentElement).FormatHint += o;
                            currentState = parserStack.Pop();
                        }
                        else
                        {
                            currentElement.Length++;
                            ((FormatElement)currentElement).FormatHint += o;
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
                            ((FormatElement)currentElement).FormatLength += o;
                        }
                        break;
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
                elements.Add( currentElement);
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
        FormatterLength
    }
}
