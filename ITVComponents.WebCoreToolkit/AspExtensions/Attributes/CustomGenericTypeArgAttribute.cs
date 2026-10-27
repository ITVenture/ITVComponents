using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Attributes
{
    /// <summary>
    /// Die Vorgabe fuer ein Typargument eines generischen Controllers, das nicht aus dem Kontext kommt (etwa ein ViewModel).
    /// </summary>
    /// <remarks>
    /// Reihenfolge der Aufloesung (<see cref="AssemblyPartWithGenerics"/>): Kontext-Argumente, dann ausdrueckliche
    /// Konfiguration (<c>ViewConfig:CustomViewGenericArgs</c>), dann - mit <see cref="EntityParameter"/> - ein Typ, der sich
    /// per <see cref="ViewModelForEntityAttribute"/> zum Entitaetstyp bekennt, zuletzt <see cref="Type"/>.
    /// </remarks>
    [AttributeUsage(validOn:AttributeTargets.Class|AttributeTargets.Method, AllowMultiple = true)]
    public class CustomGenericTypeArgAttribute:Attribute
    {
        public string Name { get; }

        public Type Type { get; }

        /// <summary>
        /// Der Typparameter, dessen Entitaetstyp das Argument bestimmt (z.B. <c>TWebPlugin</c> fuer
        /// <c>TWebPluginViewModel</c>). Ist er gesetzt, gewinnt eine Ableitung von <see cref="Type"/>, die sich per
        /// <see cref="ViewModelForEntityAttribute"/> zu diesem Entitaetstyp bekennt.
        /// </summary>
        public string EntityParameter { get; set; }

        public CustomGenericTypeArgAttribute(string name, Type type)
        {
            this.Name=name;
            this.Type=type;
        }
    }
}
