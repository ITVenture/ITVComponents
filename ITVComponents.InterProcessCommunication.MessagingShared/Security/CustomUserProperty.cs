using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Security
{
    [Serializable]
    public class CustomUserProperty
    {
        public string PropertyName{get;set;}

        public string Value { get; set; }

        public CustomUserPropertyType PropertyType { get; set; }
    }
}
