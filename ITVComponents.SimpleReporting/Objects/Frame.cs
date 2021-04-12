using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.SimpleReporting.Objects
{
    public class Frame:IDrawingObject
    {
        public int ObjectId { get; set; }

        public RectangleF ObjectRegion { get; set; }

        public Color FillColor { get; set; }

        public Color BorderColor { get; set; }
    }
}
