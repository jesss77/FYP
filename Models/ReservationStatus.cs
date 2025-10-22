using System.ComponentModel.DataAnnotations;

namespace FYP.Models
{
    public class ReservationStatus
    {
        [Key]
        public int ReservationStatusID { get; set; }
        [Required, StringLength(100)]
        public string StatusName { get; set; }
        [StringLength(300)]
        public string Description { get; set; }
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
