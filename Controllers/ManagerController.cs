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

        public async Task<IActionResult> Tables()
        {
            var tables = await _context.Tables
                .Include(t => t.Restaurant)
                .OrderBy(t => t.TableNumber)
                .ToListAsync();

            return View("Tables", tables);
        }
        public IActionResult Index()
        {
            return View("Index");
        }

        public async Task<IActionResult> CreateTable()
        {
            return View("CreateTable");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTable(FYP.Models.Table model)
        {
            try
            {
                ModelState.Remove("CreatedBy");
                ModelState.Remove("CreatedAt");
                ModelState.Remove("UpdatedBy");
                ModelState.Remove("UpdatedAt");
                ModelState.Remove("Restaurant");
                ModelState.Remove("RestaurantID");

                if (!ModelState.IsValid)
                {
                    return View("CreateTable", model);
                }

                var restaurant = await _context.Restaurants.FirstOrDefaultAsync();
                if (restaurant == null)
                {
                    var settings = await _context.Settings.FirstOrDefaultAsync();
                    if (settings == null)
                    {
                        settings = new FYP.Models.Settings
                        {
                            Key = "Name",
                            Value = "Fine O Dine",
                            CreatedBy = User.Identity?.Name ?? "manager",
                            UpdatedBy = User.Identity?.Name ?? "manager",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.Settings.Add(settings);
                        await _context.SaveChangesAsync();
                    }

                    restaurant = new FYP.Models.Restaurant
                    {
                        SettingsID = settings.SettingsID,
                        CreatedBy = User.Identity?.Name ?? "manager",
                        UpdatedBy = User.Identity?.Name ?? "manager",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Restaurants.Add(restaurant);
                    await _context.SaveChangesAsync();
                }

                if (await _context.Tables.AnyAsync(t => t.TableNumber == model.TableNumber && t.RestaurantID == restaurant.RestaurantID))
                {
                    ModelState.AddModelError("TableNumber", "A table with this number already exists.");
                    return View("CreateTable", model);
                }

                model.RestaurantID = restaurant.RestaurantID;
                model.Restaurant = restaurant;
                model.CreatedBy = User.Identity?.Name ?? "manager";
                model.UpdatedBy = User.Identity?.Name ?? "manager";
                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;

                _context.Tables.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Table {model.TableNumber} created successfully!";
                return RedirectToAction(nameof(Tables));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View("CreateTable", model);
            }
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

        public async Task<IActionResult> TableJoins()
        {
            var joins = await _context.TablesJoins
                .Include(tj => tj.PrimaryTable)
                .Include(tj => tj.JoinedTable)
                .OrderBy(tj => tj.TablesJoinID)
                .ToListAsync();

            ViewBag.Tables = await _context.Tables.OrderBy(t => t.TableNumber).ToListAsync();
            return View("~/Views/Manager/TableJoins.cshtml", joins);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTableJoin(int primaryTableId, int joinedTableId, int totalCapacity)
        {
            if (primaryTableId == joinedTableId)
            {
                TempData["Error"] = "Cannot join a table to itself.";
                return RedirectToAction(nameof(TableJoins));
            }

            var exists = await _context.TablesJoins.AnyAsync(tj => (tj.PrimaryTableID == primaryTableId && tj.JoinedTableID == joinedTableId) || (tj.PrimaryTableID == joinedTableId && tj.JoinedTableID == primaryTableId));
            if (exists)
            {
                TempData["Error"] = "This join already exists.";
                return RedirectToAction(nameof(TableJoins));
            }

            var join = new FYP.Models.TablesJoin
            {
                PrimaryTableID = primaryTableId,
                JoinedTableID = joinedTableId,
                TotalCapacity = totalCapacity,
                CreatedBy = User.Identity?.Name ?? "manager",
                UpdatedBy = User.Identity?.Name ?? "manager",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.TablesJoins.Add(join);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Table join created.";
            return RedirectToAction(nameof(TableJoins));
        }

        public async Task<IActionResult> DeleteTableJoin(int id)
        {
            var join = await _context.TablesJoins.FindAsync(id);
            if (join == null) return NotFound();

            _context.TablesJoins.Remove(join);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Table join removed.";
            return RedirectToAction(nameof(TableJoins));
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
                .Where(r => r.ReservationDate == date)
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

    }
}
