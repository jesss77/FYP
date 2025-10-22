using FYP.Data;
using FYP.Models;
using FYP.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace FYP.Areas.Identity.Pages.Account
{
    public class ResendEmailConfirmationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly ApplicationDbContext _db;

        public ResendEmailConfirmationModel(
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            ApplicationDbContext db)
        {
            _userManager = userManager;
            _emailService = emailService;
            _db = db;
        }

        [BindProperty]
        public string Email { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                ModelState.AddModelError(string.Empty, "Please enter your email.");
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email not found.");
                return Page();
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = System.Web.HttpUtility.UrlEncode(token);

            if (user != null)
            {
                token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                if (token != null)
                {
                    await _emailService.SendConfirmationEmailAsync(user, token);
                }
            }

            TempData["Success"] = "A new confirmation email has been sent. Please check your inbox.";
            return RedirectToPage("/Account/PendingConfirmation", new { area = "Identity" });
        }
    }
}
