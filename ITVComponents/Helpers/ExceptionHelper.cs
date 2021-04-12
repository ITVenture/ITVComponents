using System;
using System.Text;

namespace ITVComponents.Helpers
{
    public static class ExceptionHelper
    {
        /// <summary>
        /// Outlines a spcific exception for the calling class to print the whole error-tree
        /// </summary>
        /// <param name="exception">the exception that was thrown by a component</param>
        /// <returns>the string - representation of the provided exception</returns>
        public static string OutlineException(this Exception exception)
        {
            StringBuilder sbl = new StringBuilder();
            Exception x = exception;
            while (x != null)
            {
                IAutoOutline oln = x as IAutoOutline;
                if (oln == null)
                {
                    sbl.AppendLine($"Type: {x.GetType().FullName}; Message: {x.Message}");
                    sbl.AppendLine(x.StackTrace);
                    sbl.AppendLine(new string('-', 80));
                }
                else
                {
                    sbl.AppendLine(oln.Outline());
                }

                x = x.InnerException;
            }

            return sbl.ToString();
        }

        /// <summary>
        /// Inline - Exceptionhandler for logging simple errors
        /// </summary>
        /// <typeparam name="T">the Exception-Type you want to catch</typeparam>
        /// <param name="action">the action to perform</param>
        /// <param name="exceptionAction">the errorAction to perform</param>
        public static void InlineError<T>(Action action, Action<T> exceptionAction) where T:Exception
        {
            try
            {
                action();
            }
            catch (T ex)
            {
                exceptionAction(ex);
            }
        }

        public static T InlineError<T, TEx>(Func<T> action, Action<TEx> exceptionAction, bool reThrow = false) where TEx : Exception
        {
            try
            {
                return action();
            }
            catch (TEx ex)
            {
                exceptionAction(ex);
                if (reThrow)
                {
                    throw;
                }
            }

            return default(T);
        }

        /// <summary>
        /// Identifies an exception that is able to outline itself automatically
        /// </summary>
        public interface IAutoOutline
        {
            /// <summary>
            /// Outlines the content of the current exception
            /// </summary>
            /// <returns></returns>
            string Outline();
        }
    }
}
