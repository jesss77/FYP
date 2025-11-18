using Microsoft.AspNetCore.Identity;
using FYP.Models;
using FYP.Data;
using Microsoft.Extensions.Logging;

namespace FYP.Services
{
    public static class UserSeeder
    {
        public static async Task SeedUsersAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Starting user seeding...");

            // Ensure restaurant exists first
            var restaurant = await context.Restaurants.FindAsync(1);
            if (restaurant == null)
            {
                logger.LogWarning("Restaurant with ID 1 not found. Cannot seed employees without restaurant.");
                return;
            }

            // Seed Admin User
            await SeedAdminAsync(userManager, context, logger);

            // Seed Manager User
            await SeedManagerAsync(userManager, context, logger);

            // Seed Employee User
            await SeedEmployeeAsync(userManager, context, logger);

            // Seed Customer User
            await SeedCustomerAsync(userManager, context, logger);

            logger.LogInformation("User seeding completed.");
        }

        private static async Task SeedAdminAsync(UserManager<ApplicationUser> userManager, ApplicationDbContext context, ILogger logger)
        {
            var adminEmail = "admin@finodine.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                logger.LogInformation("Creating admin user: {Email}", adminEmail);

                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    logger.LogInformation("Admin user created successfully.");
                }
                else
                {
                    logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogInformation("Admin user already exists.");
            }
        }

        private static async Task SeedManagerAsync(UserManager<ApplicationUser> userManager, ApplicationDbContext context, ILogger logger)
        {
            var managerEmail = "manager@finodine.com";
            var managerUser = await userManager.FindByEmailAsync(managerEmail);

            if (managerUser == null)
            {
                logger.LogInformation("Creating manager user and employee record: {Email}", managerEmail);

                // Create Employee record first
                var employee = new Employee
                {
                    Email = managerEmail,
                    FirstName = "Manager",
                    LastName = "User",
                    RestaurantID = 1,
                    PhoneNumber = "+1 555 0001",
                    IsActive = true,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Employees.Add(employee);
                await context.SaveChangesAsync();
                logger.LogInformation("Manager employee record created with ID: {EmployeeID}", employee.EmployeeID);

                // Create ApplicationUser linked to Employee
                managerUser = new ApplicationUser
                {
                    UserName = managerEmail,
                    Email = managerEmail,
                    EmailConfirmed = true,
                    EmployeeID = employee.EmployeeID
                };

                var result = await userManager.CreateAsync(managerUser, "Manager@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(managerUser, "Manager");

                    // Update Employee with ApplicationUserId
                    employee.ApplicationUserId = managerUser.Id;
                    context.Employees.Update(employee);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Manager user and employee linked successfully.");
                }
                else
                {
                    logger.LogError("Failed to create manager user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogInformation("Manager user already exists.");
            }
        }

        private static async Task SeedEmployeeAsync(UserManager<ApplicationUser> userManager, ApplicationDbContext context, ILogger logger)
        {
            var employeeEmail = "employee@finodine.com";
            var employeeUser = await userManager.FindByEmailAsync(employeeEmail);

            if (employeeUser == null)
            {
                logger.LogInformation("Creating employee user and employee record: {Email}", employeeEmail);

                // Create Employee record first
                var employee = new Employee
                {
                    Email = employeeEmail,
                    FirstName = "Employee",
                    LastName = "User",
                    RestaurantID = 1,
                    PhoneNumber = "+1 555 0002",
                    IsActive = true,
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Employees.Add(employee);
                await context.SaveChangesAsync();
                logger.LogInformation("Employee record created with ID: {EmployeeID}", employee.EmployeeID);

                // Create ApplicationUser linked to Employee
                employeeUser = new ApplicationUser
                {
                    UserName = employeeEmail,
                    Email = employeeEmail,
                    EmailConfirmed = true,
                    EmployeeID = employee.EmployeeID
                };

                var result = await userManager.CreateAsync(employeeUser, "Employee@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(employeeUser, "Employee");

                    // Update Employee with ApplicationUserId
                    employee.ApplicationUserId = employeeUser.Id;
                    context.Employees.Update(employee);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Employee user and employee linked successfully.");
                }
                else
                {
                    logger.LogError("Failed to create employee user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogInformation("Employee user already exists.");
            }
        }

        private static async Task SeedCustomerAsync(UserManager<ApplicationUser> userManager, ApplicationDbContext context, ILogger logger)
        {
            var customerEmail = "customer@example.com";
            var customerUser = await userManager.FindByEmailAsync(customerEmail);

            if (customerUser == null)
            {
                logger.LogInformation("Creating customer user and customer record: {Email}", customerEmail);

                // Create Customer record first
                var customer = new Customer
                {
                    Email = customerEmail,
                    FirstName = "Customer",
                    LastName = "User",
                    PhoneNumber = "+1 555 0003",
                    PreferredLanguage = "en",
                    IsActive = true,
                    PictureBytes = Array.Empty<byte>(),
                    CreatedBy = "system",
                    UpdatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                logger.LogInformation("Customer record created with ID: {CustomerID}", customer.CustomerID);

                // Create ApplicationUser linked to Customer
                customerUser = new ApplicationUser
                {
                    UserName = customerEmail,
                    Email = customerEmail,
                    EmailConfirmed = true,
                    CustomerID = customer.CustomerID
                };

                var result = await userManager.CreateAsync(customerUser, "Customer@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(customerUser, "Customer");

                    // Update Customer with ApplicationUserId
                    customer.ApplicationUserId = customerUser.Id;
                    context.Customers.Update(customer);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Customer user and customer linked successfully.");
                }
                else
                {
                    logger.LogError("Failed to create customer user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogInformation("Customer user already exists.");
            }
        }
    }
}
