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
        private readonly TableAllocationService _allocationService;

        public GuestController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            IStringLocalizer<FYP.Localization.SharedResource> localizer,
            TableAllocationService allocationService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _localizer = localizer;
            _allocationService = allocationService;
        }
        
        public async Task<IActionResult> Index()
        {
            // Do not auto-redirect authenticated users; allow accessing guest reservation page
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reserve(int PartySize, DateTime ReservationDate, TimeSpan ReservationTime, string? Notes, int? Duration,
            string? FirstName, string? LastName, string GuestEmail, string? GuestPhone)
        {
            // Server-side validation
            if (PartySize < 1 || ReservationDate == default || ReservationTime == default)
            {
                TempData["Error"] = _localizer["Please select a valid party size, date and time."].Value;
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(GuestEmail))
            {
                TempData["Error"] = _localizer["Please provide your email."].Value;
                return RedirectToAction("Index");
            }

            // Use provided first and last names (already separate)
            string? firstName = string.IsNullOrWhiteSpace(FirstName) ? null : FirstName.Trim();
            string? lastName = string.IsNullOrWhiteSpace(LastName) ? null : LastName.Trim();

            var selectedDateTimeUtc = new DateTime(
                ReservationDate.Year, ReservationDate.Month, ReservationDate.Day,
                ReservationTime.Hours, ReservationTime.Minutes, 0, DateTimeKind.Utc);
            if (selectedDateTimeUtc < DateTime.UtcNow)
            {
                TempData["Error"] = _localizer["Date and time cannot be in the past."].Value;
                return RedirectToAction("Index");
            }

            // Business hours check (10:00 - 22:00)
            var open = new TimeSpan(10, 0, 0);
            var close = new TimeSpan(22, 0, 0);
            if (ReservationTime < open || ReservationTime > close)
            {
                TempData["Error"] = _localizer["Selected time is outside business hours."].Value;
                return RedirectToAction("Index");
            }

            // Get or create guest
            var guest = await _context.Guests
                .FirstOrDefaultAsync(g => g.Email == GuestEmail.Trim());

            if (guest == null)
            {
                guest = new Guest
                {
                    Email = GuestEmail.Trim(),
                    FirstName = firstName,
                    LastName = lastName,
                    PhoneNumber = string.IsNullOrWhiteSpace(GuestPhone) ? null : GuestPhone.Trim(),
                    IsActive = true,
                    CreatedBy = "guest",
                    UpdatedBy = "guest",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Guests.Add(guest);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Update guest info if provided (only update if new values are not null/empty)
                if (!string.IsNullOrWhiteSpace(firstName))
                {
                    guest.FirstName = firstName;
                }
                if (!string.IsNullOrWhiteSpace(lastName))
                {
                    guest.LastName = lastName;
                }
                guest.PhoneNumber = string.IsNullOrWhiteSpace(GuestPhone) ? null : GuestPhone.Trim();
                guest.UpdatedBy = "guest";
                guest.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // Ensure Confirmed status exists
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

            var effectiveDuration = Duration.HasValue && Duration.Value >= 45 ? Duration.Value : 90;

            // Use allocation service to find best table assignment
            var allocation = await _allocationService.FindBestAllocationAsync(
                restaurant.RestaurantID,
                ReservationDate,
                ReservationTime,
                effectiveDuration,
                PartySize);

            if (!allocation.Success)
            {
                TempData["Error"] = _localizer[allocation.ErrorMessage ?? "No tables available"].Value;
                return RedirectToAction("Index");
            }

            try
            {
                // Create reservation with allocated tables
                var reservation = await _allocationService.CreateReservationAsync(
                    allocation,
                    null,
                    guest.GuestID,
                    restaurant.RestaurantID,
                    ReservationDate,
                    ReservationTime,
                    effectiveDuration,
                    PartySize,
                    Notes,
                    confirmedStatus.ReservationStatusID,
                    false,
                    "guest");

                // Confirmation is sent by the reservation creation workflow. Show success message to guest.
                TempData["Message"] = _localizer["Thanks! Your reservation is confirmed."].Value;
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                var root = ex.GetBaseException()?.Message ?? ex.Message;
                TempData["Error"] = _localizer["Failed to save reservation: {0}", root].Value;
                return RedirectToAction("Index");
            }
        }
    }
}