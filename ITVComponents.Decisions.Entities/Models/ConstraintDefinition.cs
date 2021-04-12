using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ITVComponents.Decisions.Entities.Models
{
    public class ConstraintDefinition
    {
        [Key]
        public int ConstraintId { get; set; }

        [MaxLength(50)]
        public string ConstraintIdentifier { get; set; }

        [MaxLength(512)]
        public string ConstraintType { get; set; }

        public virtual ICollection<ConstraintParameterDefinition> Parameters { get; } = new HashSet<ConstraintParameterDefinition>();
    }
}
