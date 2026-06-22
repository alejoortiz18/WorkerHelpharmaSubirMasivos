using Microsoft.Extensions.FileProviders;
using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Infrastructure;
using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using GestionArchivosEscaneados.Models.Settings;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configuration.AddUserSecrets<Program>(optional: true);

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

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var rutas = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RutasSettings>>().Value;
    var unc = scope.ServiceProvider.GetRequiredService<GestionArchivosEscaneados.Infrastructure.Unc.UncConexionService>();
    var trazabilidad = scope.ServiceProvider.GetRequiredService<ITrazabilidadConsultaSqlService>();
    var accesible = unc.AsegurarAccesoUnc();
    await trazabilidad.EnsureSchemaAsync();
    logger.LogInformation(
        "StartupPortal | RaizUnc={RaizUnc} | UncAccesible={Accesible} | UsaCredenciales={UsaCredenciales} | Entorno={Entorno}",
        rutas.RaizUnc,
        accesible,
        unc.UsaCredenciales,
        app.Environment.EnvironmentName);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "wwwroot"))
});
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();
