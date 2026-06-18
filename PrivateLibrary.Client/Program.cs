using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PrivateLibrary.Client;
using PrivateLibrary.Client.Auth;
using PrivateLibrary.Client.Offline;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

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

// Pilot feature: wishlists (offline-capable).
builder.Services.AddScoped<PrivateLibrary.Client.Features.Wishlists.WishlistClientService>();

var host = builder.Build();

// Bring connectivity + sync online before the first render so cached data and the
// outbox are ready and any queued mutations flush immediately when online.
var connectivity = host.Services.GetRequiredService<ConnectivityService>();
await connectivity.InitializeAsync();

// Eagerly resolve feature services so they register their sync replay handlers
// before the engine performs its first flush.
_ = host.Services.GetRequiredService<PrivateLibrary.Client.Features.Wishlists.WishlistClientService>();

await host.Services.GetRequiredService<SyncEngine>().InitializeAsync();

await host.RunAsync();
