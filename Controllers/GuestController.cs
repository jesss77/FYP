using FYP.Data;
using FYP.Models;
using FYP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
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
        private readonly IStringLocalizer<FYP.Localization.SharedResource> _localizer;

        public GuestController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            IStringLocalizer<FYP.Localization.SharedResource> localizer)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _localizer = localizer;
        }
        public async Task<IActionResult> Index()
        {
            // If authenticated customer, redirect to customer reservation
            if (User?.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Customer");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reserve(int PartySize, DateTime ReservationDate, TimeSpan ReservationTime, string? Notes, int? Duration,
            string GuestName, string GuestEmail, string? GuestPhone)
        {
            // Server-side validation similar to customer flow
            if (PartySize < 1 || ReservationDate == default || ReservationTime == default)
            {
                TempData["Error"] = _localizer["Please select a valid party size, date and time."];
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(GuestName) || string.IsNullOrWhiteSpace(GuestEmail))
            {
                TempData["Error"] = _localizer["Please provide your name and email."];
                return RedirectToAction("Index");
            }

            var selectedDateTimeUtc = new DateTime(
                ReservationDate.Year, ReservationDate.Month, ReservationDate.Day,
                ReservationTime.Hours, ReservationTime.Minutes, 0, DateTimeKind.Utc);
            if (selectedDateTimeUtc < DateTime.UtcNow)
            {
                TempData["Error"] = _localizer["Date and time cannot be in the past."];
                return RedirectToAction("Index");
            }

            // Optional: Basic business hours check (10:00 - 22:00) or use Settings if available
            var open = new TimeSpan(10, 0, 0);
            var close = new TimeSpan(22, 0, 0);
            if (ReservationTime < open || ReservationTime > close)
            {
                TempData["Error"] = _localizer["Selected time is outside business hours."];
                return RedirectToAction("Index");
            }

            // Ensure Pending status exists
            var confirmedStatus = await _context.ReservationStatuses.FirstOrDefaultAsync(s => s.StatusName == "Confirmed");
            if (confirmedStatus == null)
            {
                confirmedStatus = new ReservationStatus
                {
                    StatusName = "Confirmed",
                    Description = "Confirmed by system",
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ReservationStatuses.Add(confirmedStatus);
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

            // Dedicated Guest customer id (non-auth reserved customer)
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
                TempData["Error"] = _localizer["No table can fit the selected party size."];
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
                TempData["Error"] = _localizer["No tables are available for the selected time and duration."];
                return RedirectToAction("Index");
            }

            var effectiveNotes = Notes;

            // Transactional create
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var reservation = new Reservation
                {
                    CustomerID = guestCustomerId,
                    IsGuest = true,
                    GuestName = GuestName,
                    GuestEmail = GuestEmail,
                    GuestPhone = string.IsNullOrWhiteSpace(GuestPhone) ? null : GuestPhone,
                    RestaurantID = restaurant.RestaurantID,
                    ReservationDate = ReservationDate.Date,
                    ReservationTime = ReservationTime,
                    Duration = effectiveDuration,
                    Notes = effectiveNotes,
                    ReservationStatusID = confirmedStatus.ReservationStatusID,
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

                // Send confirmation email
                var restaurantName = _localizer["BrandName"].Value;
                var subject = _localizer["Your reservation at {0} is confirmed", restaurantName].Value;
                var body = $@"
                    <h2>{_localizer["Reservation Confirmed"]}</h2>
                    <p>{_localizer["Hello"]} {System.Net.WebUtility.HtmlEncode(GuestName)},</p>
                    <p>{_localizer["Your reservation details are below:"]}</p>
                    <ul>
                        <li>{_localizer["Date"]}: {ReservationDate:yyyy-MM-dd}</li>
                        <li>{_localizer["Time"]}: {ReservationTime}</li>
                        <li>{_localizer["Duration"]}: {effectiveDuration} min</li>
                        <li>{_localizer["Party Size"]}: {PartySize}</li>
                        <li>{_localizer["Table"]}: {chosenTableId}</li>
                        <li>{_localizer["Restaurant"]}: {restaurantName}</li>
                    </ul>
                    <p>{_localizer["We look forward to welcoming you."]}</p>";

                await _emailService.SendEmailAsync(GuestEmail, subject, body);

                await tx.CommitAsync();

                TempData["Message"] = _localizer["Thanks! Your reservation is confirmed. A confirmation email has been sent."];
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                var root = ex.GetBaseException()?.Message ?? ex.Message;
                TempData["Error"] = _localizer["Failed to save reservation: {0}", root];
                return RedirectToAction("Index");
            }
        }
    }
}