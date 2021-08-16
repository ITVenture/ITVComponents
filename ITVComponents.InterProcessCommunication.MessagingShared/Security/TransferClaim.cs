using System.Collections.Generic;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Security
{
    public class TransferClaim
    {
        public string Value { get; set; }
        public string Issuer { get; set; }
        public Dictionary<string, string> Properties { get; set; }
        public string OriginalIssuer { get; set; }
        public string Type { get; set; }
        public string ValueType { get; set; }
    }
}
