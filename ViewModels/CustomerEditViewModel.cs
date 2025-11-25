using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FYP.ViewModels
{
    public class CustomerEditViewModel
    {
        [Required]
        public int CustomerID { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [StringLength(100)]
        public string? FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string? LastName { get; set; }

        [Phone]
        public string? PhoneNumber { get; set; }

        [StringLength(10)]
        public string? PreferredLanguage { get; set; }

        // For display (existing image bytes)
        public byte[]? PictureBytes { get; set; }

        // For uploads
        public IFormFile? Picture { get; set; }
    }
}