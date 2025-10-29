using FYP.Data;
using FYP.Models;
using FYP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    [AllowAnonymous]
    public class GuestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;

        public GuestController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }
        public async Task<IActionResult> Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reserve(int PartySize, DateTime ReservationDate, TimeSpan ReservationTime, string? Notes, int? Duration, string? ReservationName)
        {
            // Server-side validation similar to customer flow
            if (PartySize < 1 || ReservationDate == default || ReservationTime == default)
            {
                TempData["Error"] = "Please select a valid party size, date and time.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(ReservationName))
            {
                TempData["Error"] = "Please provide a name for the reservation.";
                return RedirectToAction("Index");
            }

            var selectedDateTimeUtc = new DateTime(
                ReservationDate.Year, ReservationDate.Month, ReservationDate.Day,
                ReservationTime.Hours, ReservationTime.Minutes, 0, DateTimeKind.Utc);
            if (selectedDateTimeUtc < DateTime.UtcNow)
            {
                TempData["Error"] = "Date and time cannot be in the past.";
                return RedirectToAction("Index");
            }

            // Ensure Pending status exists
            var pendingStatus = await _context.ReservationStatuses.FirstOrDefaultAsync(s => s.StatusName == "Pending");
            if (pendingStatus == null)
            {
                pendingStatus = new ReservationStatus
                {
                    StatusName = "Pending",
                    Description = "Awaiting confirmation",
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ReservationStatuses.Add(pendingStatus);
                await _context.SaveChangesAsync();
            }

            // Ensure restaurant exists
            var restaurant = await _context.Restaurants.OrderBy(r => r.RestaurantID).FirstOrDefaultAsync();
            if (restaurant == null)
            {
                restaurant = new Restaurant
                {
                    SettingsID = 1,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Restaurants.Add(restaurant);
                await _context.SaveChangesAsync();
            }

            // Get dedicated Guest customer id
            var guestCustomerId = await GuestCustomerSeeder.EnsureGuestCustomerAsync(HttpContext.RequestServices);

            var effectiveDuration = Duration.HasValue && Duration.Value >= 45 ? Duration.Value : 90;

            // Availability search
            var startUtc = new DateTime(
                ReservationDate.Year, ReservationDate.Month, ReservationDate.Day,
                ReservationTime.Hours, ReservationTime.Minutes, 0, DateTimeKind.Utc);
            var endUtc = startUtc.AddMinutes(effectiveDuration);

            var capacityTables = await _context.Tables
                .Where(t => t.IsAvailable && t.RestaurantID == restaurant.RestaurantID && t.Capacity >= PartySize)
                .OrderBy(t => t.Capacity)
                .ToListAsync();

            if (!capacityTables.Any())
            {
                TempData["Error"] = "No table can fit the selected party size.";
                return RedirectToAction("Index");
            }

            int? chosenTableId = null;
            foreach (var table in capacityTables)
            {
                var existingForTable = await (from rt in _context.ReservationTables
                                              join r in _context.Reservations on rt.ReservationID equals r.ReservationID
                                              where rt.TableID == table.TableID
                                              select new { r.ReservationDate, r.ReservationTime, r.Duration }).ToListAsync();

                var overlaps = existingForTable.Any(er =>
                {
                    var erStart = new DateTime(er.ReservationDate.Year, er.ReservationDate.Month, er.ReservationDate.Day,
                                               er.ReservationTime.Hours, er.ReservationTime.Minutes, 0, DateTimeKind.Utc);
                    var erEnd = erStart.AddMinutes(er.Duration);
                    return startUtc < erEnd && erStart < endUtc; // overlap
                });

                if (!overlaps)
                {
                    chosenTableId = table.TableID;
                    break;
                }
            }

            if (chosenTableId == null)
            {
                TempData["Error"] = "No tables are available for the selected time and duration.";
                return RedirectToAction("Index");
            }

            var effectiveNotes = Notes;
            if (!string.IsNullOrWhiteSpace(ReservationName))
            {
                effectiveNotes = string.IsNullOrWhiteSpace(Notes)
                    ? $"Reservation Name: {ReservationName}"
                    : $"Reservation Name: {ReservationName} | {Notes}";
            }

            // Transactional create
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var reservation = new Reservation
                {
                    CustomerID = guestCustomerId,
                    RestaurantID = restaurant.RestaurantID,
                    ReservationDate = ReservationDate.Date,
                    ReservationTime = ReservationTime,
                    Duration = effectiveDuration,
                    Notes = effectiveNotes,
                    ReservationStatusID = pendingStatus.ReservationStatusID,
                    ReservationType = true,
                    PartySize = Math.Max(1, PartySize),
                    CreatedBy = "guest",
                    UpdatedBy = "guest",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync();

                var link = new ReservationTables
                {
                    ReservationID = reservation.ReservationID,
                    TableID = chosenTableId.Value,
                    CreatedBy = "guest",
                    UpdatedBy = "guest",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ReservationTables.Add(link);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();

                TempData["Message"] = "Thanks! Please check your email to confirm your booking.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                var root = ex.GetBaseException()?.Message ?? ex.Message;
                TempData["Error"] = $"Failed to save reservation: {root}";
                return RedirectToAction("Index");
            }
        }
    }
}