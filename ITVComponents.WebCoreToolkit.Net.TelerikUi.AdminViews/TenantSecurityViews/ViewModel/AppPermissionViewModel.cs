using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel
{
    /// <summary>
    /// Ein Rechtebuendel unterhalb eines Anwendungs-Templates.
    /// </summary>
    /// <remarks>
    /// <c>Assigned</c> und <c>UniQUID</c> sind mit PRE242 weggefallen: sie trugen das Ankreuzfeld einer
    /// Zuordnung, die es seit PRE240 nicht mehr gibt - ein Buendel gehoert genau einem Template. Das
    /// Kindgitter legt jetzt an, benennt um und loescht, statt zu- und abzuwaehlen.
    /// </remarks>
    public class AppPermissionViewModel
    {
        [Required, MaxLength(150)]
        public string PermissionSetName { get; set; }

        public int ParentId { get; set; }

        public int AppPermissionSetId { get; set; }
    }
}
