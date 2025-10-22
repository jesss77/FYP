using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Models;
using FYP.Data;
using FYP.Services;
using System;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    [Authorize]
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;

        public EmployeeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        // GET: Employee/Create
        [AllowAnonymous]
        public async Task<IActionResult> Create()
        {
            var model = new Employee
            {
                RestaurantID = 1,
                Email = TempData["Email"]?.ToString(),
                ApplicationUserId = TempData["UserId"]?.ToString(),
                CreatedBy = TempData["UserId"]?.ToString(),
                UpdatedBy = TempData["UserId"]?.ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Restaurant = await _context.Restaurants.FindAsync(1)
            };

            // Keep TempData for the POST method
            TempData.Keep("UserId");
            TempData.Keep("Email");

            return View(model);
        }

        // POST: Employee/Create
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee model)
        {
            ModelState.Remove("ApplicationUser");

            if (!ModelState.IsValid)
                return View(model);

            // Set audit and IDs
            model.ApplicationUserId ??= TempData["UserId"]?.ToString();
            model.Email ??= TempData["Email"]?.ToString();
            model.CreatedBy ??= model.ApplicationUserId;
            model.UpdatedBy ??= model.ApplicationUserId;
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;
            model.RestaurantID = 1; // default restaurant
            model.Restaurant = await _context.Restaurants.FindAsync(1);

            // Set pending approval
            model.IsActive = false;

            _context.Employees.Add(model);
            await _context.SaveChangesAsync();

            // Send email to all admins
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            foreach (var admin in admins)
            {
                var approveUrl = Url.Action(
                    "ApproveEmployee",
                    "Admin",
                    new { id = model.EmployeeID },
                    protocol: Request.Scheme);

                string body = $@"
                    <p>New employee registration:</p>
                    <p>Name: {model.FirstName} {model.LastName}</p>
                    <p>Email: {model.Email}</p>
                    <p><a href='{approveUrl}' style='color:red;'>Approve Employee</a></p>";
                await _emailService.SendEmailAsync(admin.Email, "New Employee Approval Needed", body);
            }

            // Send pending confirmation email to employee
            string employeeBody = $@"
                <p>Dear {model.FirstName} {model.LastName},</p>
                <p>Your registration is pending admin approval. You will receive an email once approved.</p>";

            await _emailService.SendEmailAsync(model.Email, "Registration Pending Approval", employeeBody);

            // Redirect to pending confirmation page
            return RedirectToPage("/Account/PendingConfirmation", new { area = "Identity" });
        }

        public async Task<IActionResult> Dashboard()
        {
            // Check if user's email is confirmed
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("/Account/ConfirmEmail", new { area = "Identity", email = user?.Email });
            }

            // Get the employee record for the current user
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id);

            if (employee == null)
            {
                return RedirectToAction("Create");
            }

            return View(employee);
        }

        public async Task<IActionResult> Index()
        {
            // Check if user's email is confirmed
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("/Account/ConfirmEmail", new { area = "Identity", email = user?.Email });
            }

            // Get the employee record for the current user
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id);

            if (employee == null)
            {
                return RedirectToAction("Create");
            }

            return View(employee);
        }
    }
}
