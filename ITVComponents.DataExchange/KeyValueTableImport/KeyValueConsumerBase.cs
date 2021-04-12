using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.DataAccess;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.DataExchange.Import;
using ITVComponents.ExtendedFormatting;

namespace ITVComponents.DataExchange.KeyValueTableImport
{
    public abstract class KeyValueConsumerBase:ImportConsumerBase<IBasicKeyValueProvider, KeyValueAcceptanceCallbackParameter>
    {
        protected KeyValueConsumerBase(IImportSource<IBasicKeyValueProvider, KeyValueAcceptanceCallbackParameter> source, ConstConfigurationCollection virtualColumns) : base(source, virtualColumns)
        {
        }
    }
}
