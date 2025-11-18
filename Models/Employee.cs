using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace FYP.Models
{
    public class Employee
    {
        [Key]
        public int EmployeeID { get; set; }

        [Required, EmailAddress, StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100), MinLength(2)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100), MinLength(2)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public int RestaurantID { get; set; } = 0; //default
        public Restaurant? Restaurant { get; set; }

        [Phone, StringLength(20)]
        public string? PhoneNumber { get; set; }

        public bool IsActive { get; set; } = false;

        // Link to ApplicationUser
        public string? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        [StringLength(450)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        [StringLength(450)]
        public string? UpdatedBy { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
