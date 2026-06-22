// Revia.Api — hosts the Blazor WebAssembly client and exposes the REST API.
// It reuses the existing data + identity layer (AppDbContext, ApplicationUser,
// HouseholdClaimsPrincipalFactory) from the Revia project, so behaviour
// matches the running Blazor Server app. Auth is token-based (JWT) for the WASM
// client; the Server app keeps its own cookie auth and is unaffected.

using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Revia.Api.Auth;
using Revia.Api.Infrastructure;
using Revia.Data;
using Revia.Modules.Lending.Services;
using Revia.Modules.Wishlists;
using Revia.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ---- Observability ----
// Structured request logging so every request is traceable (method, path, status,
// duration). Tuned to skip the noisy reachability probe and the static WASM files.
builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode
        | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration;
    o.CombineLogs = true;
});

// ---- Distributed tracing + metrics (OpenTelemetry) ----
builder.Services.AddReviaTelemetry(builder.Configuration);

// ---- Consistent error responses (RFC 7807 ProblemDetails) ----
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---- API documentation ----
builder.Services.AddOpenApi();

// ---- Health checks ----
// "live" = process is up; the DB check is tagged "ready" so a load balancer can
// distinguish "started" from "can actually serve requests".
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

// ---- Rate limiting ----
// A tight fixed window on the auth endpoints throttles credential stuffing /
// brute force at the edge, on top of Identity's per-account lockout.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 10;
        o.QueueLimit = 0;
    });
});

// ---- Data + Identity (reused from the Revia project) ----
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
StartupValidation.ValidateAndHardenJwt(jwt, builder.Environment, builder.Configuration);
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
builder.Services.AddScoped<Revia.Modules.Bmi.BmiService>();
builder.Services.AddSingleton<Revia.Modules.Bmi.IBmiPhotoStorage, Revia.Modules.Bmi.BmiPhotoStorage>();
builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddSingleton<IPersonPhotoStorage, PersonPhotoStorage>();
builder.Services.AddScoped<TvGuideService>();
builder.Services.AddScoped<Revia.Modules.Games.GameService>();
builder.Services.AddSingleton<Revia.Modules.Games.IGameFileStorage, Revia.Modules.Games.GameFileStorage>();
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
    // Named-category logger (not ILogger<Program>): the referenced legacy Revia
    // project also defines a top-level Program type, so the generic form is ambiguous.
    var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Revia.Api.Startup");
    try
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

        if (StartupValidation.UsesDefaultAdminPassword(app.Environment, app.Configuration))
        {
            log.LogWarning(
                "Seed:AdminPassword is still the shipped default in a non-Development " +
                "environment. Set a unique admin password via Seed__AdminPassword.");
        }

        log.LogInformation("Applying migrations and seeding identity…");
        await db.Database.MigrateAsync();
        await IdentitySeeder.SeedAsync(userManager, roleManager, db, app.Configuration);

        if (app.Configuration.GetValue<bool>("Demo:Enabled"))
        {
            // Reset the demo data on each start by default, so anything a visitor changed
            // is discarded; set Demo:ResetOnStartup=false to keep it between restarts.
            var resetDemo = app.Configuration.GetValue("Demo:ResetOnStartup", true);
            log.LogInformation("Demo mode enabled (resetOnStartup={Reset}); seeding demo data…", resetDemo);
            await DemoSeeder.SeedAsync(userManager, roleManager, db, reset: resetDemo);
        }
        log.LogInformation("Startup migration + seed complete.");
    }
    catch (Exception ex)
    {
        // A failed migration/seed means the app can't serve correctly — fail fast and
        // loud rather than starting in a half-initialised state.
        log.LogCritical(ex, "Database migration/seed failed during startup.");
        throw;
    }
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSecurityHeaders();
app.UseHttpLogging();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.MapOpenApi();
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health endpoints: /health is a cheap liveness probe; /health/ready also verifies
// the database is reachable (tagged "ready") so orchestrators gate traffic on it.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false // liveness only: don't run the DB check
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Lightweight reachability probe used by the PWA client to distinguish
// "device has WiFi" from "our server is actually up". Returns 200 immediately.
app.MapMethods("/api/ping", ["HEAD", "GET"], () => Results.Ok());

app.MapFallbackToFile("index.html");

app.Run();
