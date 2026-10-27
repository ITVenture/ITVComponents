using System;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Attributes
{
    /// <summary>
    /// Bekennt ein ViewModel zu einem Entitaetstyp: ein generischer Controller, dessen ViewModel-Argument per
    /// <see cref="CustomGenericTypeArgAttribute.EntityParameter"/> an diese Entitaet gebunden ist, nimmt dann dieses
    /// ViewModel statt der Vorgabe - ohne Eintrag in <c>ViewConfig:CustomViewGenericArgs</c>.
    /// </summary>
    /// <remarks>
    /// Gilt auch fuer Ableitungen des Entitaetstyps. Ein offener generischer Typ (<c>typeof(HierarchyWebPlugin&lt;,,&gt;)</c>)
    /// passt auf jede Schliessung. Bekennen sich mehrere ViewModels gleich nah zur selben Entitaet, gilt die Vorgabe und es
    /// wird ein Fehler geloggt.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ViewModelForEntityAttribute : Attribute
    {
        public ViewModelForEntityAttribute(Type entityType)
        {
            EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        }

        public Type EntityType { get; }
    }
}
