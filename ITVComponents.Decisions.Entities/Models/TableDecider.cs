using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITVComponents.Decisions.Entities.Models
{
    public class TableDecider
    {
        [Key]
        public int TableDeciderId { get; set; }

        [MaxLength(400)]
        public string TableName { get; set; }

        public int DeciderId { get; set; }

        [ForeignKey(nameof(DeciderId))]
        public virtual Decider Decider { get; set; }
    }
}
