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
    [Authorize]
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;

        public CustomerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Create()
        {
            var model = new Customer
            {
                Email = TempData["Email"]?.ToString(),
                ApplicationUserId = TempData["UserId"]?.ToString(),
                CreatedBy = TempData["UserId"]?.ToString(),
                UpdatedBy = TempData["UserId"]?.ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var defaultImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pics", "pfp.jpg");
            if (System.IO.File.Exists(defaultImagePath))
            {
                model.PictureBytes = await System.IO.File.ReadAllBytesAsync(defaultImagePath);
            }
            else
            {
                model.PictureBytes = Array.Empty<byte>();
            }

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer model, IFormFile? picture)
        {
            ModelState.Remove("ApplicationUser");
            ModelState.Remove("PictureBytes");

            if (!ModelState.IsValid)
                return View(model);

            model.ApplicationUserId ??= TempData["UserId"]?.ToString();
            model.Email ??= TempData["Email"]?.ToString();

            model.CreatedBy ??= model.ApplicationUserId;
            model.UpdatedBy ??= model.ApplicationUserId;
            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;

            if (picture != null && picture.Length > 0)
            {
                using var ms = new MemoryStream();
                await picture.CopyToAsync(ms);
                model.PictureBytes = ms.ToArray();
            }
            else
            {
                var defaultImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pics", "pfp.jpg");
                if (System.IO.File.Exists(defaultImagePath))
                    model.PictureBytes = await System.IO.File.ReadAllBytesAsync(defaultImagePath);
                else
                    model.PictureBytes = Array.Empty<byte>();
            }

            _context.Customers.Add(model);
            await _context.SaveChangesAsync();

            // Email confirmation was already sent during registration
            // No need to send another confirmation email here

            // Redirect to login page with message about email confirmation
            TempData["Message"] = "Registration successful! Please check your email and confirm your account before logging in.";
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        public async Task<IActionResult> Dashboard()
        {
            // Check if user's email is confirmed
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("/Account/ConfirmEmail", new { area = "Identity", email = user?.Email });
            }

            // Get the customer record for the current user
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id);

            if (customer == null)
            {
                return RedirectToAction("Create");
            }

            return View(customer);
        }

        public async Task<IActionResult> Index()
        {
            // Check if user's email is confirmed
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("/Account/ConfirmEmail", new { area = "Identity", email = user?.Email });
            }

            // Get the customer record for the current user
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id);

            if (customer == null)
            {
                return RedirectToAction("Create");
            }

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reserve(int PartySize, DateTime ReservationDate, TimeSpan ReservationTime, string? Notes, int? Duration, string? ReservationName)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("/Account/ConfirmEmail", new { area = "Identity", email = user?.Email });
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id);
            if (customer == null)
            {
                return RedirectToAction("Create");
            }

            // Server-side validation: required and not in the past
            if (PartySize < 1)
            {
                TempData["Error"] = "Please select a valid party size.";
                return RedirectToAction("Index");
            }
            if (ReservationDate == default || ReservationTime == default)
            {
                TempData["Error"] = "Please select a valid date and time.";
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

            // Ensure there is a Pending status id (seed provides ID=1). Fallback creates it once if missing.
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

            var effectiveNotes = Notes;
            if (!string.IsNullOrWhiteSpace(ReservationName))
            {
                effectiveNotes = string.IsNullOrWhiteSpace(Notes)
                    ? $"Reservation Name: {ReservationName}"
                    : $"Reservation Name: {ReservationName} | {Notes}";
            }

            // Ensure a valid restaurant exists; pick the first, create one if necessary
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

            // Transactionally create reservation and link to table
            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                var reservation = new Reservation
                {
                    CustomerID = customer.CustomerID,
                    RestaurantID = restaurant.RestaurantID,
                    ReservationDate = ReservationDate.Date,
                    ReservationTime = ReservationTime,
                    Duration = effectiveDuration,
                    Notes = effectiveNotes,
                    ReservationStatusID = pendingStatus.ReservationStatusID,
                    ReservationType = true, // pre-booked (not walk-in)
                    PartySize = Math.Max(1, PartySize),
                    CreatedBy = user.Id,
                    UpdatedBy = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync();

                var link = new ReservationTables
                {
                    ReservationID = reservation.ReservationID,
                    TableID = chosenTableId.Value,
                    CreatedBy = user.Id,
                    UpdatedBy = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ReservationTables.Add(link);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();

                TempData["Message"] = "Reservation submitted! We'll confirm shortly.";
                return RedirectToAction("Index");
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