using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using PrivateLibrary.Components;
using PrivateLibrary.Services;
using PrivateLibrary.Data;
using PrivateLibrary.Modules.Lending.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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

// Neue EF-Core-/SQLite-Integration
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Neue Fach-Services
builder.Services.AddTransient<ILendingService, LendingService>();
builder.Services.AddTransient<IPersonService, PersonService>();
builder.Services.AddScoped<PrivateLibrary.Modules.Lending.Services.ToastService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
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
