using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.Expressions.Models
{
    public abstract class FilterBase
    {
        /// <summary>
        /// Gets or sets a value that can be used during processing. this value is ignored by any serializers
        /// </summary>
        [JsonIgnore]
        public object ProcessingInfo { get; set; }

        protected abstract string DescribeFilter();
        public override string ToString()
        {
            return DescribeFilter();
        }
    }
}