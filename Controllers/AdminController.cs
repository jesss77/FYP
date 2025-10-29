using FYP.Data;
using FYP.Models;
using FYP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext context, IEmailService emailService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _emailService = emailService;
            _userManager = userManager;
        }

        public class CreateEmployeeViewModel
        {
            [Required, EmailAddress]
            public string Email { get; set; }

            [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
            public string Password { get; set; }

            [Required, StringLength(100), MinLength(2)]
            public string FirstName { get; set; }

            [Required, StringLength(100), MinLength(2)]
            public string LastName { get; set; }

            [Phone, StringLength(20)]
            public string? PhoneNumber { get; set; }
        }

        public class EditEmployeeViewModel
        {
            public int EmployeeID { get; set; }

            [Required, StringLength(100), MinLength(2)]
            public string FirstName { get; set; }

            [Required, StringLength(100), MinLength(2)]
            public string LastName { get; set; }

            [Phone, StringLength(20)]
            public string? PhoneNumber { get; set; }
        }

        // ================== SETTINGS ==================

        // GET: /Admin/Settings
        public async Task<IActionResult> Settings()
        {
            var settings = await _context.Settings
                .OrderBy(s => s.Key)
                .ToListAsync();
            return View(settings);
        }

        // GET: /Admin/CreateSetting
        public IActionResult CreateSetting()
        {
            return View();
        }

        // POST: /Admin/CreateSetting
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSetting(Settings model)
        {
            // Remove audit fields from validation since they're set programmatically
            ModelState.Remove("CreatedBy");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedBy");
            ModelState.Remove("UpdatedAt");
            
            if (ModelState.IsValid)
            {
                // Prevent duplicate key
                if (await _context.Settings.AnyAsync(s => s.Key == model.Key))
                {
                    ModelState.AddModelError("Key", "A setting with this key already exists.");
                    return View(model);
                }

                model.CreatedBy = User.Identity?.Name ?? "system";
                model.UpdatedBy = User.Identity?.Name ?? "system";
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;

                _context.Settings.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Setting created successfully!";
                return RedirectToAction(nameof(Settings));
            }

            return View(model);
        }

        // GET: /Admin/EditSetting/5
        public async Task<IActionResult> EditSetting(int id)
        {
            var setting = await _context.Settings.FindAsync(id);
            if (setting == null) return NotFound();

            return View(setting);
        }

        // POST: /Admin/EditSetting/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSetting(int id, Settings model)
        {
            if (id != model.SettingsID) return BadRequest();

            // Remove audit fields from validation since they're set programmatically
            ModelState.Remove("CreatedBy");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedBy");
            ModelState.Remove("UpdatedAt");
            
            if (ModelState.IsValid)
            {
                var setting = await _context.Settings.FindAsync(id);
                if (setting == null) return NotFound();

                // Prevent duplicate key
                if (await _context.Settings.AnyAsync(s => s.Key == model.Key && s.SettingsID != id))
                {
                    ModelState.AddModelError("Key", "A setting with this key already exists.");
                    return View(model);
                }

                setting.Key = model.Key;
                setting.Value = model.Value;
                setting.UpdatedBy = User.Identity?.Name ?? "system";
                setting.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Setting updated successfully!";
                return RedirectToAction(nameof(Settings));
            }

            return View(model);
        }

        // GET: /Admin/DeleteSetting/5
        public async Task<IActionResult> DeleteSetting(int id)
        {
            var setting = await _context.Settings.FindAsync(id);
            if (setting == null) return NotFound();

            _context.Settings.Remove(setting);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Setting deleted successfully!";
            return RedirectToAction(nameof(Settings));
        }

        // ================== EMPLOYEES ==================

        // GET: /Admin/Employees
        public async Task<IActionResult> Employees()
        {
            var employees = await _context.Employees
                .Include(e => e.ApplicationUser)
                .ToListAsync();

            return View(employees);
        }

        // GET: /Admin/CreateEmployee
        public IActionResult CreateEmployee()
        {
            return View(new CreateEmployeeViewModel());
        }

        // POST: /Admin/CreateEmployee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEmployee(CreateEmployeeViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Create ApplicationUser
            var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
            var identityResult = await _userManager.CreateAsync(user, model.Password);

            if (!identityResult.Succeeded)
            {
                foreach (var error in identityResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            // Add user to Employee role
            await _userManager.AddToRoleAsync(user, "employee");

            // Auto-confirm email for employees created by admin
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _userManager.ConfirmEmailAsync(user, token);

            // Get the first restaurant (or create one if none exists)
            var restaurant = await _context.Restaurants.FirstOrDefaultAsync();
            if (restaurant == null)
            {
                // If no restaurant exists, get or create settings first
                var settings = await _context.Settings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    // Create default settings if none exist
                    settings = new Settings
                    {
                        Key = "Name",
                        Value = "Fine O Dine",
                        CreatedBy = User.Identity?.Name ?? "admin",
                        UpdatedBy = User.Identity?.Name ?? "admin",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Settings.Add(settings);
                    await _context.SaveChangesAsync();
                }

                // Create default restaurant
                restaurant = new Restaurant
                {
                    SettingsID = settings.SettingsID,
                    CreatedBy = User.Identity?.Name ?? "admin",
                    UpdatedBy = User.Identity?.Name ?? "admin",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Restaurants.Add(restaurant);
                await _context.SaveChangesAsync();
            }

            // Create Employee record
            var employee = new Employee
            {
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                PhoneNumber = model.PhoneNumber,
                ApplicationUserId = user.Id,
                RestaurantID = restaurant.RestaurantID,
                Restaurant = restaurant,
                IsActive = true, // Auto-activate
                CreatedBy = User.Identity?.Name ?? "admin",
                UpdatedBy = User.Identity?.Name ?? "admin",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Employee {model.FirstName} {model.LastName} created successfully!";
            return RedirectToAction(nameof(Employees));
        }

        // GET: /Admin/EditEmployee/5
        public async Task<IActionResult> EditEmployee(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            var model = new EditEmployeeViewModel
            {
                EmployeeID = employee.EmployeeID,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                PhoneNumber = employee.PhoneNumber
            };

            return View(model);
        }

        // POST: /Admin/EditEmployee/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEmployee(int id, EditEmployeeViewModel model)
        {
            if (id != model.EmployeeID) return BadRequest();

            if (!ModelState.IsValid)
                return View(model);

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            employee.FirstName = model.FirstName;
            employee.LastName = model.LastName;
            employee.PhoneNumber = model.PhoneNumber;
            employee.UpdatedBy = User.Identity?.Name ?? "admin";
            employee.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Employee {model.FirstName} {model.LastName} updated successfully!";
            return RedirectToAction(nameof(Employees));
        }

        // GET: /Admin/DeleteEmployee/5
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.ApplicationUser)
                .FirstOrDefaultAsync(e => e.EmployeeID == id);

            if (employee == null) return NotFound();

            // Store user info before deletion
            var userId = employee.ApplicationUserId;
            var employeeName = $"{employee.FirstName} {employee.LastName}";

            // First, delete the Employee record
            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            // Then delete the ApplicationUser from AspNetUsers table
            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    // Clear the EmployeeID reference first
                    user.EmployeeID = null;
                    await _userManager.UpdateAsync(user);

                    // Now delete the user
                    var deleteResult = await _userManager.DeleteAsync(user);
                    if (!deleteResult.Succeeded)
                    {
                        // If UserManager delete fails, try direct context deletion
                        _context.Users.Remove(user);
                        await _context.SaveChangesAsync();
                    }
                }
            }

            TempData["Success"] = $"Employee {employeeName} deleted successfully!";
            return RedirectToAction(nameof(Employees));
        }

        // GET: /Admin/ToggleActivateEmployee/5
        public async Task<IActionResult> ToggleActivateEmployee(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.ApplicationUser)
                .FirstOrDefaultAsync(e => e.EmployeeID == id);

            if (employee == null) return NotFound();

            employee.IsActive = !employee.IsActive;
            employee.UpdatedBy = User.Identity?.Name ?? "admin";
            employee.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            string action = employee.IsActive ? "activated" : "deactivated";
            TempData["Success"] = $"Employee {employee.FirstName} {employee.LastName} {action} successfully!";
            return RedirectToAction(nameof(Employees));
        }

        // ================== TABLES ==================

        // GET: /Admin/Tables
        public async Task<IActionResult> Tables()
        {
            var tables = await _context.Tables
                .Include(t => t.Restaurant)
                .OrderBy(t => t.TableNumber)
                .ToListAsync();

            return View(tables);
        }

        // GET: /Admin/CreateTable
        public IActionResult CreateTable()
        {
            return View();
        }

        // POST: /Admin/CreateTable
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTable(Table model)
        {
            try
            {
                // Remove audit fields and restaurant from validation since they're set programmatically
                ModelState.Remove("CreatedBy");
                ModelState.Remove("CreatedAt");
                ModelState.Remove("UpdatedBy");
                ModelState.Remove("UpdatedAt");
                ModelState.Remove("Restaurant");
                ModelState.Remove("RestaurantID");
                
                // Debug: Log model state
                if (!ModelState.IsValid)
                {
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        Console.WriteLine($"Validation Error: {error.ErrorMessage}");
                    }
                    return View(model);
                }
                
                if (ModelState.IsValid)
                {
                // Get the first restaurant (or create one if none exists)
                var restaurant = await _context.Restaurants.FirstOrDefaultAsync();
                if (restaurant == null)
                {
                    // If no restaurant exists, get or create settings first
                    var settings = await _context.Settings.FirstOrDefaultAsync();
                    if (settings == null)
                    {
                        // Create default settings if none exist
                        settings = new Settings
                        {
                            Key = "Name",
                            Value = "Fine O Dine",
                            CreatedBy = User.Identity?.Name ?? "admin",
                            UpdatedBy = User.Identity?.Name ?? "admin",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.Settings.Add(settings);
                        await _context.SaveChangesAsync();
                    }

                    // Create default restaurant
                    restaurant = new Restaurant
                    {
                        SettingsID = settings.SettingsID,
                        CreatedBy = User.Identity?.Name ?? "admin",
                        UpdatedBy = User.Identity?.Name ?? "admin",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Restaurants.Add(restaurant);
                    await _context.SaveChangesAsync();
                }

                // Check for duplicate table number
                if (await _context.Tables.AnyAsync(t => t.TableNumber == model.TableNumber && t.RestaurantID == restaurant.RestaurantID))
                {
                    ModelState.AddModelError("TableNumber", "A table with this number already exists.");
                    return View(model);
                }

                model.RestaurantID = restaurant.RestaurantID;
                model.Restaurant = restaurant;
                model.CreatedBy = User.Identity?.Name ?? "admin";
                model.UpdatedBy = User.Identity?.Name ?? "admin";
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;

                _context.Tables.Add(model);
                await _context.SaveChangesAsync();

                    TempData["Success"] = $"Table {model.TableNumber} created successfully!";
                    return RedirectToAction(nameof(Tables));
                }

                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating table: {ex.Message}");
                TempData["Error"] = $"Error creating table: {ex.Message}";
                return View(model);
            }
        }

        // GET: /Admin/EditTable/5
        public async Task<IActionResult> EditTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            return View(table);
        }

        // POST: /Admin/EditTable/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTable(int id, Table model)
        {
            if (id != model.TableID) return BadRequest();

            // Remove audit fields and restaurant from validation since they're set programmatically
            ModelState.Remove("CreatedBy");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedBy");
            ModelState.Remove("UpdatedAt");
            ModelState.Remove("Restaurant");
            ModelState.Remove("RestaurantID");
            
            if (ModelState.IsValid)
            {
                var table = await _context.Tables.FindAsync(id);
                if (table == null) return NotFound();

                // Check for duplicate table number
                if (await _context.Tables.AnyAsync(t => t.TableNumber == model.TableNumber && t.RestaurantID == table.RestaurantID && t.TableID != id))
                {
                    ModelState.AddModelError("TableNumber", "A table with this number already exists.");
                    return View(model);
                }

                table.TableNumber = model.TableNumber;
                table.Capacity = model.Capacity;
                table.IsJoinable = model.IsJoinable;
                table.IsAvailable = model.IsAvailable;
                table.UpdatedBy = User.Identity?.Name ?? "admin";
                table.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Table {model.TableNumber} updated successfully!";
                return RedirectToAction(nameof(Tables));
            }

            return View(model);
        }

        // GET: /Admin/DeleteTable/5
        public async Task<IActionResult> DeleteTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            _context.Tables.Remove(table);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Table {table.TableNumber} deleted successfully!";
            return RedirectToAction(nameof(Tables));
        }

        // GET: /Admin/ToggleTableAvailability/5
        public async Task<IActionResult> ToggleTableAvailability(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            table.IsAvailable = !table.IsAvailable;
            table.UpdatedBy = User.Identity?.Name ?? "admin";
            table.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            string action = table.IsAvailable ? "made available" : "made unavailable";
            TempData["Success"] = $"Table {table.TableNumber} {action} successfully!";
            return RedirectToAction(nameof(Tables));
        }

        // ================== DASHBOARD ==================

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
