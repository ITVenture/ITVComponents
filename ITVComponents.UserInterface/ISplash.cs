using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.Plugins;

namespace ITVComponents.UserInterface
{
    public interface ISplash:IPlugin
    {
        void Show();
        void Hide();
    }
}
