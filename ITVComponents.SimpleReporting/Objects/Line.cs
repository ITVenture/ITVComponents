using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.SimpleReporting.Objects
{
    public class Line:IDrawingObject
    {
        private RectangleF objectRegion;
        private PointF point1;
        private PointF point2;
        public int ObjectId { get; set; }

        public RectangleF ObjectRegion
        {
            get { return objectRegion; }
            set { }
        }

        public PointF Point1
        {
            get { return point1; }
            set
            {
                point1 = value;
                CalculateRegion();
            }
        }

        public PointF Point2
        {
            get { return point2; }
            set
            {
                point2 = value;
                CalculateRegion();
            }
        }

        public Color LineColor { get; set; }

        private void CalculateRegion()
        {
            float x = point1.X < point2.X ? point1.X : point2.X;
            float y = point1.Y < point2.Y ? point1.Y : point2.Y;
            float w = Math.Abs(point1.X - point2.X);
            float h = Math.Abs(point1.Y - point2.Y);
            objectRegion = new RectangleF(x, y, w, h);
        }
    }
}
