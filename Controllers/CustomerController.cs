using FYP.Data;
using FYP.Models;
using FYP.Services;
using FYP.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
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
        private readonly IStringLocalizer<FYP.Localization.SharedResource> _localizer;
        private readonly TableAllocationService _allocationService;

        public CustomerController(
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

        [AllowAnonymous]
        public async Task<IActionResult> Create()
        {
            var vm = new CustomerCreateViewModel
            {
                Email = TempData["Email"]?.ToString(),
                ApplicationUserId = TempData["UserId"]?.ToString(),
                CreatedBy = TempData["UserId"]?.ToString(),
                UpdatedBy = TempData["UserId"]?.ToString()
            };

            var defaultImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pics", "pfp.jpg");
            if (System.IO.File.Exists(defaultImagePath))
            {
                vm.PictureBytes = await System.IO.File.ReadAllBytesAsync(defaultImagePath);
            }
            else
            {
                vm.PictureBytes = Array.Empty<byte>();
            }

            return View(vm);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerCreateViewModel vm)
        {
            // If a file was uploaded, populate PictureBytes for redisplay even if validation fails
            if (vm.Picture != null && vm.Picture.Length > 0)
            {
                using var msPreview = new MemoryStream();
                await vm.Picture.CopyToAsync(msPreview);
                vm.PictureBytes = msPreview.ToArray();
            }

            if (!ModelState.IsValid)
            {
                // Ensure default picture is set when none uploaded
                if ((vm.PictureBytes == null || vm.PictureBytes.Length == 0))
                {
                    var defaultImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "pics", "pfp.jpg");
                    if (System.IO.File.Exists(defaultImagePath))
                    {
                        vm.PictureBytes = await System.IO.File.ReadAllBytesAsync(defaultImagePath);
                    }
                    else
                    {
                        vm.PictureBytes = Array.Empty<byte>();
                    }
                }

                return View(vm);
            }

            var model = new Customer
            {
                Email = vm.Email ?? TempData["Email"]?.ToString(),
                ApplicationUserId = vm.ApplicationUserId ?? TempData["UserId"]?.ToString(),
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                PhoneNumber = vm.PhoneNumber,
                PreferredLanguage = vm.PreferredLanguage,
                CreatedBy = vm.CreatedBy ?? vm.ApplicationUserId,
                UpdatedBy = vm.UpdatedBy ?? vm.ApplicationUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (vm.Picture != null && vm.Picture.Length > 0)
            {
                using var ms = new MemoryStream();
                await vm.Picture.CopyToAsync(ms);
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

            return RedirectToPage("/Account/ConfirmEmail", new { area = "Identity", email = model.Email });
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

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
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id);

            if (customer == null)
            {
                return RedirectToAction("Create");
            }

            // Load customer's reservations
            var reservations = await _context.Reservations
                .Include(r => r.ReservationStatus)
                .Include(r => r.ReservationTables)
                    .ThenInclude(rt => rt.Table)
                .Where(r => r.CustomerID == customer.CustomerID)
                .OrderByDescending(r => r.ReservedFor)
                .ThenByDescending(r => r.ReservationTime)
                .ToListAsync();

            ViewBag.Reservations = reservations;

            return View(customer);
        }

        public async Task<IActionResult> MakeReservation()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id);

            if (customer == null)
            {
                return RedirectToAction("Create");
            }

            return View(customer);
        }

        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id);

            if (customer == null)
            {
                return RedirectToAction("Create");
            }

            var vm = new CustomerEditViewModel
            {
                CustomerID = customer.CustomerID,
                Email = customer.Email,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                PhoneNumber = customer.PhoneNumber,
                PreferredLanguage = customer.PreferredLanguage,
                PictureBytes = customer.PictureBytes
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(CustomerEditViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerID == vm.CustomerID && c.ApplicationUserId == user.Id);

            if (customer == null)
            {
                TempData["Error"] = "Customer not found.";
                return RedirectToAction("Index");
            }

            // Validate model
            ModelState.Remove("PictureBytes");
            if (!ModelState.IsValid)
            {
                // Attach existing bytes for redisplay
                var reloadedCustomer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerID == vm.CustomerID);
                if (reloadedCustomer != null)
                {
                    vm.PictureBytes = reloadedCustomer.PictureBytes;
                }

                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["Error"] = $"Validation failed: {string.Join(", ", errors)}";
                return View("EditProfile", vm);
            }

            try
            {
                // Update fields
                customer.FirstName = vm.FirstName;
                customer.LastName = vm.LastName;
                customer.PhoneNumber = vm.PhoneNumber;
                customer.PreferredLanguage = vm.PreferredLanguage;
                customer.UpdatedBy = user.Id;
                customer.UpdatedAt = DateTime.UtcNow;

                if (vm.Picture != null && vm.Picture.Length > 0)
                {
                    if (vm.Picture.Length > 5 * 1024 * 1024)
                    {
                        TempData["Error"] = "Profile picture must be less than 5MB.";
                        vm.PictureBytes = customer.PictureBytes;
                        return View("EditProfile", vm);
                    }

                    var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                    if (!allowedTypes.Contains(vm.Picture.ContentType.ToLower()))
                    {
                        TempData["Error"] = "Only image files (JPG, PNG, GIF) are allowed.";
                        vm.PictureBytes = customer.PictureBytes;
                        return View("EditProfile", vm);
                    }

                    using var ms = new MemoryStream();
                    await vm.Picture.CopyToAsync(ms);
                    customer.PictureBytes = ms.ToArray();
                    _context.Entry(customer).Property(c => c.PictureBytes).IsModified = true;
                }

                _context.Entry(customer).Property(c => c.FirstName).IsModified = true;
                _context.Entry(customer).Property(c => c.LastName).IsModified = true;
                _context.Entry(customer).Property(c => c.PhoneNumber).IsModified = true;
                _context.Entry(customer).Property(c => c.PreferredLanguage).IsModified = true;
                _context.Entry(customer).Property(c => c.UpdatedBy).IsModified = true;
                _context.Entry(customer).Property(c => c.UpdatedAt).IsModified = true;

                var saved = await _context.SaveChangesAsync();
                if (saved > 0)
                {
                    TempData["Success"] = "Profile updated successfully!";
                    TempData["ProfileUpdated"] = DateTime.UtcNow.Ticks.ToString();
                }
                else
                {
                    TempData["Success"] = "Profile information verified - no changes needed.";
                }

                return RedirectToAction("Index");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                TempData["Error"] = $"Database concurrency error: {ex.Message}. Please try again.";
                var reloadedCustomer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerID == vm.CustomerID);
                if (reloadedCustomer != null)
                {
                    vm.PictureBytes = reloadedCustomer.PictureBytes;
                }
                return View("EditProfile", vm);
            }
            catch (DbUpdateException ex)
            {
                TempData["Error"] = $"Database error: {ex.InnerException?.Message ?? ex.Message}";
                var reloadedCustomer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerID == vm.CustomerID);
                if (reloadedCustomer != null)
                {
                    vm.PictureBytes = reloadedCustomer.PictureBytes;
                }
                return View("EditProfile", vm);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to update profile: {ex.Message}";
                var reloadedCustomer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerID == vm.CustomerID);
                if (reloadedCustomer != null)
                {
                    vm.PictureBytes = reloadedCustomer.PictureBytes;
                }
                return View("EditProfile", vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reserve(int PartySize, DateTime ReservationDate, TimeSpan ReservationTime, string? Notes, int? Duration)
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

            // Server-side validation
            if (PartySize < 1 || ReservationDate == default || ReservationTime == default)
            {
                TempData["Error"] = _localizer["Please select a valid party size, date and time."].Value;
                return RedirectToAction("MakeReservation");
            }

            var selectedDateTimeUtc = new DateTime(
                ReservationDate.Year, ReservationDate.Month, ReservationDate.Day,
                ReservationTime.Hours, ReservationTime.Minutes, 0, DateTimeKind.Utc);
            if (selectedDateTimeUtc < DateTime.UtcNow)
            {
                TempData["Error"] = _localizer["Date and time cannot be in the past."].Value;
                return RedirectToAction("MakeReservation");
            }

            // Business hours check (10:00 - 22:00)
            var open = new TimeSpan(10, 0, 0);
            var close = new TimeSpan(22, 0, 0);
            if (ReservationTime < open || ReservationTime > close)
            {
                TempData["Error"] = _localizer["Selected time is outside business hours."].Value;
                return RedirectToAction("MakeReservation");
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
                return RedirectToAction("MakeReservation");
            }

            try
            {
                // Create reservation with allocated tables
                var reservation = await _allocationService.CreateReservationAsync(
                    allocation,
                    customer.CustomerID,
                    null,
                    restaurant.RestaurantID,
                    ReservationDate,
                    ReservationTime,
                    effectiveDuration,
                    PartySize,
                    Notes,
                    confirmedStatus.ReservationStatusID,
                    false,
                    user.Id);

                // Confirmation email is sent by the reservation creation workflow (CreateReservationAsync)
                TempData["Message"] = _localizer["Thanks! Your reservation is confirmed."].Value;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                var root = ex.GetBaseException()?.Message ?? ex.Message;
                TempData["Error"] = _localizer["Failed to save reservation: {0}", root].Value;
                return RedirectToAction("MakeReservation");
            }
        }

        // Keep MyReservations for backward compatibility (optional)
        public async Task<IActionResult> MyReservations()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login", new { area = "Identity" });

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id);
            if (customer == null) return RedirectToAction("Create");

            var reservations = await _context.Reservations
                .Include(r => r.ReservationStatus)
                .Include(r => r.ReservationTables).ThenInclude(rt => rt.Table)
                .Where(r => r.CustomerID == customer.CustomerID)
                .OrderByDescending(r => r.ReservedFor).ThenByDescending(r => r.ReservationTime)
                .ToListAsync();

            return View("MyReservations", reservations);
        }

        // Cancel reservation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelReservation(int reservationId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.ApplicationUserId == user.Id);

            if (customer == null)
            {
                TempData["Error"] = "Customer profile not found.";
                return RedirectToAction("Index");
            }

            var reservation = await _context.Reservations
                .Include(r => r.ReservationStatus)
                .FirstOrDefaultAsync(r => r.ReservationID == reservationId && r.CustomerID == customer.CustomerID);

            if (reservation == null)
            {
                TempData["Error"] = "Reservation not found.";
                return RedirectToAction("Index");
            }

            if (reservation.ReservationStatus.StatusName == "Cancelled" || reservation.ReservationStatus.StatusName == "Completed")
            {
                TempData["Error"] = "This reservation cannot be cancelled.";
                return RedirectToAction("Index");
            }

            var cancelledStatus = await _context.ReservationStatuses
                .FirstOrDefaultAsync(s => s.StatusName == "Cancelled");

            if (cancelledStatus != null)
            {
                var oldStatus = reservation.ReservationStatus.StatusName;
                reservation.ReservationStatusID = cancelledStatus.ReservationStatusID;
                reservation.UpdatedBy = user.Id;
                reservation.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await LogReservationAction(reservationId, "StatusChanged", $"From {oldStatus} to Cancelled", user.Id);

                // Send cancellation email to customer
                try
                {
                    if (!string.IsNullOrWhiteSpace(customer.Email))
                    {
                        var subject = _localizer["Your reservation has been cancelled"].Value;
                        var greeting = !string.IsNullOrWhiteSpace(customer.FirstName)
                            ? $"{_localizer["Hello"].Value} {System.Net.WebUtility.HtmlEncode(customer.FirstName)},"
                            : _localizer["Hello"].Value;

                        var body = $@"
                            <h2>{_localizer["Reservation Cancelled"]}</h2>
                            <p>{greeting}</p>
                            <p>{_localizer["Your reservation on"]} {reservation.ReservationDate:yyyy-MM-dd} {_localizer["at"]} {reservation.ReservationTime.ToString(@"hh\:mm") } {_localizer["has been cancelled."]}</p>
                            <p>{_localizer["If you have any questions please contact us."]}</p>";

                        await _emailService.SendEmailAsync(customer.Email, subject, body);
                    }
                }
                catch (Exception ex)
                {
                    // don't block cancellation on email failure
                    var logger = HttpContext.RequestServices.GetService(typeof(ILogger<CustomerController>)) as ILogger;
                    logger?.LogWarning(ex, "Failed to send cancellation email for reservation {ReservationId}", reservationId);
                }

                TempData["Message"] = "Reservation cancelled successfully.";
            }
            else
            {
                TempData["Error"] = "Unable to cancel reservation.";
            }

            return RedirectToAction("Index");
        }

        // Helper method to log reservation actions
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