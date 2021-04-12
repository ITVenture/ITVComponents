using System.Collections.Generic;
using ITVComponents.DataExchange.Import;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.DictionaryTableImport
{
    public interface IDictionaryConsumer<TValue> :
        IImportConsumer
            <IDictionary<string,TValue>, DictionaryAcceptanceCallbackParameter<TValue>>
    {
    }
}
