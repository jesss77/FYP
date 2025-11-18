using FYP.Data;
using FYP.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace FYP.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly ApplicationDbContext _context;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
                return Page();

            // Step 1: Find the user by email
            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            // Step 2: Check if user is an employee
            bool isEmployee = await _userManager.IsInRoleAsync(user, "employee");

            // Step 3: Check email confirmation status BEFORE attempting sign-in (skip for employees)
            if (!isEmployee && !await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("./ConfirmEmail", new { email = Input.Email });
            }

            // Step 4: Check for Employee specific approval status
            if (isEmployee)
            {
                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id);
                if (employee == null || !employee.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "Your account is inactive. Please contact an administrator.");
                    return Page();
                }
            }

            // Step 5: Attempt to sign the user in (now that we know email is confirmed)
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true); // Enable lockout for security

            if (result.Succeeded)
            {
                _logger.LogInformation("User {UserId} logged in successfully.", user.Id);

                // Redirect based on role (Runs ONLY on successful login)
                if (await _userManager.IsInRoleAsync(user, "customer"))
                    return RedirectToAction("Index", "Customer", new { area = "" });

                else if (await _userManager.IsInRoleAsync(user, "employee"))
                    return RedirectToAction("Index", "Employee", new { area = "" });

                else if (await _userManager.IsInRoleAsync(user, "manager"))
                    return RedirectToAction("Index", "Manager", new { area = "" });

                else if (await _userManager.IsInRoleAsync(user, "admin"))
                    return RedirectToAction("Index", "Admin", new { area = "" });

                // fallback
                return LocalRedirect(returnUrl);
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account {UserId} locked out.", user.Id);
                return RedirectToPage("./Lockout");
            }

            if (result.IsNotAllowed)
            {
                _logger.LogWarning("User {UserId} not allowed to login.", user.Id);
                ModelState.AddModelError(string.Empty, "Your account is not allowed to login. Please contact support.");
                return Page();
            }

            // If we fall through, it's an invalid password attempt
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }
    }
}