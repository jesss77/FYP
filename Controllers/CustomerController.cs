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
        private readonly TableAllocationService _allocationService;

        public CustomerController(
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

            // Redirect to ConfirmEmail page with email parameter (for customers)
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
                .OrderByDescending(r => r.ReservationDate)
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

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(Customer model, IFormFile? picture)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerID == model.CustomerID && c.ApplicationUserId == user.Id);

            if (customer == null)
            {
                TempData["Error"] = "Customer not found.";
                return RedirectToAction("Index");
            }

            // Remove fields from validation that we'll set programmatically
            ModelState.Remove("ApplicationUser");
            ModelState.Remove("PictureBytes");
            ModelState.Remove("Email");
            ModelState.Remove("UpdatedBy");
            ModelState.Remove("UpdatedAt");

            if (!ModelState.IsValid)
            {
                // Log validation errors
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["Error"] = $"Validation failed: {string.Join(", ", errors)}";
                
                // Reload customer with picture data for redisplay
                var reloadedCustomer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerID == model.CustomerID);
                
                if (reloadedCustomer != null)
                {
                    model.PictureBytes = reloadedCustomer.PictureBytes;
                }
                
                return View("EditProfile", model);
            }

            try
            {
                // Update customer details
                customer.FirstName = model.FirstName;
                customer.LastName = model.LastName;
                customer.PhoneNumber = model.PhoneNumber;
                customer.PreferredLanguage = model.PreferredLanguage;
                customer.UpdatedBy = user.Id;
                customer.UpdatedAt = DateTime.UtcNow;

                // Handle profile picture update
                if (picture != null && picture.Length > 0)
                {
                    // Validate file size (5MB max)
                    if (picture.Length > 5 * 1024 * 1024)
                    {
                        TempData["Error"] = "Profile picture must be less than 5MB.";
                        
                        // Reload customer with picture data
                        var reloadedCustomer = await _context.Customers
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.CustomerID == model.CustomerID);
                        if (reloadedCustomer != null)
                        {
                            customer.PictureBytes = reloadedCustomer.PictureBytes;
                        }
                        
                        return View("EditProfile", customer);
                    }

                    // Validate file type
                    var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                    if (!allowedTypes.Contains(picture.ContentType.ToLower()))
                    {
                        TempData["Error"] = "Only image files (JPG, PNG, GIF) are allowed.";
                        
                        // Reload customer with picture data
                        var reloadedCustomer = await _context.Customers
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.CustomerID == model.CustomerID);
                        if (reloadedCustomer != null)
                        {
                            customer.PictureBytes = reloadedCustomer.PictureBytes;
                        }
                        
                        return View("EditProfile", customer);
                    }

                    using var ms = new MemoryStream();
                    await picture.CopyToAsync(ms);
                    customer.PictureBytes = ms.ToArray();
                }
                // If no new picture uploaded, keep the existing picture
                // Don't modify PictureBytes if picture is null

                // Mark only the properties we want to update
                _context.Entry(customer).Property(c => c.FirstName).IsModified = true;
                _context.Entry(customer).Property(c => c.LastName).IsModified = true;
                _context.Entry(customer).Property(c => c.PhoneNumber).IsModified = true;
                _context.Entry(customer).Property(c => c.PreferredLanguage).IsModified = true;
                _context.Entry(customer).Property(c => c.UpdatedBy).IsModified = true;
                _context.Entry(customer).Property(c => c.UpdatedAt).IsModified = true;
                
                if (picture != null && picture.Length > 0)
                {
                    _context.Entry(customer).Property(c => c.PictureBytes).IsModified = true;
                }
                
                // Save changes
                var savedCount = await _context.SaveChangesAsync();
                
                if (savedCount > 0)
                {
                    TempData["Success"] = "Profile updated successfully!";
                    TempData["ProfileUpdated"] = DateTime.UtcNow.Ticks.ToString(); // Cache buster
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
                
                // Reload customer with picture data
                var reloadedCustomer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerID == model.CustomerID);
                if (reloadedCustomer != null)
                {
                    customer.PictureBytes = reloadedCustomer.PictureBytes;
                }
                
                return View("EditProfile", customer);
            }
            catch (DbUpdateException ex)
            {
                TempData["Error"] = $"Database error: {ex.InnerException?.Message ?? ex.Message}";
                
                // Reload customer with picture data
                var reloadedCustomer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerID == model.CustomerID);
                if (reloadedCustomer != null)
                {
                    customer.PictureBytes = reloadedCustomer.PictureBytes;
                }
                
                return View("EditProfile", customer);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to update profile: {ex.Message}";
                
                // Reload customer with picture data
                var reloadedCustomer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerID == model.CustomerID);
                if (reloadedCustomer != null)
                {
                    customer.PictureBytes = reloadedCustomer.PictureBytes;
                }
                
                return View("EditProfile", customer);
            }
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

            // Server-side validation
            if (PartySize < 1)
            {
                TempData["Error"] = "Please select a valid party size.";
                return RedirectToAction("MakeReservation");
            }
            if (ReservationDate == default || ReservationTime == default)
            {
                TempData["Error"] = "Please select a valid date and time.";
                return RedirectToAction("MakeReservation");
            }
            var selectedDateTimeUtc = new DateTime(
                ReservationDate.Year, ReservationDate.Month, ReservationDate.Day,
                ReservationTime.Hours, ReservationTime.Minutes, 0, DateTimeKind.Utc);
            if (selectedDateTimeUtc < DateTime.UtcNow)
            {
                TempData["Error"] = "Date and time cannot be in the past.";
                return RedirectToAction("MakeReservation");
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

            var effectiveNotes = Notes;
            if (!string.IsNullOrWhiteSpace(ReservationName))
            {
                effectiveNotes = string.IsNullOrWhiteSpace(Notes)
                    ? $"Reservation Name: {ReservationName}"
                    : $"Reservation Name: {ReservationName} | {Notes}";
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
                TempData["Error"] = allocation.ErrorMessage;
                return RedirectToAction("MakeReservation");
            }

            try
            {
                // Create reservation with allocated tables
                var reservation = await _allocationService.CreateReservationAsync(
                    allocation,
                    customer.CustomerID,
                    null,
                    false,
                    restaurant.RestaurantID,
                    ReservationDate,
                    ReservationTime,
                    effectiveDuration,
                    PartySize,
                    effectiveNotes,
                    pendingStatus.ReservationStatusID,
                    false,
                    user.Id);

                TempData["Message"] = $"Reservation submitted! {allocation.AllocationStrategy}. We'll confirm shortly.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                var root = ex.GetBaseException()?.Message ?? ex.Message;
                TempData["Error"] = $"Failed to save reservation: {root}";
                return RedirectToAction("MakeReservation");
            }
        }

        // Keep MyReservations for backward compatibility (optional)
        public async Task<IActionResult> MyReservations()
        {
            return RedirectToAction("Index");
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