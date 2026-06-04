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

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MasivosWorker";
});

builder.Services.Configure<IronBarcodeSettings>(
    builder.Configuration.GetSection("IronBarcode"));

builder.Services.Configure<RutasSettings>(
    builder.Configuration.GetSection("Rutas"));

builder.Services.Configure<FileSettings>(
    builder.Configuration.GetSection("FileSettings"));

builder.Services.Configure<OpenAiSettings>(
    builder.Configuration.GetSection("OpenAi"));

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email"));

builder.Services.AddSoporteHelpharmaIntegracion(builder.Configuration);

builder.Services.AddSingleton<IronBarcodeLicenseInitializer>();
builder.Services.AddSingleton<FileManagerInfraestructure>();
builder.Services.AddSingleton<DocumentoProcesamientoService>();
builder.Services.AddSingleton<IDocumentoProcesamientoService>(sp =>
    sp.GetRequiredService<DocumentoProcesamientoService>());
builder.Services.AddSingleton<LoteWatcherInfrastructure>();
builder.Services.AddSingleton<LoteProcesamientoService>();
builder.Services.AddSingleton<LogDiarioService>();
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

var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();
var rutas = builder.Configuration.GetSection("Rutas").Get<RutasSettings>();

startupLogger.LogInformation(
    "Startup | ContentRoot={ContentRoot} | Env={Env} | RaizUnc={RaizUnc} | ArchivosNuevos={ArchivosNuevos}",
    builder.Environment.ContentRootPath,
    builder.Environment.EnvironmentName,
    rutas?.RaizUnc,
    rutas?.RutaArchivosNuevos);

host.Run();
