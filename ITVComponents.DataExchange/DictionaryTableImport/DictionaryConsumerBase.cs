using System;
using System.Collections.Generic;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.DataExchange.Import;
using ITVComponents.DataExchange.KeyValueTableImport;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.DictionaryTableImport
{
    public abstract class DictionaryConsumerBase<TValue>:ImportConsumerBase<IDictionary<string,TValue>, DictionaryAcceptanceCallbackParameter<TValue>>
    {
        protected DictionaryConsumerBase(IImportSource<IDictionary<string,TValue>, DictionaryAcceptanceCallbackParameter<TValue>> source, ConstConfigurationCollection virtualColumns) : base(source, virtualColumns)
        {
        }
    }
}
