using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.SimpleReporting.Objects
{
    public interface IDrawingObject
    {
        int ObjectId { get; set; }

        RectangleF ObjectRegion { get; set; }
    }
}
