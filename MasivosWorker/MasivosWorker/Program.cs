using Infrastructure;
using MasivosWorker;
using Models;
using Models.Dto;
using Services;

// Al correr como servicio de Windows, el directorio de trabajo es System32.
// Fijar ContentRootPath al directorio del exe garantiza que appsettings.json
// siempre se encuentre, independientemente del PC o usuario que ejecute el servicio.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// 🔥 ESTO ES LO QUE TE FALTABA
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "MasivosWorker";
});

// Configuración
builder.Services.Configure<IronBarcodeSettings>(
    builder.Configuration.GetSection("IronBarcode"));

builder.Services.Configure<RutasSettings>(
    builder.Configuration.GetSection("Rutas"));

builder.Services.AddSoporteHelpharmaIntegracion(builder.Configuration);

builder.Services.AddSingleton<IronBarcodeLicenseInitializer>();
builder.Services.AddSingleton<FileManagerInfraestructure>();
builder.Services.AddSingleton<FileWatcherInfraestructure>();
builder.Services.AddSingleton<BarcodeRegionService>();

// Worker
builder.Services.AddHostedService<Worker>();
builder.Services.Configure<FileSettings>(
    builder.Configuration.GetSection("FileSettings"));
var host = builder.Build();

// Inicializar licencia (valida y loguea si es correcta o no)
host.Services.GetRequiredService<IronBarcodeLicenseInitializer>();

// Log de ruta de configuración para diagnóstico en producción
var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation(
    "Startup | ContentRoot={ContentRoot} | Env={Env}",
    builder.Environment.ContentRootPath,
    builder.Environment.EnvironmentName);

host.Run();