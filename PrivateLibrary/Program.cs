using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Components;
using PrivateLibrary.Components.Account;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Lending.Services;
using PrivateLibrary.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllersWithViews();

// Supplies the Task<AuthenticationState> cascading value across the whole
// component tree (replaces wrapping App.razor in <CascadingAuthenticationState>).
builder.Services.AddCascadingAuthenticationState();

// Without an explicit server AuthenticationStateProvider the interactive circuit
// reports the user as anonymous (even after a successful SSR render), which makes
// every authorized page bounce back to the login screen. This revalidating
// provider carries the authenticated principal into the circuit and re-checks the
// security stamp so sign-out / password changes invalidate live circuits.
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddHttpClient<TmdbService>();
builder.Services.AddHttpClient<MovieCsvService>();
builder.Services.AddScoped<MovieFilterService>();
builder.Services.AddScoped<MovieService>();
builder.Services.AddScoped<TvGuideService>();
builder.Services.AddScoped<PosterResolverService>();
builder.Services.AddHostedService<PosterBackfillService>();

builder.Services.AddScoped(sp =>
{
    var navigation = sp.GetRequiredService<NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigation.BaseUri)
    };
});

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
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddClaimsPrincipalFactory<HouseholdClaimsPrincipalFactory>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Override the Identity default LoginPath (/Account/Login) so the auth middleware
// redirects unauthenticated users to our custom login page instead of a 404.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/login";
});

builder.Services.AddSingleton<ILendingPhotoStorage, LendingPhotoStorage>();
builder.Services.AddSingleton<IPersonPhotoStorage, PersonPhotoStorage>();
builder.Services.AddTransient<ILendingService, LendingService>();
builder.Services.AddTransient<IPersonService, PersonService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddScoped<HouseholdLinkService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

    // Ensure the DB directory exists (important when using Azure /home/data volume)
    var connStr = config.GetConnectionString("DefaultConnection") ?? string.Empty;
    var dbPath = connStr.Replace("Data Source=", string.Empty).Trim();
    var dbDir = Path.GetDirectoryName(Path.IsPathRooted(dbPath) ? dbPath : Path.Combine(env.ContentRootPath, dbPath));
    if (!string.IsNullOrWhiteSpace(dbDir))
        Directory.CreateDirectory(dbDir);

    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    await IdentitySeeder.SeedAsync(userManager, roleManager, dbContext, config);

    // One-time import of the legacy movies.csv into the Movies table.
    var movieCsvService = scope.ServiceProvider.GetRequiredService<MovieCsvService>();
    var movieLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MovieSeeder");
    await MovieSeeder.ImportCsvIfEmptyAsync(dbContext, movieCsvService, movieLogger);

    // Bootstrap movies.csv to writable location if configured and not yet present
    var writableCsvPath = config["Movies:CsvPath"];
    if (!string.IsNullOrWhiteSpace(writableCsvPath) && !File.Exists(writableCsvPath))
    {
        var sourceCsv = Path.Combine(env.WebRootPath, "data", "movies.csv");
        if (File.Exists(sourceCsv))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(writableCsvPath)!);
            File.Copy(sourceCsv, writableCsvPath);
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Only force HTTPS outside development. In dev (incl. LAN access from a phone)
// the app is served over plain HTTP, so redirecting would bounce devices to the
// untrusted localhost dev certificate.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/images", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=604800, immutable";
        }
    }
});

// Static assets (CSS, JS, service worker, manifest, images) must bypass the global
// RequireAuthenticatedUser fallback policy — otherwise the unauthenticated login
// page is served without any styling or Blazor script.
app.MapStaticAssets().AllowAnonymous();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
