using ITVComponents.Json.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Messages.ProtocolHelper
{
    internal class TypedArray: IManualSerializer
    {
        public IList<ManualSerializationData> Data { get; set; }

        public object[] RawData { get; set; }

        public void GetObjectData()
        {
            if (RawData != null)
            {
                (from t in RawData select ManualSerializationData.FromValue("_", t)).ForEach(Data.Add);
            }
        }

        public void ApplyObjectData()
        {
            RawData = (from t in Data select t.Data).ToArray();
        }
    }
}
