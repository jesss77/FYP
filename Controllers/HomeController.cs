using FYP.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace FYP.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            // If user is authenticated, redirect to their appropriate dashboard
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("customer"))
                {
                    return RedirectToAction("Index", "Customer");
                }
                else if (User.IsInRole("employee") || User.IsInRole("manager"))
                {
                    return RedirectToAction("Index", "Employee");
                }
                // Don't auto-redirect admin - let them see the home page
                // Admins can manually navigate to /Admin/Index
            }

            // If not authenticated or is admin, show welcome page
            return View();
        }

        // Landing route that always shows the public home page (used by header/logo and back links)
        [HttpGet]
        public IActionResult Landing()
        {
            return View("Index");
        }


        [HttpGet] // Optional, but good practice for simple page views
        public IActionResult AboutUs()
        {
            // This method returns the view file associated with the action name.
            // It will look for /Views/Home/AboutUs.cshtml by default.
            return View();
        }

        [HttpGet]
        public IActionResult ContactUs()
        {
            return View();
        }

        [HttpPost] // Best practice for actions that change state/settings
        public IActionResult ChangeLanguage(string lang, string returnUrl)
        {
            if (string.IsNullOrEmpty(lang))
            {
                return LocalRedirect(returnUrl ?? "/"); // Redirect back if no language is provided
            }

            // Set the cookie
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(lang)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            // Redirect the user back to the page they were on
            return LocalRedirect(returnUrl ?? "/");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
