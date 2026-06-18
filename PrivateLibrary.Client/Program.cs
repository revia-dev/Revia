using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PrivateLibrary.Client;
using PrivateLibrary.Client.Auth;

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

await builder.Build().RunAsync();
