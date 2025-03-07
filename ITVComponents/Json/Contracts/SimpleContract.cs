using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Json.Contracts
{
    internal class SimpleContract:IManualSerializer
    {
        public object Value { get; set; }
        public IList<ManualSerializationData> Data { get; set; }
        public void GetObjectData()
        {
            Data.Add(ManualSerializationData.FromValue("_", Value));
        }

        public void ApplyObjectData()
        {
            Value = Data.FirstOrDefault(n => n.PropertyName == "_")?.Data;
        }
    }
}
