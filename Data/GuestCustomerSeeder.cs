using FYP.Data;
using FYP.Models;

namespace FYP.Data
{
    public static class GuestCustomerSeeder
    {
        private const string GuestEmail = "guest@system.local";

        public static async Task<int> EnsureGuestCustomerAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<ApplicationDbContext>();

            var guest = context.Customers.FirstOrDefault(c => c.Email == GuestEmail);
            if (guest != null)
            {
                return guest.CustomerID;
            }

            guest = new Customer
            {
                Email = GuestEmail,
                FirstName = "Guest",
                LastName = "Reservation",
                PhoneNumber = null,
                PreferredLanguage = "English",
                IsActive = true,
                PictureBytes = Array.Empty<byte>(),
                ApplicationUserId = null,
                CreatedBy = "system",
                UpdatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Customers.Add(guest);
            await context.SaveChangesAsync();
            return guest.CustomerID;
        }
    }
}


