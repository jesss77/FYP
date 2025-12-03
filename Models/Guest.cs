using System.ComponentModel.DataAnnotations;

namespace FYP.Models
{
    public class Guest
    {
        [Key]
        public int GuestID { get; set; }

        [Required, StringLength(255)]
        [EmailAddress]
        public string Email { get; set; }

        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        [Phone, StringLength(20)]
        public string? PhoneNumber { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation property
        public ICollection<Reservation> Reservations { get; set; }


        // Audit fields
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
