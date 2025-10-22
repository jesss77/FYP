using System.ComponentModel.DataAnnotations;

namespace FYP.Models
{
    public class ActionType
    {
        [Key]
        public int ActionTypeID { get; set; }
        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
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
