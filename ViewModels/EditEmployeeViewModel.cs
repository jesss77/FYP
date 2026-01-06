using System.ComponentModel.DataAnnotations;

namespace FYP.ViewModels
{
    public class EditEmployeeViewModel
    {
        public int EmployeeID { get; set; }

        [Required, StringLength(100), MinLength(2)]
        public string FirstName { get; set; }

        [Required, StringLength(100), MinLength(2)]
        public string LastName { get; set; }

        [Phone, StringLength(20)]
        public string? PhoneNumber { get; set; }
    }
}
