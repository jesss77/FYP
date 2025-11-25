// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using FYP.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FYP.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(SignInManager<ApplicationUser> signInManager, ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            // Sign out from Identity and cookie schemes explicitly to ensure cookies cleared
            try
            {
                await _signInManager.SignOutAsync();
                await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during sign-out process");
            }

            _logger.LogInformation("User logged out.");

            // If a returnUrl was provided use it, otherwise redirect to site root
            var redirectUrl = returnUrl ?? Url.Content("~/");
            // Ensure we only redirect to local URLs
            if (Url.IsLocalUrl(redirectUrl))
            {
                return LocalRedirect(redirectUrl);
            }

            return RedirectToPage("/Index");
        }
    }
}
