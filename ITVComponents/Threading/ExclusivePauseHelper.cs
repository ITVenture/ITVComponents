﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.Threading
{
    public class ExclusivePauseHelper: IDisposable
    {
        private readonly IDisposable inner;
        private readonly object obj;

        public ExclusivePauseHelper(Func<IDisposable> inner):this(inner,null)
        {

        }

        public ExclusivePauseHelper(Func<IDisposable> inner, object obj)
        {
            if (obj != null)
            {
                this.obj = obj;
                if (!Monitor.IsEntered(obj))
                {
                    throw new InvalidOperationException(
                        "This can only be used when the current threads holds the lock on the given object!");
                }

                Monitor.Exit(obj);
            }
            this.inner = inner();
        }

        public void Dispose()
        {
            inner?.Dispose();
            if (obj != null)
            {
                Monitor.Enter(obj);
            }
        }
    }
}
