using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Models
{
    public class WebPluginGenericParam
    {
        [MaxLength(200)]
        public string GenericTypeName { get; set; }

        [MaxLength(2048)]
        public string TypeExpression { get; set; }
    }
}
