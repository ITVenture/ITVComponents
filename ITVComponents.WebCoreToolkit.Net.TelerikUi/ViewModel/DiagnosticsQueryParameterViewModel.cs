using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel
{
    public class DiagnosticsQueryParameterViewModel
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
    }
}
