﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Logging
{
    public interface ILogCollectorService
    {
        /// <summary>
        /// /Adds an event to a queue that is periodically being flushed
        /// </summary>
        /// <param name="logLevel">the logLevel of the event</param>
        /// <param name="category">the category of the logged event</param>
        /// <param name="title">the event-title</param>
        /// <param name="message">a message that was generated by a module</param>
        void AddEvent(LogLevel logLevel, string category, string title, string message);
    }
}
