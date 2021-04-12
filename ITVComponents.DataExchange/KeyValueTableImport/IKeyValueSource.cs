using System.Collections.Generic;
using ITVComponents.DataExchange.Import;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.KeyValueTableImport
{
    public interface IKeyValueSource :
        IImportSource
            <IBasicKeyValueProvider, KeyValueAcceptanceCallbackParameter>
    {
        
    }
}
