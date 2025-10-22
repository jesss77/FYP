using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace FYP.Models
{
    public class Customer
    {
        [Key]
        public int CustomerID { get; set; }

        [Required, EmailAddress, StringLength(255)]
        public string Email { get; set; } = string.Empty;


        [Required, StringLength(100), MinLength(2)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100), MinLength(2)]
        public string LastName { get; set; } = string.Empty;

        [Phone, StringLength(20)]
        public string? PhoneNumber { get; set; }

        [RegularExpression("English|French", ErrorMessage = "Must be English or French")]
        public string? PreferredLanguage { get; set; } = "English";

        public bool IsActive { get; set; } = false;
        public byte[]? PictureBytes { get; set; }

        public ICollection<Reservation>? Reservations { get; set; } = new List<Reservation>();

        // Link to ApplicationUser
        public string? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

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
