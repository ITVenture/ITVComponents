using System;
using ITVComponents.DataExchange.Configuration;

namespace ITVComponents.DataExchange.TextImport.Config
{
    [Serializable]
    public class RegexTextConsumerConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the RegexTextConsumerConfiguration class
        /// </summary>
        public RegexTextConsumerConfiguration()
        {
            Regexes = new RegexConfigurationCollection();
            Columns = new ColumnConfigurationCollection();
            VirtualColumns = new ConstConfigurationCollection();
        }

        /// <summary>
        /// Gets or sets the Name of this Configuration item
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the number of lines that is required for the Consumer to work properly
        /// </summary>
        public int RequiredLines { get; set; }

        /// <summary>
        /// Gets a Collection of regexes that can be used by the RegexTextConsumer
        /// </summary>
        public RegexConfigurationCollection Regexes { get; private set; }

        /// <summary>
        /// Gets a collection of columns that must be filled by the RegexTextConsumer
        /// </summary>
        public ColumnConfigurationCollection Columns { get; private set; }

        /// <summary>
        /// Gets a collection of Columns that are not evaluated from the datasource, but from Expressions
        /// </summary>
        public ConstConfigurationCollection VirtualColumns { get; private set; }
    }
}