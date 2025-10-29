using FYP.Models;
using Microsoft.AspNetCore.Identity;
using FYP.Constants;
public static class UserSeeder
{
    public static async Task SeedUsersAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Admin
        var adminEmail = "admin@mail.com";
        var adminUser = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            await userManager.CreateAsync(adminUser, "Admin@123");
            await userManager.AddToRoleAsync(adminUser, Roles.Admin);
        }

        // Managers
        var managerEmails = new[] { "manager1@mail.com", "manager2@mail.com" };
        foreach (var email in managerEmails)
        {
            var managerUser = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            if (await userManager.FindByEmailAsync(email) == null)
            {
                await userManager.CreateAsync(managerUser, "Manager@123");
                await userManager.AddToRoleAsync(managerUser, Roles.Manager);
            }
        }

        // Customers
        var userEmails = new[] { "user1@mail.com", "user2@mail.com", "user3@mail.com" };
        foreach (var email in userEmails)
        {
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            if (await userManager.FindByEmailAsync(email) == null)
            {
                await userManager.CreateAsync(user, "User@123");
                await userManager.AddToRoleAsync(user, Roles.Customer);
            }
        }

        // Employees
        var employeeEmails = new[] { "employee1@mail.com", "employee2@mail.com", "employee3@mail.com" };
        foreach (var email in employeeEmails)
        {
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            if (await userManager.FindByEmailAsync(email) == null)
            {
                await userManager.CreateAsync(user, "Employee@123");
                await userManager.AddToRoleAsync(user, Roles.Employee);
            }
        }
    }
}
