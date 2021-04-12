using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ITVComponents.DataExchange.Configuration;
using ITVComponents.DataExchange.Import;

namespace ITVComponents.DataExchange.TextImport
{
    public abstract class TextConsumerBase:ImportConsumerBase<string, TextAcceptanceCallbackParameter>, ITextConsumer
    {
        public TextConsumerBase(ITextSource source, int requiredLines) : base(source)
        {
            AcceptanceConstraints.AddConstraint(new TextAcceptanceConstraint(requiredLines));
        }

        public TextConsumerBase(ITextSource source, int requiredLines, ConstConfigurationCollection virtualColumns)
            : base(source, virtualColumns)
        {
            AcceptanceConstraints.AddConstraint(new TextAcceptanceConstraint(requiredLines));
        }
    }
}
