using FYP.Models;
using FYP.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FYP.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;
            
        public RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required, EmailAddress]
            public string Email { get; set; }

            [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            public string Role { get; set; } // "Customer" or "Employee"
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
                return Page();

            // Create ApplicationUser
            var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email };
            var identityResult = await _userManager.CreateAsync(user, Input.Password);

            if (!identityResult.Succeeded)
            {
                foreach (var error in identityResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }

            // Add user to Customer role
            await _userManager.AddToRoleAsync(user, Input.Role);

            // Generate email confirmation token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            
            // Send confirmation email
            try
            {
                await _emailService.SendConfirmationEmailAsync(user, token);
            }
            catch (Exception)
            {
                // Log the error but don't fail registration
                // You might want to add proper logging here
                ModelState.AddModelError(string.Empty, "Registration successful, but there was an issue sending the confirmation email. Please try logging in to resend the confirmation email.");
                return Page();
            }

            // Create Customer record
            TempData["UserId"] = user.Id;
            TempData["Email"] = user.Email;
            return RedirectToAction("Create", "Customer");
        }
    }
}
