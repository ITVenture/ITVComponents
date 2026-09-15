using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel
{
    public class PermissionSetViewModel
    {
        public int AppPermissionSetId { get; set; }

        /// <summary>
        /// Das Template, zu dem dieses Buendel gehoert - Pflicht.
        /// </summary>
        /// <remarks>
        /// Seit PRE240 gehoert ein Buendel genau EINEM Template. Das Feld fehlte hier, und weil es
        /// fehlte, uebertrug <c>TryUpdateModelAsync</c> nichts in die Spalte: angelegt wurde mit
        /// <c>ClientAppTemplateId = 0</c>, und der Fremdschluessel hat das abgewiesen. Es gab damit gar
        /// keinen Weg, ein Buendel anzulegen.
        /// </remarks>
        [Range(1, int.MaxValue, ErrorMessage = "A template is required")]
        public int ClientAppTemplateId { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; }
    }
}
