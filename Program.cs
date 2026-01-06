using FYP.Data;
using FYP.Models;
using FYP.Services;
using FYP.Localization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// DbContext with transient fault resiliency and optional sensitive data logging in Development
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

// Enable sensitive data logging in Development to help diagnose LINQ/EF issues (do not enable in Production)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ApplicationDbContext>(options => options.EnableSensitiveDataLogging());
}

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie options for "Remember Me" functionality
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(14); // Cookie expires in 14 days
    options.SlidingExpiration = true; // Refresh expiration on each request
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    
    // Cookie will persist only if "Remember Me" is checked
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
    // In development, allow cookies over HTTP if HTTPS certificate issues occur
    // In production, always require HTTPS
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(SharedResource));
    });

// Require authentication by default for all endpoints unless explicitly allowed
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Global exception handling is done via middleware (UseExceptionHandler and status code pages)

builder.Services.AddRazorPages(options =>
{
    // Allow anonymous access to all Identity pages (Login, Register, etc.)
    // This prevents redirect loops when accessing the login page
    options.Conventions.AllowAnonymousToAreaFolder("Identity", "/");
});

// Supported cultures
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en"),
        new CultureInfo("fr")
    };

    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

// Email service
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// Table allocation service
builder.Services.AddScoped<TableAllocationService>();

// Reports DI (on-demand service only)
builder.Services.AddScoped<FYP.Services.Interfaces.IReportService, FYP.Services.ReportService>();
// PDF generator
builder.Services.AddScoped<FYP.Services.Pdf.ReportPdfGenerator>();


var app = builder.Build();

// QuestPDF license
try
{
    QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
}
catch { }

// Middleware
if (!app.Environment.IsDevelopment())
{
    // Redirect unhandled exceptions to our Error page
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
// Also handle status codes (404, 500 etc.) by re-executing to /Error
app.UseStatusCodePagesWithReExecute("/Error");

app.UseHttpsRedirection();
app.UseStaticFiles();

// ⚡ Localization BEFORE routing
var locOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Seed roles and users
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
    
    // Apply pending migrations first
    try
    {
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while applying migrations.");
    }
    
    // Then seed data
    try
    {
        // Seed roles first
        await FYP.Data.RoleSeeder.SeedRolesAsync(serviceProvider);
        
        // Then seed users (from Services folder)
        await FYP.Services.UserSeeder.SeedUsersAsync(serviceProvider);

        // Ensure a dedicated Guest customer exists for guest reservations
        await FYP.Data.GuestCustomerSeeder.EnsureGuestCustomerAsync(serviceProvider);
        
        // Ensure a dedicated Walk-in customer exists for staff walk-ins
        await FYP.Data.WalkInCustomerSeeder.EnsureWalkInCustomerAsync(serviceProvider);
    }
    catch (Exception ex)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();
