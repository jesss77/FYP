using FYP.Data;
using FYP.Models;
using FYP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public AdminController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // GET: /Admin/PendingEmployees
        public async Task<IActionResult> PendingEmployees()
        {
            var pending = await _context.Employees
                .Include(e => e.ApplicationUser)
                .Where(e => !e.IsActive)
                .ToListAsync();

            return View(pending);
        }

        // GET: /Admin/ApproveEmployee?id=6
        [HttpGet]
        public async Task<IActionResult> ApproveEmployee(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.ApplicationUser)
                .FirstOrDefaultAsync(e => e.EmployeeID == id);

            if (employee == null) return NotFound();

            if (employee.IsActive)
            {
                TempData["Info"] = $"{employee.FirstName} {employee.LastName} is already approved.";
                return RedirectToAction(nameof(PendingEmployees));
            }

            employee.IsActive = true;
            employee.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Send notification email to employee
            if (employee.ApplicationUser != null)
            {
                string subject = "Your account has been approved!";
                string message = $"Hello {employee.FirstName},<br/>Your account has been approved by the admin. You can now log in.";
                await _emailService.SendEmailAsync(employee.Email, subject, message);
            }

            TempData["Success"] = $"{employee.FirstName} {employee.LastName} approved successfully!";
            return RedirectToAction(nameof(PendingEmployees));
        }

        public IActionResult Dashboard()
        {
            return View();
        }
        public IActionResult Index()
        {
            return View();
        }
    }
}