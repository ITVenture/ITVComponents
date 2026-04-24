using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.StateMachine.Parallel
{
    public interface IStatusWorker: IDisposable
    {
        void Initialize();
    }
}
