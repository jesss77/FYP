using FYP.Data;
using FYP.Models;

public static class WalkInCustomerSeeder
{
    private const string WalkInEmail = "walkin@system.local";

    public static async Task<int> EnsureWalkInCustomerAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        var walkIn = context.Customers.FirstOrDefault(c => c.Email == WalkInEmail);
        if (walkIn != null)
        {
            return walkIn.CustomerID;
        }

        walkIn = new Customer
        {
            Email = WalkInEmail,
            FirstName = "Walk",
            LastName = "In",
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

        context.Customers.Add(walkIn);
        await context.SaveChangesAsync();
        return walkIn.CustomerID;
    }
}



