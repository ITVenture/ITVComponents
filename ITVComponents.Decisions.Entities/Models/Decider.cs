using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ITVComponents.Decisions.Entities.Models
{
    public class Decider
    {
        [Key]
        public int DeciderId { get; set; }

        [MaxLength(50)]
        public string DisplayName { get; set; }

        public bool ContextDriven { get; set; }

        public virtual ICollection<DeciderConstraint> Constraints { get; } = new HashSet<DeciderConstraint>();

        public virtual ICollection<TableDecider> CheckingTables { get; } = new HashSet<TableDecider>();
    }
}
