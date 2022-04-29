using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ITVComponents.Threading;

namespace ITVComponents.ExtendedFormatting
{
    public class KeyValueFormatter:ObjectFormatter
    {
        /// <summary>
        /// current indents for object formatting
        /// </summary>
        private ThreadLocal<int> indent;

        /// <summary>
        /// Initializes a new instance of the KeyValueFormatter class
        /// </summary>
        public KeyValueFormatter() : base(typeof(IBasicKeyValueProvider))
        {
            indent = new ThreadLocal<int>();
        }

        #region Overrides of ObjectFormatter

        /// <summary>
        /// Formats the given object using this formatter
        /// </summary>
        /// <param name="targetObject">the target object to format</param>
        /// <returns>a formatted string representing the given object</returns>
        public override string Format(object targetObject)
        {
            IBasicKeyValueProvider provider = targetObject as IBasicKeyValueProvider;
            StringBuilder bld = new StringBuilder();
            var tmp = (from t in provider.Keys orderby t select new {t.Length, Key=t, Value = provider[t]}).ToArray();
            int ml = tmp.Max(n => n.Length);
            string frmt = $"{{2}}{{0,-{ml}}}{{3}}{{1:obj}}{Environment.NewLine}";
            int currentIndent = 0;
            if (indent.IsValueCreated && indent.Value != -1)
            {
                currentIndent = indent.Value;
            }
            string padString = new string(' ', currentIndent);
            string lineString = currentIndent == 0 ? "" : Environment.NewLine;
            indent.Value = currentIndent + 3;
            bool ok = false;
            try
            {
                foreach (var item in tmp)
                {
                    ok = true;
                    bld.AppendFormat(frmt, item.Key, item.Value, padString, lineString);
                }
            }
            finally
            {
                if (currentIndent != 0)
                {
                    indent.Value = currentIndent;
                }
                else
                {
                    indent.Value = -1;
                }
            }

            return ok ? bld.ToString(0, bld.Length - 2) : string.Empty;
        }

        #endregion
    }
}
