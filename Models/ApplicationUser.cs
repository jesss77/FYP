using Microsoft.AspNetCore.Identity;

namespace FYP.Models
{
    public class ApplicationUser : IdentityUser
    {
        public int? CustomerID { get; set; }
        public Customer Customer { get; set; }

        public int? EmployeeID { get; set; }
        public Employee Employee { get; set; }
    }

}
