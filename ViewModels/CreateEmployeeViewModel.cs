using System.ComponentModel.DataAnnotations;

namespace FYP.ViewModels
{
    public class CreateEmployeeViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
        public string Password { get; set; }

        [Required, StringLength(100), MinLength(2)]
        public string FirstName { get; set; }

        [Required, StringLength(100), MinLength(2)]
        public string LastName { get; set; }

        [Phone, StringLength(20)]
        public string? PhoneNumber { get; set; }
    }
}
