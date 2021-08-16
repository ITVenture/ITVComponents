using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub.Factory
{
    public interface IHubConnectionFactory
    {
        IHubConnection CreateConnection();
    }
}
