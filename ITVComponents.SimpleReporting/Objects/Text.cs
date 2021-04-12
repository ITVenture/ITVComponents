using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.SimpleReporting.ObjectPositioning;

namespace ITVComponents.SimpleReporting.Objects
{
    public class Text:IDrawingObject, IVirtualPositionElement
    {
        #region Implementation of IDrawingObject

        public int ObjectId { get; set; }
        public RectangleF ObjectRegion { get; set; }
        public float LineSpacing { get; set; }
        public string TextValue { get; set; }
        public string FontName { get; set; }
        public float FontSize { get; set; }
        public float Rotation { get; set; }
        public FontStyle FontStyle { get; set; }
        public StringAlignment HorizontalAlignment { get; set; }
        public StringAlignment VerticalAlignment { get; set; }

        #endregion

        #region Implementation of IVirtualPositionElement

        public float? VirtualBottom { get; set; }
        public Color TextColor { get; set; }

        #endregion
    }
}
