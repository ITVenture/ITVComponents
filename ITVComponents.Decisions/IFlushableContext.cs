﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Decisions
{
    public interface IFlushableContext
    {
        /// <summary>
        /// Flushes all settings in the current context
        /// </summary>
        void Flush();
    }
}
