// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using FYP.Data;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace FYP.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public ResetPasswordModel(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            public string Code { get; set; }
        }

        public IActionResult OnGet(string code = null, string email = null)
        {
            if (code == null)
            {
                return BadRequest("A code must be supplied for password reset.");
            }
            else
            {
                Input = new InputModel
                {
                    Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)),
                    Email = email
                };
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                ModelState.AddModelError(string.Empty, "Invalid password reset attempt.");
                return Page();
            }

            // Check if email is confirmed
            var isEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
            if (!isEmailConfirmed)
            {
                ModelState.AddModelError(string.Empty, "Your email address has not been confirmed yet. Please confirm your email before resetting your password.");
                return Page();
            }

            // Verify the reset token is valid (this also checks expiration)
            var tokenProvider = _userManager.Options.Tokens.PasswordResetTokenProvider;
            var isValidToken = await _userManager.VerifyUserTokenAsync(
                user, 
                tokenProvider, 
                "ResetPassword", 
                Input.Code);

            if (!isValidToken)
            {
                ModelState.AddModelError(string.Empty, "The password reset link has expired or is invalid. Please request a new password reset link.");
                return Page();
            }

            // Check if the new password is the same as the current password
            var isSamePassword = await _userManager.CheckPasswordAsync(user, Input.Password);
            if (isSamePassword)
            {
                ModelState.AddModelError(string.Empty, "Your new password cannot be the same as your current password. Please choose a different password.");
                return Page();
            }

            var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
            if (result.Succeeded)
            {
                try
                {
                    // If the user is linked to a Customer record, activate the customer account
                    if (user.CustomerID.HasValue)
                    {
                        var customer = await _db.Customers.FindAsync(user.CustomerID.Value);
                        if (customer != null)
                        {
                            customer.IsActive = true;
                            customer.UpdatedAt = DateTime.UtcNow;
                            customer.UpdatedBy = user.Id ?? "system";
                            await _db.SaveChangesAsync();
                        }
                    }
                }
                catch
                {
                    // Don't block password reset completion if database update fails; log could be added
                }

                TempData["SuccessMessage"] = "Your password has been successfully reset. Please log in with your new password.";
                return RedirectToPage("./Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
    }
}
