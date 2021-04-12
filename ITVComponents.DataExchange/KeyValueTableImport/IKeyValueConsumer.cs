using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.DataExchange.Import;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.KeyValueTableImport
{
    public interface IKeyValueConsumer :
        IImportConsumer
            <IBasicKeyValueProvider, KeyValueAcceptanceCallbackParameter>
    {
    }
}
