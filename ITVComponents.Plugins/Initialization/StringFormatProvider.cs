using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Extensions;

namespace ITVComponents.Plugins.Initialization
{
    public abstract class StringFormatProvider:IPlugin
    {
        private StringFormatProvider parent;

        public string UniqueName { get; set; }

        /// <summary>
        /// Processes a raw-string and uses it as format-string of the configured const-collection
        /// </summary>
        /// <param name="rawString">the raw-string that was read from a plugin-configuration string</param>
        /// <returns>the format-result of the raw-string</returns>
        public string ProcessLiteral(string rawString, Dictionary<string,object> customStringFormatArguments)
        {
            var formatPrototype = new Dictionary<string, object>();
            FillDictionary(formatPrototype);
            customStringFormatArguments ??= new();
            customStringFormatArguments = formatPrototype.ExtendDictionary(customStringFormatArguments);
            if (parent != null)
            {
                formatPrototype.Clear();
                parent.FillDictionary(formatPrototype);
                customStringFormatArguments = formatPrototype.ExtendDictionary(customStringFormatArguments);
            }

            return FormatStringInternal(rawString, customStringFormatArguments);
        }

        protected abstract string FormatStringInternal(string rawString, Dictionary<string, object> customStringFormatArguments);

        protected abstract void FillDictionary(IDictionary<string, object> values);

        public void Dispose()
        {
            Dispose(true);
            OnDisposed();
        }


        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
