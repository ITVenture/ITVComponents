using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.ForeignKeys;

namespace ITVComponents.WebCoreToolkit.EntityFramework
{
    public interface IForeignKeyProviderWithOptions:IForeignKeyProvider
    {
        ForeignKeyOptions DefaultFkOptions { get; protected internal set; }
    }
}
