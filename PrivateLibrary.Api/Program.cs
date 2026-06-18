// PrivateLibrary.Api — hosts the Blazor WebAssembly client and (from Phase 2 on)
// exposes the REST API the client talks to. During Phase 1 this only serves the
// WASM app shell so the new architecture can build and run alongside the existing
// Blazor Server app, which remains untouched.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

// Serve the Blazor WASM client's static assets and index.html.
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();

// SPA fallback to the WASM client's index.html for client-side routes.
app.MapFallbackToFile("index.html");

app.Run();
