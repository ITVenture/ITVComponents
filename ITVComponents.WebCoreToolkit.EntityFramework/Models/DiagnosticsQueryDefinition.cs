using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    public class DiagnosticsQueryDefinition
    {
        public string DiagnosticsQueryName { get; set; }

        public string DbContext { get; set; }

        public bool AutoReturn { get; set; }

        public string QueryText { get; set; }

        public string Permission { get; set; }

        public List<DiagnosticsQueryParameterDefinition> Parameters { get;set; } = new List<DiagnosticsQueryParameterDefinition>();
    }
}
