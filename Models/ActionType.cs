using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models
{
    public class ActionType
    {
        [Key]
        public int ActionTypeID { get; set; }

        [Required, StringLength(100)]
        [Column("Name")]
        public string ActionTypeName { get; set; }

        [StringLength(300)]
        public string? Description { get; set; }

        [Required, StringLength(450)]
        public string CreatedBy { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [StringLength(450)]
        public string? UpdatedBy { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; }
    }
}
