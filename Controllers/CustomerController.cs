using FYP.Data;
using FYP.Models;
using FYP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;

        public CustomerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Create()
        {
            var model = new Customer
            {
                Email = TempData["Email"]?.ToString(),
                ApplicationUserId = TempData["UserId"]?.ToString(),
                CreatedBy = TempData["UserId"]?.ToString(),
                UpdatedBy = TempData["UserId"]?.ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var defaultImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pics", "pfp.jpg");
            if (System.IO.File.Exists(defaultImagePath))
            {
                model.PictureBytes = await System.IO.File.ReadAllBytesAsync(defaultImagePath);
            }
            else
            {
                model.PictureBytes = Array.Empty<byte>();
            }

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer model, IFormFile? picture)
        {
            ModelState.Remove("ApplicationUser");
            ModelState.Remove("PictureBytes");

            if (!ModelState.IsValid)
                return View(model);

            model.ApplicationUserId ??= TempData["UserId"]?.ToString();
            model.Email ??= TempData["Email"]?.ToString();

            model.CreatedBy ??= model.ApplicationUserId;
            model.UpdatedBy ??= model.ApplicationUserId;
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;

            if (picture != null && picture.Length > 0)
            {
                using var ms = new MemoryStream();
                await picture.CopyToAsync(ms);
                model.PictureBytes = ms.ToArray();
            }
            else
            {
                var defaultImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pics", "pfp.jpg");
                if (System.IO.File.Exists(defaultImagePath))
                    model.PictureBytes = await System.IO.File.ReadAllBytesAsync(defaultImagePath);
                else
                    model.PictureBytes = Array.Empty<byte>();
            }

            _context.Customers.Add(model);
            await _context.SaveChangesAsync();

            // Email confirmation was already sent during registration
            // No need to send another confirmation email here

            // Redirect to login page with message about email confirmation
            TempData["Message"] = "Registration successful! Please check your email and confirm your account before logging in.";
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        public async Task<IActionResult> Dashboard()
        {
            // Check if user's email is confirmed
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("/Account/ConfirmEmail", new { area = "Identity", email = user?.Email });
            }

            // Get the customer record for the current user
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id);

            if (customer == null)
            {
                return RedirectToAction("Create");
            }

            return View(customer);
        }

        public async Task<IActionResult> Index()
        {
            // Check if user's email is confirmed
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("/Account/ConfirmEmail", new { area = "Identity", email = user?.Email });
            }

            // Get the customer record for the current user
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id);

            if (customer == null)
            {
                return RedirectToAction("Create");
            }

            return View(customer);
        }
    }
}