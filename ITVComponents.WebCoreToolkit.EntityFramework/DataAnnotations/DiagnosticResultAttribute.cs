using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Property,AllowMultiple=false,Inherited=true)]
    public class DiagnosticResultAttribute:Attribute
    {
        public string DiagnosticQueryName { get; }

        public DiagnosticResultAttribute(string diagnosticQueryName)
        {
            this.DiagnosticQueryName = diagnosticQueryName;
        }
    }
}
