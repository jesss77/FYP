using System.ComponentModel.DataAnnotations;

namespace FYP.Models
{
    public class TablesJoin
    {
        [Key]
        public int TablesJoinID { get; set; }

        [Required]
        public int PrimaryTableID { get; set; }
        public Table PrimaryTable { get; set; }

        [Required]
        public int JoinedTableID { get; set; }
        public Table JoinedTable { get; set; }

        [Required]
        public int TotalCapacity { get; set; }

        [Required, StringLength(450)]
        public string CreatedBy { get; set; }
        [Required]
        public DateTime CreatedAt { get; set; }
        [StringLength(450)]
        public string UpdatedBy { get; set; }
        [Required]
        public DateTime UpdatedAt { get; set; }
    }
}
