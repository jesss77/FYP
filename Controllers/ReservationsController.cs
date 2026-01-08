using FYP.Data;
using FYP.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    [Authorize(Roles = "manager,employee")]
    public class ReservationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FYP.Services.IEmailService _emailService;
        private readonly FYP.Services.TableAllocationService _allocationService;

        public ReservationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            FYP.Services.IEmailService emailService,
            FYP.Services.TableAllocationService allocationService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _allocationService = allocationService;
        }

        // GET: /Reservations (List View with filters and sorting)
        public async Task<IActionResult> Index(
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
                if (fromDate.HasValue && toDate.HasValue)
                {
                    var start = fromDate.Value.Date;
                    var endExclusive = toDate.Value.Date.AddDays(1);
                    query = query.Where(r => r.ReservedFor >= start && r.ReservedFor < endExclusive);
                }
                else if (fromDate.HasValue)
                {
                    var start = fromDate.Value.Date;
                    query = query.Where(r => r.ReservedFor >= start);
                }
                else if (toDate.HasValue)
                {
                    var endExclusive = toDate.Value.Date.AddDays(1);
                    query = query.Where(r => r.ReservedFor < endExclusive);
                }
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
                        EF.Functions.Like(r.Customer.Email ?? "", $"%{s}%") ||
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
            
            // Quick stats for dashboard
            var upcomingReservations = orderedList.Where(r => r.ReservedFor >= today).Count();
            var todayWalkIns = todayReservations.Where(r => !r.ReservationType).Count();
            ViewBag.UpcomingCount = upcomingReservations;
            ViewBag.TodayCount = todayReservations.Count;
            ViewBag.TodayWalkInsCount = todayWalkIns;
            
            ViewBag.SortBy = sortBy;
            ViewBag.SortDir = sortDir;
            // Clear filters in the UI after applying search
            ViewBag.Search = "";
            ViewBag.FromDate = null;
            ViewBag.ToDate = null;
            ViewBag.Status = "";

            // Load statuses for filter dropdown
            ViewBag.Statuses = await _context.ReservationStatuses
                .OrderBy(s => s.StatusName)
                .ToListAsync();

            // Add table count for manager view
            if (User.IsInRole("manager"))
            {
                ViewBag.TableCount = await _context.Tables.CountAsync();
            }

            // Get user's employee record to render header
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id);
            if (employee == null)
            {
                return RedirectToAction("Create", "Employee");
            }

            return View(employee);
        }

        // GET: /Reservations/Calendar (Calendar View)
        public async Task<IActionResult> Calendar(DateTime? date)
        {
            var selectedDate = date ?? DateTime.UtcNow.Date;
            ViewData["SelectedDate"] = selectedDate;

            // Get business hours from settings
            var businessStart = await _context.Settings
                .Where(s => s.Key == "BusinessDayStart")
                .Select(s => s.Value)
                .FirstOrDefaultAsync() ?? "09:00:00";

            var businessEnd = await _context.Settings
                .Where(s => s.Key == "BusinessDayEnd")
                .Select(s => s.Value)
                .FirstOrDefaultAsync() ?? "22:00:00";

            ViewData["BusinessDayStart"] = businessStart;
            ViewData["BusinessDayEnd"] = businessEnd;

            // Get all tables (resources for calendar)
            var tables = await _context.Tables
                .Where(t => t.IsAvailable)
                .OrderBy(t => t.TableNumber)
                .ToListAsync();

            var resources = tables.Select(t => new
            {
                id = t.TableID.ToString(),
                title = $"Table {t.TableNumber}",
                capacity = t.Capacity,
                order = t.TableNumber
            }).ToList();

            ViewData["ResourcesJson"] = System.Text.Json.JsonSerializer.Serialize(resources);

            // Get reservations for the selected date
            var reservations = await _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Guest)
                .Include(r => r.ReservationStatus)
                .Include(r => r.ReservationTables)
                    .ThenInclude(rt => rt.Table)
                .Where(r => r.ReservedFor >= selectedDate && r.ReservedFor < selectedDate.AddDays(1))
                .ToListAsync();

            var events = reservations.SelectMany(r =>
                r.ReservationTables.Select(rt => new
                {
                    id = r.ReservationID.ToString(),
                    resourceId = rt.TableID.ToString(),
                    title = r.Guest != null
                        ? $"{r.Guest.FirstName} {r.Guest.LastName}"
                        : r.Customer != null
                            ? $"{r.Customer.FirstName} {r.Customer.LastName}"
                            : "Unknown",
                    start = $"{selectedDate:yyyy-MM-dd}T{r.ReservationTime:hh\\:mm\\:ss}",
                    end = $"{selectedDate:yyyy-MM-dd}T{r.ReservationTime.Add(TimeSpan.FromMinutes(r.Duration)):hh\\:mm\\:ss}",
                    status = r.ReservationStatus.StatusName,
                    partySize = r.PartySize,
                    isWalkIn = !r.ReservationType,
                    className = r.ReservationStatus.StatusName.ToLowerInvariant() switch
                    {
                        "confirmed" => "event-confirmed",
                        "seated" => "event-seated",
                        "completed" => "event-completed",
                        "cancelled" => "event-cancelled",
                        _ => ""
                    }
                })
            ).ToList();

            ViewData["EventsJson"] = System.Text.Json.JsonSerializer.Serialize(events);

            return View();
        }

        // GET: /Reservations/CreateWalkIn
        public IActionResult CreateWalkIn()
        {
            return View();
        }

        // POST: /Reservations/CreateWalkIn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWalkIn(int PartySize, string? Notes, int? Duration, string FirstName, string LastName, string? GuestEmail, string GuestPhone, DateTime ReservationDate, TimeSpan ReservationTime)
        {
            var user = await _userManager.GetUserAsync(User);

            if (PartySize < 1)
            {
                TempData["Error"] = "Please select a valid party size.";
                ViewBag.FirstName = FirstName;
                ViewBag.LastName = LastName;
                ViewBag.GuestEmail = GuestEmail;
                ViewBag.GuestPhone = GuestPhone;
                ViewBag.PartySize = PartySize;
                ViewBag.Notes = Notes;
                ViewBag.ReservationDate = ReservationDate;
                ViewBag.ReservationTime = ReservationTime.ToString(@"HH\:mm");
                return View();
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
            {
                TempData["Error"] = "First name and last name are required.";
                ViewBag.FirstName = FirstName;
                ViewBag.LastName = LastName;
                ViewBag.GuestEmail = GuestEmail;
                ViewBag.GuestPhone = GuestPhone;
                ViewBag.PartySize = PartySize;
                ViewBag.Notes = Notes;
                ViewBag.ReservationDate = ReservationDate;
                ViewBag.ReservationTime = ReservationTime.ToString(@"HH\:mm");
                return View();
            }

            // Validate phone number is required
            if (string.IsNullOrWhiteSpace(GuestPhone))
            {
                TempData["Error"] = "Phone number is required.";
                ViewBag.FirstName = FirstName;
                ViewBag.LastName = LastName;
                ViewBag.GuestEmail = GuestEmail;
                ViewBag.GuestPhone = GuestPhone;
                ViewBag.PartySize = PartySize;
                ViewBag.Notes = Notes;
                ViewBag.ReservationDate = ReservationDate;
                ViewBag.ReservationTime = ReservationTime.ToString(@"HH\:mm");
                return View();
            }

            // Validate date is not in the past
            var selectedDateTime = ReservationDate.Date.Add(ReservationTime);
            if (selectedDateTime < DateTime.Now)
            {
                TempData["Error"] = "Reservation date and time cannot be in the past.";
                ViewBag.FirstName = FirstName;
                ViewBag.LastName = LastName;
                ViewBag.GuestEmail = GuestEmail;
                ViewBag.GuestPhone = GuestPhone;
                ViewBag.PartySize = PartySize;
                ViewBag.Notes = Notes;
                ViewBag.ReservationDate = ReservationDate;
                ViewBag.ReservationTime = ReservationTime.ToString(@"HH\:mm");
                return View();
            }

            // Get business hours from Settings
            var businessHoursStart = new TimeSpan(10, 0, 0); // Default
            var businessHoursEnd = new TimeSpan(22, 0, 0);   // Default

            var openingHoursSetting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Key == "Opening Hours");

            if (openingHoursSetting != null && !string.IsNullOrWhiteSpace(openingHoursSetting.Value))
            {
                // Parse "10:00 - 22:00" format
                var parts = openingHoursSetting.Value.Split('-', StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                {
                    if (TimeSpan.TryParse(parts[0], out var start))
                        businessHoursStart = start;
                    if (TimeSpan.TryParse(parts[1], out var end))
                        businessHoursEnd = end;
                }
            }

            // Validate time is within business hours
            if (ReservationTime < businessHoursStart || ReservationTime > businessHoursEnd)
            {
                TempData["Error"] = $"Selected time is outside business hours ({businessHoursStart:hh\\:mm} - {businessHoursEnd:hh\\:mm}).";
                ViewBag.FirstName = FirstName;
                ViewBag.LastName = LastName;
                ViewBag.GuestEmail = GuestEmail;
                ViewBag.GuestPhone = GuestPhone;
                ViewBag.PartySize = PartySize;
                ViewBag.Notes = Notes;
                ViewBag.ReservationDate = ReservationDate;
                ViewBag.ReservationTime = ReservationTime.ToString(@"HH\:mm");
                return View();
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

            var allocation = await _allocationService.FindBestAllocationAsync(
                restaurant.RestaurantID,
                ReservationDate,
                ReservationTime,
                effectiveDuration,
                PartySize);

            if (!allocation.Success)
            {
                TempData["Error"] = allocation.ErrorMessage ?? "No tables available.";
                ViewBag.FirstName = FirstName;
                ViewBag.LastName = LastName;
                ViewBag.GuestEmail = GuestEmail;
                ViewBag.GuestPhone = GuestPhone;
                ViewBag.PartySize = PartySize;
                ViewBag.Notes = Notes;
                ViewBag.ReservationDate = ReservationDate;
                ViewBag.ReservationTime = ReservationTime.ToString(@"HH\:mm");
                return View();
            }

            try
            {
                int? customerId = null;
                int? guestId = null;

                // Check if email matches an existing customer account
                if (!string.IsNullOrWhiteSpace(GuestEmail))
                {
                    var existingCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.Email.ToLower() == GuestEmail.Trim().ToLower());

                    if (existingCustomer != null)
                    {
                        // Link to customer account
                        customerId = existingCustomer.CustomerID;
                        
                        // Update customer info if needed
                        existingCustomer.PhoneNumber = GuestPhone.Trim();
                        existingCustomer.UpdatedBy = user?.Id ?? "system";
                        existingCustomer.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // Create guest with provided details
                        var guest = new Guest
                        {
                            Email = GuestEmail.Trim(),
                            FirstName = FirstName.Trim(),
                            LastName = LastName.Trim(),
                            PhoneNumber = GuestPhone.Trim(),
                            IsActive = true,
                            CreatedBy = user?.Id ?? "system",
                            UpdatedBy = user?.Id ?? "system",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.Guests.Add(guest);
                        await _context.SaveChangesAsync();
                        guestId = guest.GuestID;
                    }
                }
                else
                {
                    // No email provided, create guest with generated email
                    var guest = new Guest
                    {
                        Email = $"walkin_{Guid.NewGuid():N}@system.local",
                        FirstName = FirstName.Trim(),
                        LastName = LastName.Trim(),
                        PhoneNumber = GuestPhone.Trim(),
                        IsActive = true,
                        CreatedBy = user?.Id ?? "system",
                        UpdatedBy = user?.Id ?? "system",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Guests.Add(guest);
                    await _context.SaveChangesAsync();
                    guestId = guest.GuestID;
                }

                var reservation = await _allocationService.CreateReservationAsync(
                    allocation,
                    customerId,
                    guestId,
                    restaurant.RestaurantID,
                    ReservationDate,
                    ReservationTime,
                    effectiveDuration,
                    PartySize,
                    Notes,
                    confirmedStatus.ReservationStatusID,
                    true,
                    user?.Id ?? "system");

                TempData["Message"] = "Walk-in reservation saved successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to create walk-in: {ex.Message}";
                ViewBag.FirstName = FirstName;
                ViewBag.LastName = LastName;
                ViewBag.GuestEmail = GuestEmail;
                ViewBag.GuestPhone = GuestPhone;
                ViewBag.PartySize = PartySize;
                ViewBag.Notes = Notes;
                ViewBag.ReservationDate = ReservationDate;
                ViewBag.ReservationTime = ReservationTime.ToString(@"HH\:mm");
                return View();
            }
        }

        // POST: /Reservations/UpdateStatus
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
            reservation.UpdatedBy = user?.Id ?? User.Identity?.Name ?? "system";
            reservation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await LogReservationAction(reservationId, "StatusChanged", $"From {oldStatus} to {newStatus.StatusName}", user?.Id ?? "system");

            TempData["Message"] = $"Reservation status updated to {newStatus.StatusName}.";
            return RedirectAfterStatusChange(returnTo);
        }

        // POST: /Reservations/OverrideTable (Manager only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "manager")]
        public async Task<IActionResult> OverrideTable(int reservationId, int newTableId)
        {
            var user = await _userManager.GetUserAsync(User);

            var reservation = await _context.Reservations
                .Include(r => r.ReservationTables)
                .FirstOrDefaultAsync(r => r.ReservationID == reservationId);

            if (reservation == null)
            {
                TempData["Error"] = "Reservation not found.";
                return RedirectToAction("Index");
            }

            var newTable = await _context.Tables.FindAsync(newTableId);
            if (newTable == null || !newTable.IsAvailable)
            {
                TempData["Error"] = "Table not available.";
                return RedirectToAction("Index");
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
                CreatedBy = user?.Id ?? User.Identity?.Name ?? "system",
                UpdatedBy = user?.Id ?? User.Identity?.Name ?? "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ReservationTables.Add(newAssignment);

            await _context.SaveChangesAsync();
            await LogReservationAction(reservationId, "TableChanged", $"To Table {newTable.TableNumber}", user?.Id ?? "system");

            TempData["Message"] = $"Table changed to {newTable.TableNumber}.";
            return RedirectToAction("Index");
        }

        // Helper: Redirect after status change
        private IActionResult RedirectAfterStatusChange(string? returnTo)
        {
            if (string.Equals(returnTo, "Calendar", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Calendar");
            }

            return RedirectToAction("Index");
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
