using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Options
{
    public abstract class ContextResolveOptions
    {
        private Dictionary<string, Func<IServiceProvider, string, string, object>> factories = new Dictionary<string, Func<IServiceProvider, string, string, object>>();

        /// <summary>
        /// Initializes a new instance of the ForeignKeySourceOptions class
        /// </summary>
        public ContextResolveOptions()
        {
            Factories = new ReadOnlyDictionary<string, Func<IServiceProvider, string, string, object>>(factories);
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether to use dynamic Data-Adapters for context-actions
        /// </summary>
        public bool UseDynamicDataAdapters{get;set;}

        /// <summary>
        /// Gets a Dictionary containing factories
        /// </summary>
        public IReadOnlyDictionary<string, Func<IServiceProvider, string, string, object>> Factories { get; }

        /// <summary>
        /// Registers a factory in this options object
        /// </summary>
        /// <param name="key">the key of a ForeignKey providing data-context</param>
        /// <param name="factory">the factory-method that is used to get the required datacontext</param>
        /// <returns>this object for method chaining</returns>
        public ContextResolveOptions RegisterService(string key, Func<IServiceProvider, string, string, object> factory)
        {
            factories.Add(key, factory);
            return this;
        }
    }
}
