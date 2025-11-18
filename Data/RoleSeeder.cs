using FYP.Constants;
using Microsoft.AspNetCore.Identity;

namespace FYP.Data
{
    public static class RoleSeeder
    {
        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            if (!await roleManager.RoleExistsAsync(Roles.Admin))
                await roleManager.CreateAsync(new IdentityRole(Roles.Admin));

            if (!await roleManager.RoleExistsAsync(Roles.Manager))
                await roleManager.CreateAsync(new IdentityRole(Roles.Manager));

            if (!await roleManager.RoleExistsAsync(Roles.Employee))
                await roleManager.CreateAsync(new IdentityRole(Roles.Employee));

            if (!await roleManager.RoleExistsAsync(Roles.Customer))
                await roleManager.CreateAsync(new IdentityRole(Roles.Customer));
        }
    }
}
