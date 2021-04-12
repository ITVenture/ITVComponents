using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.AutoValues
{
    [AttributeUsage(AttributeTargets.Property,AllowMultiple=false,Inherited = true)]
    public class AutoValueAttribute:Attribute
    {
        /// <summary>
        /// Gets or sets a value that must be set to the given field in order to issue a new value of the underlaying sequence
        /// </summary>
        public string TriggerValue { get; set; }
    }
}
