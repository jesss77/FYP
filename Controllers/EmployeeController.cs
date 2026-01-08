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
    [Authorize(Roles = "employee,manager,admin")]
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly TableAllocationService _allocationService;

        public EmployeeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            TableAllocationService allocationService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _allocationService = allocationService;
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

        // Employee Dashboard - shows today's summary
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);

            // Get the employee record for the current user
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id);

            if (employee == null)
            {
                return RedirectToAction("Create");
            }

            // Get today's reservations summary (based on ReservedFor)
            var today = DateTime.UtcNow.Date;
            var todayReservations = await _context.Reservations
                .Include(r => r.ReservationStatus)
                .Include(r => r.Customer)
                .Include(r => r.Guest)
                .Include(r => r.ReservationTables)
                    .ThenInclude(rt => rt.Table)
                .Where(r => r.ReservedFor.Date == today)
                .OrderBy(r => r.ReservationTime)
                .ToListAsync();

            ViewBag.TodayReservations = todayReservations;
            ViewBag.TodayDate = today;
            
            return View(employee);
        }

        // Index should redirect to Reservations
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id);
            if (employee == null)
            {
                return RedirectToAction("Create");
            }
            return RedirectToAction("Index", "Reservations");
        }

        // Reservations - redirects to Reservations controller
        public IActionResult Reservations()
        {
            return RedirectToAction("Index", "Reservations");
        }

        // Create walk-in reservation - redirects to Reservations controller
        public IActionResult CreateWalkIn()
        {
            return RedirectToAction("CreateWalkIn", "Reservations");
        }

        // Change assigned table for a reservation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeAssignedTable(int reservationId, string tableSelection)
        {
            var user = await _userManager.GetUserAsync(User);

            var reservation = await _context.Reservations
                .Include(r => r.ReservationTables)
                .FirstOrDefaultAsync(r => r.ReservationID == reservationId);

            if (reservation == null)
            {
                TempData["Error"] = "Reservation not found.";
                return RedirectToAction("Calendar", "Reservations", new { date = DateTime.UtcNow.Date });
            }

            // Store the reservation date for redirect
            var reservationDate = reservation.ReservedFor.Date;

            // Parse table selection (format: "single_5" or "joined_3_7")
            var parts = tableSelection.Split('_');
            if (parts.Length < 2)
            {
                TempData["Error"] = "Invalid table selection.";
                return RedirectToAction("Calendar", "Reservations", new { date = reservationDate });
            }

            var selectionType = parts[0]; // "single" or "joined"
            
            // Remove old table assignments
            if (reservation.ReservationTables != null && reservation.ReservationTables.Any())
            {
                _context.ReservationTables.RemoveRange(reservation.ReservationTables);
            }

            if (selectionType == "single")
            {
                // Assign single table
                var tableId = int.Parse(parts[1]);
                var table = await _context.Tables.FindAsync(tableId);
                
                if (table == null || !table.IsAvailable)
                {
                    TempData["Error"] = "Selected table is not available.";
                    return RedirectToAction("Calendar", "Reservations", new { date = reservationDate });
                }

                var newAssignment = new ReservationTables
                {
                    ReservationID = reservationId,
                    TableID = tableId,
                    CreatedBy = user?.Id ?? "employee",
                    UpdatedBy = user?.Id ?? "employee",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ReservationTables.Add(newAssignment);

                await _context.SaveChangesAsync();
                TempData["Message"] = $"Table changed to Table {table.TableNumber} successfully!";
            }
            else if (selectionType == "joined")
            {
                // Assign joined tables
                var table1Id = int.Parse(parts[1]);
                var table2Id = int.Parse(parts[2]);

                var table1 = await _context.Tables.FindAsync(table1Id);
                var table2 = await _context.Tables.FindAsync(table2Id);

                if (table1 == null || table2 == null || !table1.IsAvailable || !table2.IsAvailable)
                {
                    TempData["Error"] = "One or more selected tables are not available.";
                    return RedirectToAction("Calendar", "Reservations", new { date = reservationDate });
                }

                // Add both tables to reservation
                _context.ReservationTables.Add(new ReservationTables
                {
                    ReservationID = reservationId,
                    TableID = table1Id,
                    CreatedBy = user?.Id ?? "employee",
                    UpdatedBy = user?.Id ?? "employee",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                _context.ReservationTables.Add(new ReservationTables
                {
                    ReservationID = reservationId,
                    TableID = table2Id,
                    CreatedBy = user?.Id ?? "employee",
                    UpdatedBy = user?.Id ?? "employee",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                TempData["Message"] = $"Tables changed to Tables {table1.TableNumber} + {table2.TableNumber} successfully!";
            }

            return RedirectToAction("Calendar", "Reservations", new { date = reservationDate });
        }

        // Update reservation status
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
                return RedirectAfterStatusChange(returnTo, reservation?.ReservedFor);
            }

            var newStatus = await _context.ReservationStatuses.FindAsync(statusId);
            if (newStatus == null)
            {
                TempData["Error"] = "Invalid status.";
                return RedirectAfterStatusChange(returnTo, reservation?.ReservedFor);
            }

            var oldStatus = reservation.ReservationStatus.StatusName;
            reservation.ReservationStatusID = statusId;
            reservation.UpdatedBy = user?.Id ?? User.Identity?.Name ?? "employee";
            reservation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await LogReservationActionAsync(reservationId, "StatusChanged", $"From {oldStatus} to {newStatus.StatusName}", user?.Id ?? "employee");

            TempData["Message"] = $"Reservation status updated to {newStatus.StatusName}.";
            return RedirectAfterStatusChange(returnTo, reservation.ReservedFor);
        }

        private IActionResult RedirectAfterStatusChange(string? returnTo, DateTime? reservationDate)
        {
            var date = reservationDate?.Date ?? DateTime.UtcNow.Date;
            
            if (string.Equals(returnTo, "Calendar", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Calendar", "Reservations", new { date });
            }

            return RedirectToAction("Index", "Reservations");
        }

        private async Task LogReservationActionAsync(int reservationId, string actionName, string? oldValue, string userId)
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
    }
}
