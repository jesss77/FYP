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

            // Get today's reservations summary
            var today = DateTime.UtcNow.Date;
            var todayReservations = await _context.Reservations
                .Include(r => r.ReservationStatus)
                .Include(r => r.Customer)
                .Include(r => r.Guest)
                .Include(r => r.ReservationTables)
                    .ThenInclude(rt => rt.Table)
                .Where(r => r.ReservationDate == today)
                .OrderBy(r => r.ReservationTime)
                .ToListAsync();

            ViewBag.TodayReservations = todayReservations;
            ViewBag.TodayDate = today;
            
            return View(employee);
        }

        // Index redirects to Dashboard or shows today's reservations in full view
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            // Get the employee record for the current user
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id);

            if (employee == null)
            {
                return RedirectToAction("Create");
            }

            // Show today's reservations by default in index
            return RedirectToAction("Reservations");
        }

        // View all reservations with date filter
        public async Task<IActionResult> Reservations(DateTime? filterDate)
        {
            var date = filterDate ?? DateTime.UtcNow.Date;

            var reservations = await _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Guest)
                .Include(r => r.ReservationStatus)
                .Include(r => r.ReservationTables)
                    .ThenInclude(rt => rt.Table)
                .Where(r => r.ReservationDate == date)
                .OrderBy(r => r.ReservationTime)
                .ToListAsync();

            ViewBag.FilterDate = date;
            return View(reservations);
        }

        // Create walk-in reservation
        public IActionResult CreateWalkIn()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWalkIn(int PartySize, string? Notes, int? Duration, string? GuestName, string? GuestPhone)
        {
            var user = await _userManager.GetUserAsync(User);
            
            if (PartySize < 1)
            {
                TempData["Error"] = "Please select a valid party size.";
                return RedirectToAction("CreateWalkIn");
            }

            // Auto-confirm walk-ins
            var confirmedStatus = await _context.ReservationStatuses
                .FirstOrDefaultAsync(s => s.StatusName == "Confirmed");

            var restaurant = await _context.Restaurants.FirstOrDefaultAsync();
            if (restaurant == null)
            {
                TempData["Error"] = "Restaurant not configured.";
                return RedirectToAction("CreateWalkIn");
            }

            var effectiveDuration = Duration ?? 90;
            // Use local time for walk-in creation so calendar reflects actual local time
            var now = DateTime.Now;

            // Use allocation service to find best table assignment
            var allocation = await _allocationService.FindBestAllocationAsync(
                restaurant.RestaurantID,
                now.Date,
                new TimeSpan(now.Hour, now.Minute, 0),
                effectiveDuration,
                PartySize);

            if (!allocation.Success)
            {
                TempData["Error"] = allocation.ErrorMessage;
                return RedirectToAction("CreateWalkIn");
            }

            try
            {
                int? customerId = null;
                int? guestId = null;
                bool isGuest = !string.IsNullOrWhiteSpace(GuestName);

                if (isGuest)
                {
                    // Create or get guest record for walk-in
                    var nameParts = GuestName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    var firstName = nameParts.Length > 0 ? nameParts[0] : "Walk-in";
                    var lastName = nameParts.Length > 1 ? nameParts[1] : "Guest";

                    var guest = new Guest
                    {
                        Email = $"walkin_{Guid.NewGuid():N}@system.local", // Unique email for walk-ins
                        FirstName = firstName,
                        LastName = lastName,
                        PhoneNumber = GuestPhone,
                        IsActive = true,
                        CreatedBy = user.Id,
                        UpdatedBy = user.Id,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Guests.Add(guest);
                    await _context.SaveChangesAsync();
                    guestId = guest.GuestID;
                }
                else
                {
                    // Use walk-in customer ID for anonymous walk-ins
                    customerId = await WalkInCustomerSeeder.EnsureWalkInCustomerAsync(HttpContext.RequestServices);
                }

                // Create reservation with allocated tables
                var reservation = await _allocationService.CreateReservationAsync(
                    allocation,
                    customerId,
                    guestId,
                    isGuest,
                    restaurant.RestaurantID,
                    now.Date,
                    new TimeSpan(now.Hour, now.Minute, 0),
                    effectiveDuration,
                    PartySize,
                    Notes,
                    confirmedStatus.ReservationStatusID,
                    true,
                    user.Id);

                var tableInfo = allocation.AllocatedTableIds.Count > 1
                    ? $"{allocation.AllocatedTableIds.Count} joined tables ({string.Join(", ", allocation.AllocatedTableIds)})"
                    : $"Table {allocation.AllocatedTableIds[0]}";

                TempData["Message"] = $"Walk-in reservation created! {tableInfo} assigned. {allocation.AllocationStrategy}";
                return RedirectToAction("Reservations");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to create walk-in: {ex.Message}";
                return RedirectToAction("CreateWalkIn");
            }
        }

        // Update reservation status
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int reservationId, int statusId, string? returnTo = null)
        {
            var user = await _userManager.GetUserAsync(User);
            
            var reservation = await _context.Reservations
                .Include(r => r.ReservationStatus)
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
            reservation.UpdatedBy = user.Id;
            reservation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await LogReservationAction(reservationId, "StatusChanged", $"From {oldStatus} to {newStatus.StatusName}", user.Id);

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

        // Override table assignment
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

            // Remove old table assignment
            var oldAssignment = reservation.ReservationTables.FirstOrDefault();
            if (oldAssignment != null)
            {
                _context.ReservationTables.Remove(oldAssignment);
            }

            // Add new table assignment
            var newAssignment = new ReservationTables
            {
                ReservationID = reservationId,
                TableID = newTableId,
                CreatedBy = user.Id,
                UpdatedBy = user.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ReservationTables.Add(newAssignment);

            await _context.SaveChangesAsync();
            await LogReservationAction(reservationId, "TableChanged", $"To Table {newTable.TableNumber}", user.Id);

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
    }
}
