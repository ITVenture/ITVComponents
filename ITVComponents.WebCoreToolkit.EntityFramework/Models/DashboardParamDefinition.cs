using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    public class DashboardParamDefinition
    {
        public string ParameterName { get; set; }

        public InputType InputType { get; set; }

        public string InputConfig { get; set; }
    }

    public enum InputType
    {
        Switch,
        Text,
        MaskedText,
        Number,
        Combo
    }
}
