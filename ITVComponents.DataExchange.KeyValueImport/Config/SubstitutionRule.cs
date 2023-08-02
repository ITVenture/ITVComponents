using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ITVComponents.DataExchange.KeyValueImport.Config
{
    [Serializable]
    public class SubstitutionRule
    {
        public string GroupTag { get; set; }

        public string RegexPattern { get;set; }

        public RegexOptions RegexOptions { get;set; }

        public string ReplaceValue { get; set; }
    }
}
