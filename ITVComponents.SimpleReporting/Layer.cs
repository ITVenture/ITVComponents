using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.SimpleReporting.Objects;

namespace ITVComponents.SimpleReporting
{
    public class Layer
    {
        public Layer(string layerName)
        {
            LayerName = layerName;
        }

        public string LayerName { get; }

        public LayerLocation Location { get; set; } = LayerLocation.Back;

        public bool Show { get; set; } = true;

        public bool Print { get; set; } = true;

        internal List<LayerObject> Objects { get; } = new List<LayerObject>();
    }

    public enum LayerLocation
    {
        Front,
        Back
    }
}
