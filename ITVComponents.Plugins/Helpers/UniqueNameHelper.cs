using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins.Initialization;

namespace ITVComponents.Plugins.Helpers
{
    internal class UniqueNameHelper
    {
        private string uniqueName;
        private string uniqueNameRaw;
        private Dictionary<string, object> customVariables;
        private StringFormatProvider stringLiteralFormatter;

        public UniqueNameHelper(string uniqueNameRaw, Dictionary<string, object> customVariables,
            StringFormatProvider stringLiteralFormatter)
        {
            this.uniqueNameRaw = uniqueNameRaw;
            this.customVariables = customVariables;
            this.stringLiteralFormatter = stringLiteralFormatter;
        }
        public string UniqueNameRaw
        {
            get => uniqueNameRaw;
        }

        public Dictionary<string, object> CustomVariables
        {
            get => customVariables;
        }

        public string UniqueName => uniqueName ??= BuildUniqueName();

        private string BuildUniqueName()
        {
            string retVal = uniqueNameRaw;
            if (uniqueNameRaw.StartsWith("$") && customVariables != null && stringLiteralFormatter != null)
            {
                retVal = stringLiteralFormatter.ProcessLiteral(retVal.Substring(1), customVariables);
            }

            return retVal;
        }
    }
}
