using FYP.Data;
using FYP.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Threading.Tasks;

namespace FYP.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public ConfirmEmailModel(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        public async Task<IActionResult> OnGetAsync(string userId, string code, string email)
        {
            // If email parameter is provided, show confirmation page
            if (!string.IsNullOrEmpty(email))
            {
                ViewData["Email"] = email;
                return Page();
            }

            // Handle email confirmation token
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
                return RedirectToAction("Index", "Customer");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            // Decode the token from the URL
            var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                var customer = await _db.Customers.FirstOrDefaultAsync(c => c.ApplicationUserId == userId);
                if (customer != null)
                {
                    customer.IsActive = true;
                    await _db.SaveChangesAsync();
                }

                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            return Page();
        }
    }
}
