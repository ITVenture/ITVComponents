using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.SimpleReporting.Objects;

namespace ITVComponents.SimpleReporting.Internal
{
    internal interface IInternalDocument:IDocument
    {
        /// <summary>
        /// Gets all layers
        /// </summary>
        IEnumerable<Layer> Layers { get; }

        /// <summary>
        /// Adds an object to the specified layer
        /// </summary>
        /// <param name="layerName">the layer on which to add the object</param>
        /// <param name="page">the page on which the object was created</param>
        /// <param name="drawingObject">the drawing-object to add on the given page</param>
        void AddLayerObject(string layerName, Page page, IDrawingObject drawingObject);

        /// <summary>
        /// Adds an object to the specified layer
        /// </summary>
        /// <param name="layerName">the layer on which to add the object</param>
        /// <param name="page">the page on which the object was created</param>
        /// <param name="drawingObjects">the set of drawing-objects to add on the given page</param>
        void AddLayerObjects(string layerName, Page page, IEnumerable<IDrawingObject> drawingObjects);
    }
}
