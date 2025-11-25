# Feature Map - FYP Application

This document maps features to implementation files, controllers, and key methods/view models.

## Authentication & User Management
- Login: `Areas/Identity/Pages/Account/Login.cshtml` and `Login.cshtml.cs` - uses `SignInManager<ApplicationUser>.PasswordSignInAsync`
- Logout: `Areas/Identity/Pages/Account/Logout.cshtml.cs` - `SignInManager.SignOutAsync()` and `HttpContext.SignOutAsync(...)`
- User seeding: `Services/UserSeeder.cs` and `Data/RoleSeeder.cs`

## Settings
- Model: `Models/Settings.cs`
- Storage: `Data/ApplicationDbContext.cs` DbSet `Settings`
- Seed: `ApplicationDbContext.OnModelCreating()` seeds default setting keys
- Admin UI: `Controllers/AdminController.cs` - `Settings()`, `EditSetting(id)`, `EditSetting POST` (only updates `Value`)
- Admin Views: `Views/Admin/Settings.cshtml` (cards grid) and `Views/Admin/EditSetting.cshtml` (value-only editing)
- Styling: `wwwroot/css/admin.css`

## Footer
- View: `Views/Shared/_Footer.cshtml` - reads `Settings` from DB, shows `Opening Hours`, `Phone`, `BrandName` using `ApplicationDbContext`

## Employees
- Model: `Models/Employee.cs`
- Admin CRUD: `Controllers/AdminController.cs` methods `Employees()`, `CreateEmployee()`, `EditEmployee()`, `DeleteEmployee()`
- Views: `Views/Admin/Employees.cshtml`, partials

## Reservations
- Controllers: `Controllers/GuestController.cs`, `Controllers/CustomerController.cs`, `Controllers/EmployeeController.cs`
- Models: `Reservation`, `ReservationTables`, `ReservationStatus` (in `Models` folder)

## Static assets & Styling
- Core site styles: `wwwroot/css/site.css`
- Admin/page-specific styles: `wwwroot/css/admin.css` (moved inlined Razor CSS here)
- Header/Footer styles: `wwwroot/css/Header-Footer.css`
- Theme variables: `wwwroot/css/theme-variables.css`

## View Components / Partials
- Header: `Views/Shared/_Header.cshtml`
- Footer: `Views/Shared/_Footer.cshtml`
- Login partial: `Views/Shared/_LoginPartial.cshtml`

## Notes & Best Practices Applied
- Moved inline Razor CSS to `wwwroot/css/admin.css` for maintainability and caching
- Kept Bootstrap grid usage and ensured `bootstrap.min.css` is included in `_Layout` before site CSS
- Edit Setting view shows `Key` as plain text and server ignores `Key` on POST to enforce immutability

