using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DIIntegration
{
    public sealed class DummyUserAwareDbContext:DbContext, IUserAwareContext
    {
        public string CurrentUserName { get; }
    }
}
