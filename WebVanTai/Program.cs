using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.IO;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

// Cấu hình DataProtection lưu khóa vào thư mục cố định để không bị văng session khi khởi động lại container
var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "temp-keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
    .SetApplicationName("WebVanTai");

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultUI()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // Bắt buộc cho ASP.NET Core Identity UI
builder.Services.AddHostedService<SeatLockCleanupService>();

// ── Payment, Email & extended-feature services ───────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<VnPayService>();
builder.Services.AddScoped<EmailService>();

var app = builder.Build();

// Seed and Migrate initial data with retry logic for SQL Server container
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<ApplicationDbContext>();

    int maxRetries = 5;
    int retryDelaySeconds = 5;
    for (int retry = 1; retry <= maxRetries; retry++)
    {
        try
        {
            logger.LogInformation("Attempting database migration and seeding (attempt {Attempt}/{MaxAttempts})...", retry, maxRetries);
            await context.Database.MigrateAsync();
            await DbInitializer.SeedAsync(services);
            await ExtraDataSeeder.SeedAsync(services); // Seed role TicketManager + tổ lái (ngoài vùng Data/)
            logger.LogInformation("Database migration and seed completed successfully.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to connect to database on attempt {Attempt}/{MaxAttempts}. Retrying in {Delay} seconds...", retry, maxRetries, retryDelaySeconds);
            if (retry == maxRetries)
            {
                logger.LogError(ex, "An error occurred while migrating/seeding the database after {MaxAttempts} attempts.", maxRetries);
                throw;
            }
            await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
        }
    }
}

if (args.Contains("--seed-only"))
{
    Console.WriteLine("Database seed completed successfully.");
    return;
}

// Configure the HTTP request pipeline.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                       Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
                       Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Razor Pages (Identity UI)
app.MapRazorPages();

// Area routing
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// Default routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
