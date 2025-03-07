using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.Json.Contracts
{
    public interface IManualSerializer:IJsonOnSerializing, IJsonOnSerialized
    {
        public IList<ManualSerializationData> Data { get; set; }

        void IJsonOnSerialized.OnSerialized()
        {
            Data = null;
        }

        void IJsonOnSerializing.OnSerializing()
        {
            Data = new List<ManualSerializationData>();
            GetObjectData();
        }

        void OnDeserialized(JsonSerializerOptions serializationOptions)
        {
            foreach (var msd in Data)
            {
                msd.ReadValues(serializationOptions);
            }

            ApplyObjectData();
            Data = null;
        }

        void GetObjectData();

        void ApplyObjectData();
    }
}
