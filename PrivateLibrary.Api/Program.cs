// PrivateLibrary.Api — hosts the Blazor WebAssembly client and exposes the REST API.
// It reuses the existing data + identity layer (AppDbContext, ApplicationUser,
// HouseholdClaimsPrincipalFactory) from the PrivateLibrary project, so behaviour
// matches the running Blazor Server app. Auth is token-based (JWT) for the WASM
// client; the Server app keeps its own cookie auth and is unaffected.

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PrivateLibrary.Api.Auth;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Lending.Services;
using PrivateLibrary.Modules.Wishlists;
using PrivateLibrary.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ---- Data + Identity (reused from the PrivateLibrary project) ----
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;

        // Lockout to throttle password brute-forcing (honoured by AuthController).
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<HouseholdClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

// ---- JWT auth ----
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    // Dev fallback so the app runs out of the box; override via config/secret in prod.
    jwt.SigningKey = "dev-only-insecure-signing-key-change-me-32+chars-please!!";
}
builder.Services.AddSingleton(jwt);
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// Bridge HttpContext.User into the AuthenticationStateProvider abstraction so the
// reused household-scoped services (via CurrentUserContext) run unchanged here.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticationStateProvider, HttpContextAuthStateProvider>();
builder.Services.AddScoped<CurrentUserContext>();

// Reused domain services.
builder.Services.AddScoped<WishlistService>();
builder.Services.AddScoped<MovieService>();
builder.Services.AddScoped<PrivateLibrary.Modules.Bmi.BmiService>();
builder.Services.AddSingleton<PrivateLibrary.Modules.Bmi.IBmiPhotoStorage, PrivateLibrary.Modules.Bmi.BmiPhotoStorage>();
builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddSingleton<IPersonPhotoStorage, PersonPhotoStorage>();
builder.Services.AddScoped<TvGuideService>();
builder.Services.AddScoped<PrivateLibrary.Modules.Games.GameService>();
builder.Services.AddSingleton<PrivateLibrary.Modules.Games.IGameFileStorage, PrivateLibrary.Modules.Games.GameFileStorage>();
builder.Services.AddScoped<ILendingService, LendingService>();
builder.Services.AddSingleton<ILendingPhotoStorage, LendingPhotoStorage>();
builder.Services.AddScoped<HouseholdLinkService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<ModuleTileLayoutService>();
builder.Services.AddScoped<DashboardLayoutService>();

var app = builder.Build();

// Startup: apply migrations and seed roles + the default admin so a fresh database
// (e.g. a new Docker volume) works out of the box without manual steps. Then, if
// demo mode is on, seed/reset the demo household for the credential-free demo.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<AppDbContext>();
    var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

    await db.Database.MigrateAsync();
    await IdentitySeeder.SeedAsync(userManager, roleManager, db, app.Configuration);

    if (app.Configuration.GetValue<bool>("Demo:Enabled"))
    {
        // Reset the demo data on each start by default, so anything a visitor changed
        // is discarded; set Demo:ResetOnStartup=false to keep it between restarts.
        var resetDemo = app.Configuration.GetValue("Demo:ResetOnStartup", true);
        await DemoSeeder.SeedAsync(userManager, roleManager, db, reset: resetDemo);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
