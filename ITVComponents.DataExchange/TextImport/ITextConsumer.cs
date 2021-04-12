using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.DataExchange.Import;

namespace ITVComponents.DataExchange.TextImport
{
    public interface ITextConsumer:IImportConsumer<string, TextAcceptanceCallbackParameter>
    {
    }
}
