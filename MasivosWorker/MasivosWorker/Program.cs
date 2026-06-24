using Infrastructure;
using MasivosWorker;
using Models;
using Models.Dto;
using Services;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Configuration.AddMasivosWorkerLocalOverrides(builder.Environment.EnvironmentName);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MasivosWorker";
});

builder.Services.Configure<IronBarcodeSettings>(
    builder.Configuration.GetSection("IronBarcode"));

builder.Services.Configure<RutasSettings>(
    builder.Configuration.GetSection("Rutas"));

builder.Services.Configure<RedSettings>(
    builder.Configuration.GetSection("Red"));

builder.Services.Configure<FileSettings>(
    builder.Configuration.GetSection("FileSettings"));

builder.Services.Configure<OpenAiSettings>(
    builder.Configuration.GetSection("OpenAi"));

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email"));

builder.Services.Configure<TrazabilidadSqlSettings>(
    builder.Configuration.GetSection("TrazabilidadSql"));

builder.Services.AddSoporteHelpharmaIntegracion(builder.Configuration);

builder.Services.AddSingleton<IronBarcodeLicenseInitializer>();
builder.Services.AddSingleton<RedDisponibleService>();
builder.Services.AddSingleton<FileManagerInfraestructure>();
builder.Services.AddSingleton<DocumentoProcesamientoService>();
builder.Services.AddSingleton<IDocumentoProcesamientoService>(sp =>
    sp.GetRequiredService<DocumentoProcesamientoService>());
builder.Services.AddSingleton<RegistroUsuariosEnProcesoService>();
builder.Services.AddSingleton<LoteWatcherInfrastructure>();
builder.Services.AddSingleton<LoteProcesamientoService>();
builder.Services.AddSingleton<ILoteProcesamientoService>(sp =>
    sp.GetRequiredService<LoteProcesamientoService>());
builder.Services.AddSingleton<ITrazabilidadSqlService, TrazabilidadSqlService>();
builder.Services.AddSingleton<BarcodeRegionService>();
builder.Services.AddSingleton<EmailNotificationService>();
builder.Services.AddSingleton<IEmailNotificationService>(sp =>
    sp.GetRequiredService<EmailNotificationService>());
builder.Services.AddHttpClient<OpenAiBarcodeService>();
builder.Services.AddSingleton<IOpenAiBarcodeService>(sp =>
    sp.GetRequiredService<OpenAiBarcodeService>());

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Services.GetRequiredService<IronBarcodeLicenseInitializer>();
var trazabilidadSql = host.Services.GetRequiredService<ITrazabilidadSqlService>();
try
{
    await trazabilidadSql.EnsureSchemaAsync();
}
catch (Exception ex)
{
    var bootstrapLogger = host.Services.GetRequiredService<ILogger<Program>>();
    bootstrapLogger.LogWarning(
        ex,
        "StartupTrazabilidadSqlIgnorada | Motivo=ErrorInicializandoEsquema | El worker continuara sin bloquearse");
}

var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();
var rutas = builder.Configuration.GetSection("Rutas").Get<RutasSettings>();
var redDisponible = host.Services.GetRequiredService<RedDisponibleService>();
var openAi = builder.Configuration.GetSection("OpenAi").Get<OpenAiSettings>();
var email = builder.Configuration.GetSection("Email").Get<EmailSettings>();
var accesible = redDisponible.EstaDisponible();

startupLogger.LogInformation(
    "StartupUnc | ContentRoot={ContentRoot} | Env={Env} | RaizUnc={RaizUnc} | ArchivosNuevos={ArchivosNuevos} | Accesible={Accesible} | UsaImpersonacion={UsaImpersonacion} | Error={Error}",
    builder.Environment.ContentRootPath,
    builder.Environment.EnvironmentName,
    rutas?.RaizUnc,
    rutas?.RutaArchivosNuevos,
    accesible,
    redDisponible.UsaCredenciales,
    redDisponible.UltimoErrorMensaje ?? "(ninguno)");

if (string.IsNullOrWhiteSpace(openAi?.ApiKey))
{
    startupLogger.LogWarning(
        "StartupOpenAiDeshabilitado | Motivo=ApiKeyNoConfigurada | ArchivoConfiguracionEsperado=appsettings.{Env}.local.json",
        builder.Environment.EnvironmentName);
}

if (email is null || !email.Habilitado)
{
    startupLogger.LogWarning(
        "StartupCorreoDeshabilitado | Motivo=SmtpNoConfiguradoODestinatariosVacios | ArchivoConfiguracionEsperado=appsettings.{Env}.local.json",
        builder.Environment.EnvironmentName);
}

host.Run();
