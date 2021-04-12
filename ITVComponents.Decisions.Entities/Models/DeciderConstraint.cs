using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITVComponents.Decisions.Entities.Models
{
    public class DeciderConstraint
    {
        [Key]
        public int DeciderConstraintId { get; set; }

        public int ConstraintId { get; set; }

        public int DeciderId { get; set; }

        [ForeignKey("ConstraintId")]
        public virtual ConstraintDefinition Constraint { get; set; }

        [ForeignKey("DeciderId")]
        public virtual Decider Decider { get; set; }
    }
}
