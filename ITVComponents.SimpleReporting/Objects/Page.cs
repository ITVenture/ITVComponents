using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.SimpleReporting.Config;
using ITVComponents.SimpleReporting.Internal;

namespace ITVComponents.SimpleReporting.Objects
{
    public class Page
    {
        private List<IDrawingObject> objects = new List<IDrawingObject>();

        private IInternalDocument parent;

        internal Page(IInternalDocument parent)
        {
            this.parent = parent;
        }

        public RectangleF PageRectangle
        {
            get
            {
                return
                    Config.GetValue<RectangleF>(
                        "PageRectangle");
            }
            set
            {
                Config.SetValue<RectangleF>(
                      "PageRectangle", value);
            }
        }

        public RectangleF ContentArea
        {
            get
            {
                return
                    Config.GetValue<RectangleF>(
                        "ContentArea");
            }
            set
            {
                Config.SetValue<RectangleF>(
                    "ContentArea", value);
            }
        }

        public DocumentConfiguration Config { get; set; }

        public void AddObject(IDrawingObject target, string layerName)
        {
            if (layerName == null)
            {
                objects.Add(target);
            }
            else
            {
                parent.AddLayerObject(layerName, this, target);
            }
        }

        public void AddObjects(IEnumerable<IDrawingObject> targets, string layerName)
        {
            if (layerName == null)
            {
                objects.AddRange(targets);
            }
            else
            {
                parent.AddLayerObjects(layerName, this, targets);
            }
        }

        public IEnumerable<LayerObject> GetObjects()
        {
            foreach (IDrawingObject obj in objects)
            {
                yield return new LayerObject {Layer = null, Page = this, Object = obj};
            }

            foreach (var obj in parent.GetLayerObjects(this))
            {

                yield return obj;
            }
        }

        public IDrawingObject[] PopObjects(int objectId)
        {
            var retVal = objects.Where(n => n.ObjectId == objectId).ToArray();
            retVal.ForEach(obj => objects.Remove(obj));
            return retVal;
        }
    }
}
