using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Plugins
{
    public interface INotifyDisposed:IDisposable
    {
        event EventHandler Disposed;
    }
}
