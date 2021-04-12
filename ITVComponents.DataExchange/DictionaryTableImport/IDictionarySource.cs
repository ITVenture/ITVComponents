using System.Collections.Generic;
using ITVComponents.DataExchange.Import;

namespace ITVComponents.DataExchange.DictionaryTableImport
{
    public interface IDictionarySource<TValue> :
        IImportSource
            <IDictionary<string, TValue>, DictionaryAcceptanceCallbackParameter<TValue>>
    {
        
    }
}
