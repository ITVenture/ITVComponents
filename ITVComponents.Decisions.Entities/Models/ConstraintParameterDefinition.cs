using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITVComponents.Decisions.Entities.Models
{
    public class ConstraintParameterDefinition
    {
        [Key]
        public int ConstraintParameterId { get; set; }

        public int ConstraintId { get; set; }

        public int ParameterOrder { get; set; }

        [MaxLength(1024)]
        public string ParameterValue { get; set; }

        [MaxLength(512)]
        public string ParameterType { get; set; }

        [ForeignKey("ConstraintId")]
        public virtual ConstraintDefinition Constraint { get; set; }
    }
}
