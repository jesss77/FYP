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

        public ManagerController(ApplicationDbContext context, IReportService reportService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _reportService = reportService;
            _userManager = userManager;
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
                // Compare by range on ReservedFor to avoid translating unmapped members or Date property
                .Where(r => r.ReservedFor >= date && r.ReservedFor < date.AddDays(1))
                .OrderBy(r => r.ReservationTime)
                .ToListAsync();

            ViewBag.FilterDate = date;

            // Try to get the manager's employee record so the view can show the manager name
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Challenge();

                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id);
                if (employee == null)
                {
                    // No employee record for manager; redirect to employee creation flow
                    return RedirectToAction("Create", "Employee");
                }

                ViewBag.TodayReservations = reservations;
                ViewBag.TodayDate = date;
                return View("Reservations", employee);
            }
            catch
            {
                // Fallback: render reservations list view directly if any error occurs
                ViewBag.TodayReservations = reservations;
                ViewBag.TodayDate = date;
                return View("Reservations", null);
            }
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
