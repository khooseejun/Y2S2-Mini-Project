using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RestaurantSystem;
using RestaurantSystem.Models; // Ensure namespace matches

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// Portability: optional per-machine overrides
// ==========================================
// `appsettings.Development.Local.json` is git-ignored. It lets each developer
// keep their own SMTP credentials (see appsettings.Development.Local.json.example)
// without editing the shared appsettings.json. Missing file = silently skipped.
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.Local.json",
    optional: true,
    reloadOnChange: true);

// ==========================================
// Phase 1: Register dependency injection services
// ==========================================
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

AppDomain.CurrentDomain.SetData("DataDirectory", builder.Environment.ContentRootPath);

// 1. Register local database connection (SQL Server Express)
builder.Services.AddDbContext<RestaurantDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Register manual Cookie authentication (Assignment 3.3 security requirement)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";               // Redirect here if not logged in
        options.AccessDeniedPath = "/Account/AccessDenied"; // Page for insufficient permissions
    });

//builder.Services.AddDistributedMemoryCache();
// Register Session service (used to store cart data)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Cart auto-clears after 30 min inactivity
    options.Cookie.HttpOnly = true;   // Cookie security attribute
    options.Cookie.IsEssential = true; // Mark as essential cookie
});

// 4. Register MVC controller and view services
builder.Services.AddControllersWithViews();

// 5. Inject custom security Helper and HttpContext accessor
builder.Services.AddScoped<Helper>();
builder.Services.AddHttpContextAccessor();

// 5b. Order pricing rules (tax %, service charge %, delivery fee) — see Models/Pricing.cs.
//     Bound from "RestaurantSettings" so the numbers live in appsettings.json, not code.
builder.Services.AddSingleton(PricingSettings.FromConfiguration(builder.Configuration));

// 6. Email service — used by Password Recovery (Email) to send real reset links via SMTP
builder.Services.AddScoped<IEmailService, EmailService>();

// ==========================================
// Phase 2: Build app and configure middleware pipeline
// ==========================================
var app = builder.Build(); // App instance created after this line

// ==========================================
// Portability: fail fast with a useful message
// ==========================================
// The database is a LocalDB .mdf file that lives beside the project and is
// attached through |DataDirectory| (set above to the content root). If the
// file is missing — a fresh copy of the project, a wrong working directory,
// or a checkout that didn't include it — every page would otherwise fail with
// a bare HTTP 500 and no explanation. Say exactly what is wrong instead.
{
    var dbPath = Path.Combine(app.Environment.ContentRootPath, "DB.mdf");
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    if (!File.Exists(dbPath))
    {
        logger.LogCritical(
            "Database file not found at '{DbPath}'. The app cannot start without it.\n" +
            "Fixes:\n" +
            "  1. Copy 'DB.mdf' and 'DB_log.ldf' into: {Root}\n" +
            "  2. Confirm no other app/Visual Studio instance has the .mdf file open.\n" +
            "  3. Ensure SQL Server Express LocalDB is installed and running: sqllocaldb start MSSQLLocalDB",
            dbPath, app.Environment.ContentRootPath);

        throw new FileNotFoundException(
            $"Database file not found: {dbPath}. " +
            "Copy DB.mdf and DB_log.ldf beside the project, and make sure LocalDB is running " +
            "(sqllocaldb start MSSQLLocalDB).", dbPath);
    }

    logger.LogInformation("Using database file: {DbPath}", dbPath);
}

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting(); // 1. Enable routing

app.UseSession();  // 2. Enable Session

app.UseAuthentication(); // 3. Authentication
app.UseAuthorization();  // 4. Authorization

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
