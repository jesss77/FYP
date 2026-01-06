using FYP.Data;
using FYP.Models;
using FYP.Services;
using FYP.ViewModels;
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

        public AdminController(
            ApplicationDbContext context,
            IEmailService emailService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _emailService = emailService;
            _userManager = userManager;
        }


        // ================== SETTINGS ==================
        public async Task<IActionResult> Settings(string search)
        {
            var query = _context.Settings.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s => EF.Functions.Like(s.Key, $"%{search.Trim()}%"));
                ViewBag.Search = search.Trim();
            }

            return View(await query.OrderBy(s => s.Key).ToListAsync());
        }

        public async Task<IActionResult> EditSetting(int id)
        {
            var setting = await _context.Settings.FindAsync(id);
            if (setting == null) return NotFound();

            ViewBag.IsKeyEditable = false;
            return View(setting);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSetting(int id, Settings model)
        {
            if (id != model.SettingsID) return BadRequest();

            ModelState.Remove("Key");
            ModelState.Remove("CreatedBy");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedBy");
            ModelState.Remove("UpdatedAt");

            if (!ModelState.IsValid) return View(model);

            var setting = await _context.Settings.FindAsync(id);
            if (setting == null) return NotFound();

            setting.Value = model.Value;
            setting.UpdatedBy = User.Identity?.Name ?? "admin";
            setting.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Setting updated successfully!";
            return RedirectToAction(nameof(Settings));
        }

        // Redirect table management to centralized TablesController
        public IActionResult Tables()
        {
            return RedirectToAction("Tables", "Tables");
        }

        public IActionResult CreateTable()
        {
            return RedirectToAction("CreateTable", "Tables");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateTable(Table model)
        {
            return RedirectToAction("CreateTable", "Tables");
        }

        public IActionResult TableJoins()
        {
            return RedirectToAction("TableJoins", "Tables");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateTableJoin(int primaryTableId, int joinedTableId)
        {
            return RedirectToAction("TableJoins", "Tables");
        }

        public IActionResult DeleteTableJoin(int id)
        {
            return RedirectToAction("TableJoins", "Tables");
        }

        // View all reservations with date filter (manager view)
        public async Task<IActionResult> Reservations(DateTime? filterDate)
        {
            var date = filterDate ?? DateTime.UtcNow.Date;

            var reservations = await _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Guest)
                .Include(r => r.ReservationStatus)
                .Include(r => r.ReservationTables)
                    .ThenInclude(rt => rt.Table)
                // Use ReservedFor range instead of the unmapped ReservationDate computed property
                .Where(r => r.ReservedFor >= date && r.ReservedFor < date.AddDays(1))
                .OrderBy(r => r.ReservationTime)
                .ToListAsync();

            ViewBag.FilterDate = date;
            return View("Reservations", reservations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int reservationId, int statusId, string? returnTo = null)
        {
            var user = await _userManager.GetUserAsync(User);

            var reservation = await _context.Reservations
                .Include(r => r.ReservationStatus)
                .Include(r => r.Customer)
                .Include(r => r.Guest)
                .FirstOrDefaultAsync(r => r.ReservationID == reservationId);

            if (reservation == null)
            {
                TempData["Error"] = "Reservation not found.";
                return RedirectAfterStatusChange(returnTo);
            }

            var newStatus = await _context.ReservationStatuses.FindAsync(statusId);
            if (newStatus == null)
            {
                TempData["Error"] = "Invalid status.";
                return RedirectAfterStatusChange(returnTo);
            }

            var oldStatus = reservation.ReservationStatus.StatusName;
            reservation.ReservationStatusID = statusId;
            reservation.UpdatedBy = user?.Id ?? User.Identity?.Name ?? "manager";
            reservation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await LogReservationAction(reservationId, "StatusChanged", $"From {oldStatus} to {newStatus.StatusName}", user?.Id ?? "manager");

            TempData["Message"] = $"Reservation status updated to {newStatus.StatusName}.";
            return RedirectAfterStatusChange(returnTo);
        }

        private IActionResult RedirectAfterStatusChange(string? returnTo)
        {
            if (string.Equals(returnTo, "Calendar", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "ReservationCalendar");
            }

            return RedirectToAction("Reservations");
        }

        // Override table assignment (manager can change assigned table)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OverrideTable(int reservationId, int newTableId)
        {
            var user = await _userManager.GetUserAsync(User);

            var reservation = await _context.Reservations
                .Include(r => r.ReservationTables)
                .FirstOrDefaultAsync(r => r.ReservationID == reservationId);

            if (reservation == null)
            {
                TempData["Error"] = "Reservation not found.";
                return RedirectToAction("Reservations");
            }

            var newTable = await _context.Tables.FindAsync(newTableId);
            if (newTable == null || !newTable.IsAvailable)
            {
                TempData["Error"] = "Table not available.";
                return RedirectToAction("Reservations");
            }

            var oldAssignment = reservation.ReservationTables.FirstOrDefault();
            if (oldAssignment != null)
            {
                _context.ReservationTables.Remove(oldAssignment);
            }

            var newAssignment = new ReservationTables
            {
                ReservationID = reservationId,
                TableID = newTableId,
                CreatedBy = user?.Id ?? User.Identity?.Name ?? "manager",
                UpdatedBy = user?.Id ?? User.Identity?.Name ?? "manager",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ReservationTables.Add(newAssignment);

            await _context.SaveChangesAsync();
            await LogReservationAction(reservationId, "TableChanged", $"To Table {newTable.TableNumber}", user?.Id ?? "manager");

            TempData["Message"] = $"Table changed to {newTable.TableNumber}.";
            return RedirectToAction("Reservations");
        }

        // Helper: Log reservation actions
        private async Task LogReservationAction(int reservationId, string actionName, string? oldValue, string userId)
        {
            var actionType = await _context.ActionTypes.FirstOrDefaultAsync(a => a.ActionTypeName == actionName);
            if (actionType == null)
            {
                actionType = new ActionType
                {
                    ActionTypeName = actionName,
                    Description = $"Reservation {actionName}",
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ActionTypes.Add(actionType);
                await _context.SaveChangesAsync();
            }

            var log = new ReservationLog
            {
                ReservationID = reservationId,
                ActionTypeID = actionType.ActionTypeID,
                OldValue = oldValue,
                CreatedBy = userId,
                UpdatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ReservationLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        // ================== USER MANAGEMENT ==================

        // GET: /Admin/ManageUsers
        public async Task<IActionResult> ManageUsers(string category = "managers", string search = null)
        {
            try
            {
                var employees = await _context.Employees
                    .Include(e => e.ApplicationUser)
                    .Where(e => e.ApplicationUserId != null)
                    .ToListAsync();

                var managerUsers = await _userManager.GetUsersInRoleAsync("manager");
                var employeeUsers = await _userManager.GetUsersInRoleAsync("employee");

                var managerUserIds = managerUsers.Select(u => u.Id).ToHashSet();
                var employeeUserIds = employeeUsers.Select(u => u.Id).ToHashSet();

                var managers = employees
                    .Where(e => e.ApplicationUserId != null && managerUserIds.Contains(e.ApplicationUserId))
                    .ToList();

                var staff = employees
                    .Where(e => e.ApplicationUserId != null && employeeUserIds.Contains(e.ApplicationUserId))
                    .ToList();

                var customers = await _context.Customers
                    .OrderBy(c => c.FirstName ?? "")
                    .ThenBy(c => c.LastName ?? "")
                    .ToListAsync();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var normalized = search.Trim();

                    managers = managers.Where(e =>
                        (!string.IsNullOrWhiteSpace(e.FirstName) && e.FirstName.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(e.LastName) && e.LastName.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(e.Email) && e.Email.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(e.PhoneNumber) && e.PhoneNumber.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                    ).ToList();

                    staff = staff.Where(e =>
                        (!string.IsNullOrWhiteSpace(e.FirstName) && e.FirstName.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(e.LastName) && e.LastName.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(e.Email) && e.Email.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(e.PhoneNumber) && e.PhoneNumber.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                    ).ToList();

                    customers = customers.Where(c =>
                        (!string.IsNullOrWhiteSpace(c.FirstName) && c.FirstName.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(c.LastName) && c.LastName.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(c.Email) && c.Email.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(c.PhoneNumber) && c.PhoneNumber.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                    ).ToList();

                    ViewBag.Search = normalized;
                }

                ViewBag.ActiveTab = category?.ToLowerInvariant();
                ViewBag.Managers = managers;
                ViewBag.Employees = staff;
                ViewBag.Customers = customers;

                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ManageUsers: {ex.Message}");
                TempData["Error"] = "An error occurred while loading users.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Admin/CreateUser
        public IActionResult CreateUser(string role)
        {
            role = (role ?? "").ToLowerInvariant();
            if (role != "manager" && role != "employee") return BadRequest();

            ViewBag.Role = role;
            return View(new CreateEmployeeViewModel());
        }

        // POST: /Admin/CreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(string role, CreateEmployeeViewModel model)
        {
            role = (role ?? "").ToLowerInvariant();
            if (role != "manager" && role != "employee") return BadRequest();

            if (!ModelState.IsValid)
            {
                ViewBag.Role = role;
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                ViewBag.Role = role;
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, role);

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _userManager.ConfirmEmailAsync(user, token);

            var restaurant = await _context.Restaurants.FirstOrDefaultAsync();
            if (restaurant == null)
            {
                var settings = await _context.Settings.FirstOrDefaultAsync();
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

            var employee = new Employee
            {
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                PhoneNumber = model.PhoneNumber,
                ApplicationUserId = user.Id,
                RestaurantID = restaurant.RestaurantID,
                IsActive = true,
                CreatedBy = User.Identity?.Name ?? "admin",
                UpdatedBy = User.Identity?.Name ?? "admin",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{role.ToUpper()} created successfully!";
            return RedirectToAction(nameof(ManageUsers), new { category = role + "s" });
        }

        // Toggle status
        public async Task<IActionResult> ToggleUserStatus(int id, string role)
        {
            role = (role ?? "").ToLowerInvariant();
            if (role != "manager" && role != "employee") return BadRequest();

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            employee.IsActive = !employee.IsActive;
            employee.UpdatedBy = User.Identity?.Name ?? "admin";
            employee.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{role.ToUpper()} status updated!";
            return RedirectToAction(nameof(ManageUsers), new { category = role + "s" });
        }

        // DELETE (Manager / Employee / Customer)
        public async Task<IActionResult> DeleteUser(int id, string role)
        {
            role = (role ?? "").ToLowerInvariant();

            if (role == "manager" || role == "employee")
            {
                var emp = await _context.Employees
                    .Include(e => e.ApplicationUser)
                    .FirstOrDefaultAsync(e => e.EmployeeID == id);

                if (emp == null) return NotFound();

                var userId = emp.ApplicationUserId;
                _context.Employees.Remove(emp);
                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user != null)
                    {
                        await _userManager.DeleteAsync(user);
                    }
                }

                TempData["Success"] = $"{role.ToUpper()} deleted successfully!";
                return RedirectToAction(nameof(ManageUsers), new { category = role + "s" });
            }

            if (role == "customer")
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null) return NotFound();

                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Customer deleted successfully!";
                return RedirectToAction(nameof(ManageUsers), new { category = "customers" });
            }

            return BadRequest();
        }

        // Toggle customer status
        public async Task<IActionResult> ToggleCustomerStatus(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            customer.IsActive = !customer.IsActive;
            customer.UpdatedBy = User.Identity?.Name ?? "admin";
            customer.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Customer status updated!";
            return RedirectToAction(nameof(ManageUsers), new { category = "customers" });
        }

        // ================== DASHBOARD ==================

        public IActionResult Dashboard() => View();
        public IActionResult Index() => View();
    }
}
