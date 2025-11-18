using FYP.Models;
using FYP.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

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
        public InputModel Input { get; set; } = new InputModel();

        public string ReturnUrl { get; set; }

        public class InputModel : IValidatableObject
        {
            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email address")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Password is required")]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [Required(ErrorMessage = "Please confirm your password")]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            public string Role { get; set; } = "Customer";

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                // Additional email validation
                if (!string.IsNullOrWhiteSpace(Email))
                {
                    // Check for whitespace
                    if (Email.Contains(" "))
                    {
                        yield return new ValidationResult("Email address cannot contain spaces", new[] { nameof(Email) });
                    }

                    // Check for valid email format
                    var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                    if (!emailRegex.IsMatch(Email))
                    {
                        yield return new ValidationResult("Please enter a valid email address", new[] { nameof(Email) });
                    }
                }

                // Additional password validation
                if (!string.IsNullOrWhiteSpace(Password))
                {
                    // Check for at least one uppercase letter
                    if (!Password.Any(char.IsUpper))
                    {
                        yield return new ValidationResult("Password must contain at least one uppercase letter", new[] { nameof(Password) });
                    }

                    // Check for at least one lowercase letter
                    if (!Password.Any(char.IsLower))
                    {
                        yield return new ValidationResult("Password must contain at least one lowercase letter", new[] { nameof(Password) });
                    }

                    // Check for at least one digit
                    if (!Password.Any(char.IsDigit))
                    {
                        yield return new ValidationResult("Password must contain at least one number", new[] { nameof(Password) });
                    }

                    // Check for at least one special character
                    if (!Password.Any(ch => !char.IsLetterOrDigit(ch)))
                    {
                        yield return new ValidationResult("Password must contain at least one special character (!@#$%^&*)", new[] { nameof(Password) });
                    }
                }
            }
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            Input = new InputModel { Role = "Customer" };
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            // Trim whitespace from inputs
            if (Input?.Email != null)
                Input.Email = Input.Email.Trim();

            // Validate model state
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Additional server-side checks
            if (string.IsNullOrWhiteSpace(Input.Email))
            {
                ModelState.AddModelError("Input.Email", "Email is required");
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Input.Password))
            {
                ModelState.AddModelError("Input.Password", "Password is required");
                return Page();
            }

            if (string.IsNullOrWhiteSpace(Input.ConfirmPassword))
            {
                ModelState.AddModelError("Input.ConfirmPassword", "Please confirm your password");
                return Page();
            }

            // Check if passwords match
            if (Input.Password != Input.ConfirmPassword)
            {
                ModelState.AddModelError("Input.ConfirmPassword", "Passwords do not match");
                return Page();
            }

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(Input.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Input.Email", "An account with this email already exists");
                return Page();
            }

            // Create ApplicationUser
            var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email };
            var identityResult = await _userManager.CreateAsync(user, Input.Password);

            if (!identityResult.Succeeded)
            {
                foreach (var error in identityResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            // Add user to role
            await _userManager.AddToRoleAsync(user, Input.Role);

            // Generate email confirmation token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            
            // Send confirmation email
            try
            {
                await _emailService.SendConfirmationEmailAsync(user, token);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Registration successful, but there was an issue sending the confirmation email: {ex.Message}");
                return Page();
            }

            // Redirect based on role to create Customer or Employee record
            TempData["UserId"] = user.Id;
            TempData["Email"] = user.Email;
            
            if (Input.Role == "Customer")
            {
                return RedirectToAction("Create", "Customer");
            }
            else if (Input.Role == "Employee")
            {
                return RedirectToAction("Create", "Employee");
            }

            return Page();
        }
    }
}
