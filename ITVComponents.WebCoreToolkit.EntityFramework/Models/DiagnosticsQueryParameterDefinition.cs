using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    public class DiagnosticsQueryParameterDefinition
    {
        public string ParameterName { get;set; }

        public QueryParameterTypes ParameterType { get; set; }

        public string Format{get;set;}

        public bool Optional { get; set; }

        public string DefaultValue { get; set; }
    }

    public enum QueryParameterTypes
    {
        Boolean,
        DateTime,
        Double,
        Int32,
        Int64,
        String
    }
}
