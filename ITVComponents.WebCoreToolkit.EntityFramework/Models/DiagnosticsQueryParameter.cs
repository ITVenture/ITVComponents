using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    public class DiagnosticsQueryParameter
    {
        [Key]
        public int DiagnosticsQueryParameterId { get; set; }

        public int DiagnosticsQueryId{get;set;}

        [MaxLength(128),Required]
        public string ParameterName { get;set; }

        public QueryParameterTypes ParameterType { get; set; }

        [MaxLength(64)]
        public string Format{get;set;}

        public bool Optional { get; set; }

        public string DefaultValue { get; set; }

        [ForeignKey(nameof(DiagnosticsQueryId))]
        public virtual DiagnosticsQuery DiagnosticsQuery { get; set; }
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
