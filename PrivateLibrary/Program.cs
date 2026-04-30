using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Movie_Library.Components;
using MovieLibraryApp.Services;
using PrivateLibrary.Data;
using PrivateLibrary.Features.Lending.Services;

var builder = WebApplication.CreateBuilder(args);

// Razor Components / Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Bisherige Services
builder.Services.AddHttpClient<TmdbService>();
builder.Services.AddScoped<MovieCsvService>();
builder.Services.AddScoped<MovieFilterService>();
builder.Services.AddScoped<PosterResolverService>();

builder.Services.AddScoped(sp =>
{
    var navigation = sp.GetRequiredService<NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigation.BaseUri)
    };
});

// Neue Services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ILendingService, LendingService>();
builder.Services.AddScoped<IPersonService, PersonService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
