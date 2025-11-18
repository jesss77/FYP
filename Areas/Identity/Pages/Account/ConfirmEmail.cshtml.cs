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

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string code, string email)
        {
            // If email parameter is provided, show "check your email" page
            if (!string.IsNullOrEmpty(email) && string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(code))
            {
                ViewData["Email"] = email;
                return Page();
            }

            // Handle email confirmation token
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
            {
                StatusMessage = "Error: Invalid confirmation link.";
                return Page();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                StatusMessage = "Error: User not found.";
                return Page();
            }

            // Check if already confirmed
            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                StatusMessage = "Your email is already confirmed. You can login now.";
                return Page();
            }

            // Decode the token from the URL
            var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, token);
            
            if (result.Succeeded)
            {
                // Activate customer account
                var customer = await _db.Customers.FirstOrDefaultAsync(c => c.ApplicationUserId == userId);
                if (customer != null)
                {
                    customer.IsActive = true;
                    customer.UpdatedAt = DateTime.UtcNow;
                    customer.UpdatedBy = userId;
                    await _db.SaveChangesAsync();
                }

                StatusMessage = "Thank you for confirming your email. Your account is now active and you can login.";
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                StatusMessage = $"Error confirming your email: {errors}";
            }

            return Page();
        }
    }
}
