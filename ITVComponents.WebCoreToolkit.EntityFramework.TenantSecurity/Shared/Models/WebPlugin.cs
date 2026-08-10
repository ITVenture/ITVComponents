using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    [Index(nameof(PluginNameUniqueness),IsUnique=true,Name="IX_UniquePluginName")]
    public class WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>:ITVComponents.WebCoreToolkit.Models.WebPlugin
    where TTenant : Tenant
    where TWebPluginGenericParameter: WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
    {
        [Key]
        public int WebPluginId { get; set; }
        
        public int? TenantId{get;set;}
        
        [DatabaseGenerated(DatabaseGeneratedOption.Computed),MaxLength(1024),Required]
        public string PluginNameUniqueness { get; set; }
        
        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }

        /// <summary>
        /// Wie <see cref="ITVComponents.WebCoreToolkit.Models.WebPlugin.AllowAnonymous"/>, aber an den
        /// Besitzer gebunden: gehoert die Zeile einem Mandanten, ist die Antwort immer <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Die Regel steht hier und nicht beim Aufrufer, weil sie eine Eigenschaft des Modells ist: ein
        /// Mandanten-Plugin ist anonym gar nicht sichtbar, also kann es auch nie anonym geladen werden.
        /// Am Aufrufer waere sie eine Regel, die man an der naechsten Aufrufstelle wieder vergessen kann;
        /// hier greift sie auch dann, wenn die Spalte von Hand gesetzt wurde.
        /// </remarks>
        public override bool AllowAnonymous
        {
            get => base.AllowAnonymous && TenantId == null;
            set => base.AllowAnonymous = value;
        }

        public virtual ICollection<TWebPluginGenericParameter> Parameters { get; set; } = new List<TWebPluginGenericParameter>();
    }
}
