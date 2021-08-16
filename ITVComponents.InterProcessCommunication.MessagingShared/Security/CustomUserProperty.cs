using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Security
{
    [Serializable]
    public class CustomUserProperty
    {
        public string PropertyName{get;set;}

        public string Value { get; set; }
    }
}
