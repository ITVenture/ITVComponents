using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Formatting.Parser.Model
{
    public class FormattableString
    {
        private readonly string pattern;
        private readonly CodeElement[] elements;
        private readonly ScriptingPolicy policy;
        private readonly Func<string, string, string, object> argumentsCallback;
        private object src;
        private string result;

        public FormattableString(string pattern, CodeElement[] elements, ScriptingPolicy policy, Func<string, string, string, object> argumentsCallback)
        {
            this.pattern = pattern;
            this.elements = elements;
            this.policy = policy;
            this.argumentsCallback = argumentsCallback;
        }

        public void Bind(object src)
        {
            this.src = src;
        }

        public override string ToString()
        {
            return result ??= BindElements();
        }

        public static implicit operator string(FormattableString srcForm)
        {
            return srcForm.ToString();
        }

        private string BindElements()
        {
            var pol = policy ?? TextFormat.DefaultFormatPolicy;
            Scope s = new Scope(new Dictionary<string, object> { { "$data", src } }, pol);
            s.ImplicitContext = "$data";
            using var ctx = ExpressionParser.BeginRepl(s,
                args => DefaultCallbacks.PrepareDefaultCallbacks(args.Scope, args.ReplSession), pol);
            object[] parsedData = new object[elements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                parsedData[i] = BindElement(ctx, elements[i], pol);
            }

            return string.Format(pattern, parsedData);
        }

        private object BindElement(IDisposable ctx, CodeElement element, ScriptingPolicy pol)
        {
            var lmcode = element.Code;
            if (lmcode == ".")
            {
                lmcode = "$data";
            }
            object result = !element.IsBlock
                ? ExpressionParser.Parse(lmcode, ctx)
                :ExpressionParser.ParseBlock(lmcode, ctx);
            if (element.RecursionDepth > 0)
            {
                StringFormatParser tok = new StringFormatParser();
                for (int i = 0; i < element.RecursionDepth; i++)
                {
                    if (result is not string s)
                    {
                        if (result is not FormattableString fs)
                        {
                            throw new InvalidOperationException("String expected for recursive execution");
                        }

                        s = fs;
                    }

                    result = tok.FormatString(src, s, null, argumentsCallback, pol);
                }
            }

            if (element.CustomFormatter != null)
            {
                result = element.CustomFormatter.ApplyFormat(element.Code, result, argumentsCallback);
            }

            return result;
        }
    }
}
