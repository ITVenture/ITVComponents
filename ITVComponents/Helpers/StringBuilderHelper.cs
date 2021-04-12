using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Helpers
{
    public static class StringBuilderHelper
    {
        public static bool EndsWith(this StringBuilder builder, string text)
        {
            if (builder.Length < text.Length)
                return false;

            var sbLength = builder.Length;
            var textLength = text.Length;
            for (int i = 1; i <= textLength; i++)
            {
                if (text[textLength - i] != builder[sbLength - i])
                    return false;
            }

            return true;
        }
    }
}
