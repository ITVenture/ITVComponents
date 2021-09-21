using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Formatting.Elements
{
    class StringElement:IFormatElement
    {
        #region Implementation of IFormatElement

        public int Start { get; set; }
        public int Length { get; set; }
        public StringBuilder Content { get; } = new StringBuilder();

        #endregion
    }
}
