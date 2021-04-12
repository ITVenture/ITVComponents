using System;

namespace ITVComponents.InterProcessCommunication.Shared.Helpers
{
    [Serializable]
    public class ObjectAvailabilityResult
    {
        public bool Available { get; set; }

        public string Message { get; set; }
    }
}
