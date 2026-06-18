using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PrivateLibrary.Client;
using PrivateLibrary.Client.Auth;
using PrivateLibrary.Client.Offline;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Localization: UI text comes from .resx (German default, English fallback). The
// active culture follows the browser; resources resolve via IStringLocalizer.
builder.Services.AddLocalization();

var apiBase = new Uri(builder.HostEnvironment.BaseAddress);

// Auth plumbing.
builder.Services.AddScoped<AuthTokenStore>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthService>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddTransient<AuthMessageHandler>();

// "auth" client talks to the auth endpoints without a token; "api" client carries
// the bearer token (and refreshes it) for all data calls.
builder.Services.AddHttpClient("auth", c => c.BaseAddress = apiBase);
builder.Services.AddHttpClient("api", c => c.BaseAddress = apiBase)
    .AddHttpMessageHandler<AuthMessageHandler>();

// Default injected HttpClient = the authenticated "api" client.
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("api"));

// Offline core (IndexedDB cache, outbox, connectivity, sync engine).
builder.Services.AddScoped<OfflineStore>();
builder.Services.AddScoped<ConnectivityService>();
builder.Services.AddScoped<SyncEngine>();

// Offline-capable features.
builder.Services.AddScoped<PrivateLibrary.Client.Features.Wishlists.WishlistClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Movies.MovieClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Bmi.BmiClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Tv.TvClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Games.GameClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Games.GameMatchClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Lending.LendingClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Dashboard.DashboardClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Analytics.AnalyticsClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Access.AccessClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Access.LayoutClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Access.AdminPermissionsClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Media.PhotoCacheService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Games.GameFilesClientService>();
builder.Services.AddScoped<PrivateLibrary.Client.Features.Persons.PersonsClientService>();

var host = builder.Build();

// Bring connectivity + sync online before the first render so cached data and the
// outbox are ready and any queued mutations flush immediately when online.
var connectivity = host.Services.GetRequiredService<ConnectivityService>();
await connectivity.InitializeAsync();

// Eagerly resolve feature services so they register their sync replay handlers
// before the engine performs its first flush.
_ = host.Services.GetRequiredService<PrivateLibrary.Client.Features.Wishlists.WishlistClientService>();
_ = host.Services.GetRequiredService<PrivateLibrary.Client.Features.Movies.MovieClientService>();
_ = host.Services.GetRequiredService<PrivateLibrary.Client.Features.Bmi.BmiClientService>();
_ = host.Services.GetRequiredService<PrivateLibrary.Client.Features.Tv.TvClientService>();
_ = host.Services.GetRequiredService<PrivateLibrary.Client.Features.Games.GameClientService>();
_ = host.Services.GetRequiredService<PrivateLibrary.Client.Features.Games.GameMatchClientService>();
_ = host.Services.GetRequiredService<PrivateLibrary.Client.Features.Lending.LendingClientService>();

await host.Services.GetRequiredService<SyncEngine>().InitializeAsync();

// Resolve the current user's effective module access up front so the nav, tiles and
// write actions can gate themselves on first render (cached for offline).
await host.Services.GetRequiredService<PrivateLibrary.Client.Features.Access.AccessClientService>()
    .GetMyAccessAsync();

await host.RunAsync();
