using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Cloning.Model
{
    /// <summary>
    /// Declares an assignment-holder that is used to create an Assignment Lambda-expression
    /// </summary>
    public class AssignmentHolder
    {
        public PropertyInfo Source { get; set; }
        public PropertyInfo Destination { get; set; }
        public bool SourceNullable { get; set; }

        public bool DestinationNullable { get; set; }
        //public MethodInfo Getter{get;set;}
        public MethodInfo Setter { get; set; }
        public Type PropType { get; set; }
        public bool UseConvert { get; set; }

        public bool SpecifyDateTimeAsUtc { get; set; }
    }
}
