using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.Operating;

namespace ITVComponents.DataExchange.KeyValueImport.TextSource.Escaping
{
    public class DefaultEscapeStrategy:EscapeStrategy
    {
        public DefaultEscapeStrategy(string name) : base(name)
        {
        }

        /// <summary>
        /// Removes escape-characters from a string and returns the effective value
        /// </summary>
        /// <param name="raw">the raw-value of the string</param>
        /// <returns>the un-escaped value of the string</returns>
        public override string Unescape(string raw)
        {
            return StringHelper.Parse($"\"{raw}\"");
        }
    }
}
