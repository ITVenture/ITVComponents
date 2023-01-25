using ITVComponents.Plugins.Initialization;

namespace ITVComponents.Plugins.Helpers
{
    public static class StringHelper 
    {

        /// <summary>
        /// Applies a string-format on a raw-type expression if required
        /// </summary>
        /// <param name="typeExpression">the type-expression found in the a configuration</param>
        /// <param name="args">the arguments that were retrieved from an event requesting generic types</param>
        /// <returns>a re-formatted string that provides a type expression</returns>

        public static string ApplyFormat(this string typeExpression, ImplementGenericTypeEventArgs args)
        {
            return typeExpression.ApplyFormat(args.Formatter);
        }

        /// <summary>
        /// Applies a string-format on a raw-type expression if required
        /// </summary>
        /// <param name="typeExpression">the type-expression found in the a configuration</param>
        /// <param name="formatter">the formatter that is used to format strings inside the calling factory</param>
        /// <returns>a re-formatted string that provides a type expression</returns>

        public static string ApplyFormat(this string typeExpression, IStringFormatProvider formatter)
        {
            var retVal = typeExpression;
            if (formatter != null && typeExpression.StartsWith("$"))
            {
                retVal = formatter.ProcessLiteral(typeExpression.Substring(1), null);
            }

            return retVal;
        }
    }
}
