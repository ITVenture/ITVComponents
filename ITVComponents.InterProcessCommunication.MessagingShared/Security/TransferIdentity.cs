using System.Collections.Generic;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Security
{
    public class TransferIdentity
    {
        public string NameType { get; set; }
        public string RoleType { get;set; }
        public string AuthenticationType { get; set; }
        public bool IsAuthenticated { get; set; }
        public string Label { get; set; }
        public List<TransferClaim> Claims { get; set; } = new List<TransferClaim>();
    }
}
