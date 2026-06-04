using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Infrastructure;
using GestionArchivosEscaneados.Models.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddGestionArchivosInfrastructure(builder.Configuration);
builder.Services.AddGestionArchivosApplication();

var sessionSettings = builder.Configuration.GetSection("Session").Get<SessionSettings>() ?? new SessionSettings();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(Math.Max(5, sessionSettings.TimeoutMinutes));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();
