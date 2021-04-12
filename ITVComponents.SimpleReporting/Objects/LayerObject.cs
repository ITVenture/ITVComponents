using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.SimpleReporting.Objects
{
    public class LayerObject
    {
        public Layer Layer { get; set; }

        public Page Page { get; set; }

        public IDrawingObject Object { get; set; }
    }
}
