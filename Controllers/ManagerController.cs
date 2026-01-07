using FYP.Data;
using FYP.Models;
using FYP.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    [Authorize(Roles = "manager")]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IReportService _reportService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FYP.Services.TableAllocationService _allocationService;

        public ManagerController(ApplicationDbContext context, IReportService reportService, UserManager<ApplicationUser> userManager, FYP.Services.TableAllocationService allocationService)
        {
            _context = context;
            _reportService = reportService;
            _userManager = userManager;
            _allocationService = allocationService;
        }
        public IActionResult Index()
        {
            return View("Index");
        }
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
        public IActionResult CreateTable(FYP.Models.Table model)
        {
            return RedirectToAction("CreateTable", "Tables");
        }

        public async Task<IActionResult> EditTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();
            return View("EditTable", table);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTable(int id, FYP.Models.Table model)
        {
            if (id != model.TableID) return BadRequest();

            ModelState.Remove("CreatedBy");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("UpdatedBy");
            ModelState.Remove("UpdatedAt");
            ModelState.Remove("Restaurant");
            ModelState.Remove("RestaurantID");

            if (!ModelState.IsValid)
                return View("EditTable", model);

            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

                if (await _context.Tables.AnyAsync(t => t.TableNumber == model.TableNumber && t.RestaurantID == table.RestaurantID && t.TableID != id))
                {
                    ModelState.AddModelError("TableNumber", "A table with this number already exists.");
                    return View("EditTable", model);
                }

            table.TableNumber = model.TableNumber;
            table.Capacity = model.Capacity;
            table.IsJoinable = model.IsJoinable;
            table.IsAvailable = model.IsAvailable;
            table.UpdatedBy = User.Identity?.Name ?? "manager";
            table.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Table {model.TableNumber} updated successfully!";
            return RedirectToAction(nameof(Tables));
        }

        public async Task<IActionResult> DeleteTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            _context.Tables.Remove(table);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Table {table.TableNumber} deleted successfully!";
            return RedirectToAction(nameof(Tables));
        }

        public async Task<IActionResult> ToggleTableAvailability(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound();

            table.IsAvailable = !table.IsAvailable;
            table.UpdatedBy = User.Identity?.Name ?? "manager";
            table.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Table {table.TableNumber} availability updated.";
            return RedirectToAction(nameof(Tables));
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

        // Create walk-in reservation (manager)
        public IActionResult CreateWalkIn()
        {
            return View("CreateWalkIn");
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

            var confirmedStatus = await _context.ReservationStatuses
                .FirstOrDefaultAsync(s => s.StatusName == "Confirmed");
            if (confirmedStatus == null)
            {
                TempData["Error"] = "Reservation status not configured.";
                return RedirectToAction("CreateWalkIn");
            }

            var restaurant = await _context.Restaurants.FirstOrDefaultAsync();
            if (restaurant == null)
            {
                TempData["Error"] = "Restaurant not configured.";
                return RedirectToAction("CreateWalkIn");
            }

            var effectiveDuration = Duration ?? 90;
            var now = DateTime.Now;

            var allocation = await _allocationService.FindBestAllocationAsync(
                restaurant.RestaurantID,
                now.Date,
                new TimeSpan(now.Hour, now.Minute, 0),
                effectiveDuration,
                PartySize);

            if (!allocation.Success)
            {
                TempData["Error"] = allocation.ErrorMessage ?? "No tables available.";
                return RedirectToAction("CreateWalkIn");
            }

            try
            {
                int? customerId = null;
                int? guestId = null;
                bool isGuest = !string.IsNullOrWhiteSpace(GuestName);

                if (isGuest)
                {
                    var nameParts = GuestName!.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    var firstName = nameParts.Length > 0 ? nameParts[0] : "Walk-in";
                    var lastName = nameParts.Length > 1 ? nameParts[1] : "Guest";

                    var guest = new Guest
                    {
                        Email = $"walkin_{Guid.NewGuid():N}@system.local",
                        FirstName = firstName,
                        LastName = lastName,
                        PhoneNumber = string.IsNullOrWhiteSpace(GuestPhone) ? null : GuestPhone,
                        IsActive = true,
                        CreatedBy = user?.Id ?? "manager",
                        UpdatedBy = user?.Id ?? "manager",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Guests.Add(guest);
                    await _context.SaveChangesAsync();
                    guestId = guest.GuestID;
                }
                else
                {
                    customerId = await FYP.Data.WalkInCustomerSeeder.EnsureWalkInCustomerAsync(HttpContext.RequestServices);
                }

                var reservation = await _allocationService.CreateReservationAsync(
                    allocation,
                    customerId,
                    guestId,
                    restaurant.RestaurantID,
                    now.Date,
                    new TimeSpan(now.Hour, now.Minute, 0),
                    effectiveDuration,
                    PartySize,
                    Notes,
                    confirmedStatus.ReservationStatusID,
                    true,
                    user?.Id ?? "manager");

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

        // View all reservations with sorting, search, and optional date range filters
        public async Task<IActionResult> Reservations(
            string sortBy = "date",
            string sortDir = "desc",
            string? search = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? status = null)
        {
            // Base query including related entities needed for display
            var query = _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Guest)
                .Include(r => r.ReservationStatus)
                .Include(r => r.ReservationTables)
                    .ThenInclude(rt => rt.Table)
                .AsQueryable();

            // Date range filter (optional)
            if (fromDate.HasValue || toDate.HasValue)
            {
                var start = (fromDate ?? DateTime.MinValue).Date;
                var endExclusive = ((toDate ?? DateTime.MaxValue).Date).AddDays(1);
                query = query.Where(r => r.ReservedFor >= start && r.ReservedFor < endExclusive);
            }

            // Status filter (optional)
            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.Trim();
                query = query.Where(r => r.ReservationStatus != null && r.ReservationStatus.StatusName == s);
            }

            // Search by customer/guest name, email, or phone (optional)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(r =>
                    (r.Guest != null && (
                        EF.Functions.Like(r.Guest.FirstName ?? "", $"%{s}%") ||
                        EF.Functions.Like(r.Guest.LastName ?? "", $"%{s}%") ||
                        EF.Functions.Like(r.Guest.Email ?? "", $"%{s}%") ||
                        EF.Functions.Like(r.Guest.PhoneNumber ?? "", $"%{s}%")
                    )) ||
                    (r.Customer != null && (
                        EF.Functions.Like(r.Customer.FirstName ?? "", $"%{s}%") ||
                        EF.Functions.Like(r.Customer.LastName ?? "", $"%{s}%") ||
                        EF.Functions.Like(r.Customer.PhoneNumber ?? "", $"%{s}%")
                    ))
                );
            }

            // Materialize so we can apply flexible sorting keys
            var allReservations = await query.ToListAsync();

            // Helper functions for sorting keys
            string NameOf(Reservation r)
                => r.Guest != null
                    ? $"{r.Guest.FirstName} {r.Guest.LastName}".Trim()
                    : r.Customer != null
                        ? $"{r.Customer.FirstName} {r.Customer.LastName}".Trim()
                        : "";

            int PrimaryTableNumber(Reservation r)
                => (r.ReservationTables != null && r.ReservationTables.Any())
                    ? r.ReservationTables.Select(rt => rt.Table?.TableNumber ?? 0).DefaultIfEmpty(0).Min()
                    : 0;

            // Default sort: by date/time descending (most recent first)
            IOrderedEnumerable<Reservation> ordered = (sortBy?.ToLowerInvariant()) switch
            {
                "name" => (sortDir?.ToLowerInvariant() == "asc")
                    ? allReservations.OrderBy(NameOf).ThenBy(r => r.ReservedFor).ThenBy(r => r.ReservationTime)
                    : allReservations.OrderByDescending(NameOf).ThenByDescending(r => r.ReservedFor).ThenByDescending(r => r.ReservationTime),
                "status" => (sortDir?.ToLowerInvariant() == "asc")
                    ? allReservations.OrderBy(r => r.ReservationStatus?.StatusName ?? "")
                    : allReservations.OrderByDescending(r => r.ReservationStatus?.StatusName ?? ""),
                "party" => (sortDir?.ToLowerInvariant() == "asc")
                    ? allReservations.OrderBy(r => r.PartySize)
                    : allReservations.OrderByDescending(r => r.PartySize),
                "table" => (sortDir?.ToLowerInvariant() == "asc")
                    ? allReservations.OrderBy(PrimaryTableNumber)
                    : allReservations.OrderByDescending(PrimaryTableNumber),
                _ => (sortDir?.ToLowerInvariant() == "asc")
                    ? allReservations.OrderBy(r => r.ReservedFor).ThenBy(r => r.ReservationTime)
                    : allReservations.OrderByDescending(r => r.ReservedFor).ThenByDescending(r => r.ReservationTime)
            };

            var orderedList = ordered.ToList();

            // Today's summary for dashboard section
            var today = DateTime.UtcNow.Date;
            var todayReservations = orderedList
                .Where(r => r.ReservedFor.Date == today)
                .OrderBy(r => r.ReservationTime)
                .ToList();

            // Populate viewbags
            ViewBag.AllReservations = orderedList;
            ViewBag.TodayReservations = todayReservations;
            ViewBag.TodayDate = today;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDir = sortDir;
            ViewBag.Search = search ?? "";
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.Status = status ?? "";

            // Load statuses for filter dropdown
            ViewBag.Statuses = await _context.ReservationStatuses
                .OrderBy(s => s.StatusName)
                .ToListAsync();

            // Get manager's employee record to render header
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id);
            if (employee == null)
            {
                return RedirectToAction("Create", "Employee");
            }

            return View("Reservations", employee);
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

    }
}
