using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Decisions.DefaultConstraints
{
    public class CallbackConstraint<T> : IConstraint<T> where T : class
    {
        private IDecider parent;

        private Func<T, IDeciderContext, Action<string>, DecisionResult> evaluation;

        public CallbackConstraint(Func<T, IDeciderContext, bool> evaluation) : this((t, c,m) => evaluation(t,c)?DecisionResult.Success:DecisionResult.Fail)
        {

        }

        public CallbackConstraint(Func<T, IDeciderContext, Action<string>, bool> evaluation) : this((t, c, m) => evaluation(t, c,m) ? DecisionResult.Success : DecisionResult.Fail)
        {

        }

        public CallbackConstraint(Func<T, IDeciderContext, DecisionResult> evaluation) : this((t, c, m) => evaluation(t, c))
        {

        }

        public CallbackConstraint(Func<T, IDeciderContext, Action<string>, DecisionResult> evaluation)
        {
            this.evaluation = evaluation;
        }

        public void SetParent(IDecider parent)
        {
            SetParent((IDecider<T>)parent);
        }

        public DecisionResult Verify(object data, out string message)
        {
            return Verify((T)data, out message);
        }

        public void SetParent(IDecider<T> parent)
        {
            if (this.parent != null)
            {
                throw new InvalidOperationException("SetParent can only be called once!");
            }

            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            this.parent = parent;
        }

        public DecisionResult Verify(T data, out string message)
        {
            var bld = new StringBuilder();
            try
            {
                IDeciderContext ctx = null;
                if (parent.IsContextDriven)
                {
                    ctx = parent.Context;
                }

                return evaluation(data, ctx, s => bld.AppendLine(s));
            }
            finally
            {
                message = bld.ToString();
            }
        }
    }
}
